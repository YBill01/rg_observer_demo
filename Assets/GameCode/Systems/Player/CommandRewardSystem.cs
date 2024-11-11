using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Legacy.Database;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class CommandRewardSystem : ComponentSystem
    {       
        private EntityQuery _query_requests;

        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        private NativeQueue<ClientRequestLoots> _energy;

        protected override void OnCreate()
        {
            _query_requests = GetEntityQuery(
               ComponentType.ReadOnly<CommandRequest>(),
               ComponentType.ReadOnly<CommandArenaTag>(),
               ComponentType.ReadOnly<NetworkMessageRaw>(),
               ComponentType.Exclude<CommandCompleteTag>()
           );

            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
            _player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();
        }

        protected override void OnUpdate()
        {
            var _requests = _query_requests.ToComponentDataArray<CommandRequest>(Allocator.TempJob);
            var _messages = _query_requests.ToComponentDataArray<NetworkMessageRaw>(Allocator.TempJob);
            var _entities = _query_requests.ToEntityArray(Allocator.TempJob);
            var _settings = Database.Settings.Instance.Get<LootSettings>();

            for (int i = 0; i < _requests.Length; ++i)
            {
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                var _index = _requests[i].index;
                var _message = _messages[i];

                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
                    var _command = (ArenaCommandType)_message.ReadByte();
                    UnityEngine.Debug.Log($"CommandLootSystem >> Command: {_command}");
                    switch (_command)
                    {
                        #region ArenaCommandType.Reward
                        case ArenaCommandType.Reward:
                            {
                                var _arena_index = _message.ReadUShort();
                                var _reward_index = _message.ReadByte();

                                if (!Battlefields.Instance.Get(_arena_index, out BinaryBattlefields info))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                if (_reward_index >= info.rewards.Count)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                if (profile.rating.HasReward(_arena_index, _reward_index))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                var _reward = info.rewards[_reward_index];

                                if (_reward.rating > profile.rating.max)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                // mark reward
                                if (!Rewards.Instance.Get(_reward.reward, out BinaryReward binaryReward))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                var _update = new PlayerUpdateLootEvent
                                {
                                    currency = new PlayerProfileCurrency
                                    {
                                        hard = binaryReward.hard,
                                        soft = binaryReward.soft,
                                        //shard = binaryReward.shard
                                    },
                                    cards = new Dictionary<ushort, ushort>(),
                                    lootSourceType = LootSourceType.ArenaReward
                                };

                                // cards
                                if (binaryReward.cards.Count > 0)
                                {
                                    for (byte k = 0; k < binaryReward.cards.Count; ++k)
                                    {
                                        var _reward_card = binaryReward.cards[k];
                                        var cardId = _reward_card.card;
                                        var cardCount = _reward_card.count;

                                        if (_reward_card.count <= 0)
                                        {
                                            if (!Rewards.Instance.GetRandomRarityCard(_reward.reward, out cardId))
                                            {
                                                // TODO: errors
                                                continue;
                                            }
                                            
                                            cardCount = _reward_card.rarity_card.rarity_count;
                                        }
                                        
                                        if (!_update.cards.ContainsKey(cardId))
                                        {
                                            profile.rating.collectedRewards[binaryReward.index] = cardId;
                                            _update.cards[cardId] = 0;
                                        }

                                        _update.cards[cardId] += cardCount;
                                    }
                                }
                                
                                // loot-box
                                _update.lootSourceID = binaryReward.lootbox;
                                
                                if (_update.lootSourceID > 0)
                                {
                                    if (!Loots.Instance.Get(_update.lootSourceID, out BinaryLoot lootbox))
                                    {
                                        // TODO: errors
                                        continue;
                                    }

                                    if (!lootbox.Open(info.index, _settings.percent, out PlayerUpdateLootEvent _lootUpdate))
                                    {
                                        // TODO: errors
                                        continue;
                                    }

                                    _update.currency.hard += _lootUpdate.currency.hard;
                                    _update.currency.soft += _lootUpdate.currency.soft;
                                    //_update.currency.shard += _lootUpdate.currency.shard;
                                    
                                    if (_lootUpdate.cards.Count > 0)
                                    {
                                        foreach (var index in _lootUpdate.cards.Keys)
                                        {
                                            if (!_update.cards.ContainsKey(index))
                                            {
                                                _update.cards[index] = 0;
                                            }

                                            _update.cards[index] = _lootUpdate.cards[index];
                                        }
                                    }
                                }

                                // profile update
                                profile.rating.AddReward(_arena_index, _reward_index);

                                // HARD
                                var newEn = EntityManager.CreateEntity();
                                var newComponen = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.hard,
                                    source_id = info.index,
                                    currencyType = CurrencyType.Hard,
                                    changeSourceType = CurrencyChangeSourceType.ArenaReward
                                };
                                EntityManager.AddComponentData(newEn, newComponen);

                                // SOFT
                                newEn = EntityManager.CreateEntity();
                                newComponen = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.soft,
                                    source_id = info.index,
                                    currencyType = CurrencyType.Soft,
                                    changeSourceType = CurrencyChangeSourceType.ArenaReward
                                };
                                EntityManager.AddComponentData(newEn, newComponen);

                                profile.currency.hard += _update.currency.hard;
                                profile.currency.soft += _update.currency.soft;
                                //profile.currency.shard += _update.currency.shard;

                                foreach (var index in _update.cards.Keys)
                                {
                                    profile.AddCards(index, _update.cards[index]);
                                }

                                if (_auth_system.Sessions.TryGetValue(_index, out Entity entity))
                                {
                                    var _response = default(NetworkMessageRaw);
                                    _response.Write((byte)ObserverPlayerMessage.OpenLootResult);
                                    _update.Serialize(ref _response);

                                    var _player = EntityManager.GetComponentData<ObserverPlayerClient>(entity);
                                    _response.Send(
                                        _player_system.Driver,
                                        _player_system.ReliablePeline,
                                        _player.connection
                                    );
                                }

                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                            }
                            break;
                            #endregion
                    }
                }
            }

            _entities.Dispose();
            _requests.Dispose();
            _messages.Dispose();
        }

    }
}
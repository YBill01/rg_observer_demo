using System.Collections;
using System.Collections.Generic;
using Legacy.Database;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]
    public class CommandBattlePassSystem : ComponentSystem
    {
        private AuthorizationSystem _auth_system;
        private EntityQuery _query_requests;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
        {
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<CommandRequest>(),
                ComponentType.ReadOnly<CommandBattlePassTag>(),
                ComponentType.ReadOnly<NetworkMessageRaw>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );
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
                    var _battlePassData = Shop.Instance.BattlePass.GetCurrent();
                    if (_battlePassData == null)
                    {
                        // TODO: errors
                        continue;
                    }

                    var _command = (BattlePassCommandType) _message.ReadByte();

                    switch (_command)
                    {
                        #region BattlePassCommandType.Reward

                        case BattlePassCommandType.Reward:
                        {
                            var _reward_free_type = _message.ReadBool();
                            var _reward_level_index = _message.ReadByte();

                            var currentLevel = Mathf.Floor(profile.battlePass.stars / 10);

                            if (_reward_level_index > currentLevel)
                            {
                                // TODO: errors
                                continue;
                            }

                            ushort _reward_index = 0;

                            if (_reward_free_type)
                            {
                                if (profile.battlePass.HasFreeReward(_reward_level_index))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                _reward_index = _battlePassData.tresures[_reward_level_index].free;
                            }
                            else
                            {
                                if (profile.battlePass.HasPremiumReward(_reward_level_index))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                _reward_index = _battlePassData.tresures[_reward_level_index].pay;
                            }

                            // mark reward
                            if (!Rewards.Instance.Get(_reward_index, out BinaryReward binaryReward))
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
                                lootSourceType = LootSourceType.BattlePass
                            };

                            // cards
                            if (binaryReward.cards.Count > 0)
                            {
                                for (byte k = 0; k < binaryReward.cards.Count; ++k)
                                {
                                    var _reward_card = binaryReward.cards[k];

                                    if (!_update.cards.ContainsKey(_reward_card.card))
                                    {
                                        _update.cards[_reward_card.card] = 0;
                                    }

                                    _update.cards[_reward_card.card] += _reward_card.count;
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

                                
                                
                                if (!lootbox.Open(Settings.Instance.Get<ArenaSettings>().queue[profile.CurrentArena.number], _settings.percent,
                                    out PlayerUpdateLootEvent _lootUpdate))
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
                            if (_reward_free_type)
                                profile.battlePass.AddBattlePassFreeReward(_reward_level_index, true);
                            else
                                profile.battlePass.AddBattlePassPremiumReward(_reward_level_index, true);

                            // HARD
                            var newEn = EntityManager.CreateEntity();
                            var newComponen = new ObserverPlayerCurrencyChangeEventInfo
                            {
                                player_index = _index,
                                difference = (int)_update.currency.hard,
                                source_id = _reward_level_index,
                                currencyType = CurrencyType.Hard,
                                changeSourceType = CurrencyChangeSourceType.BattlePass
                            };
                            EntityManager.AddComponentData(newEn, newComponen);

                            // SOFT
                            newEn = EntityManager.CreateEntity();
                            newComponen = new ObserverPlayerCurrencyChangeEventInfo
                            {
                                player_index = _index,
                                difference = (int)_update.currency.soft,
                                source_id = _reward_level_index,
                                currencyType = CurrencyType.Soft,
                                changeSourceType = CurrencyChangeSourceType.BattlePass
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
                                _response.Write((byte) ObserverPlayerMessage.OpenLootResult);
                                _update.Serialize(ref _response);

                                var _player = EntityManager.GetComponentData<ObserverPlayerClient>(entity);
                                _response.Send(
                                    _player_system.Driver,
                                    _player_system.ReliablePeline,
                                    _player.connection
                                );
                            }

                            var newEntity = EntityManager.CreateEntity();
                            EntityManager.AddComponentData(newEntity,
                                new ObserverPlayerProfileRequest {index = _index});
                        }
                            break;
                    }

                    #endregion
                }

                EntityManager.AddComponentData(EntityManager.CreateEntity(),
                    new ObserverPlayerProfileRequest {index = _index});

            }

            _entities.Dispose();
            _requests.Dispose();
            _messages.Dispose();
        }
    }
}
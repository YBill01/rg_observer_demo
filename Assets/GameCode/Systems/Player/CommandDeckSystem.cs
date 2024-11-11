using Unity.Entities;
using Unity.Collections;
using Legacy.Database;

namespace Legacy.Observer
{

    [UpdateInGroup(typeof(PlayerSystems))]
	public class CommandDeckSystem : ComponentSystem
	{
		private AuthorizationSystem _auth_system;
		private EntityQuery _query_requests;

		protected override void OnCreate()
		{
			_auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<CommandRequest>(),
                ComponentType.ReadOnly<CommandDeckTag>(),
                ComponentType.ReadOnly<NetworkMessageRaw>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );
        }

		protected override void OnUpdate()
		{
            var _requests = _query_requests.ToComponentDataArray<CommandRequest>(Allocator.TempJob);
            var _messages = _query_requests.ToComponentDataArray<NetworkMessageRaw>(Allocator.TempJob);
            var _entities = _query_requests.ToEntityArray(Allocator.TempJob);

            for (int i = 0; i < _requests.Length; ++i)
            {
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                var _index = _requests[i].index;
                var _message = _messages[i];

                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
                    var _command = (DeckCommandType)_message.ReadByte();
                    switch (_command)
                    {
                        #region DeckCommandType.Hero
                        case DeckCommandType.Hero:
                            {
                                //var _deck_index = _message.ReadByte();
                                var _deck_index = profile.config.deck;
                                var _hero_index = _message.ReadUShort();

                                if (_deck_index >= profile.sets.Count)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                if (!profile.heroes.ContainsKey(_hero_index))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                profile.sets[_deck_index].heroID = _hero_index;
                            }
                            break;
                        #endregion

                        #region DeckCommandType.Card
                        case DeckCommandType.Card:
                            { 
                                var _card_sid = _message.ReadUShort();
                                var _gameSettings = Database.Settings.Instance.Get<BaseGameSettings>();                                

                                if (!profile.cards.ContainsKey(_card_sid))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                var _card_profile = profile.cards[_card_sid];
                                if (_card_profile.level == _gameSettings.maxLevel)
                                {
                                    // TODO: errors
                                    continue;
                                }
                                uint _need_cards = Levels.Instance.GetCountToUpgradeCard(_card_sid, _card_profile.level, UpgradeCostType.CardsCount);
                                uint _need_soft = Levels.Instance.GetCountToUpgradeCard(_card_sid, _card_profile.level, UpgradeCostType.CardsSoft);

                                if (_need_cards > _card_profile.count)
                                {
                                    //-- TODO: errors
                                    var entity = EntityManager.CreateEntity();
                                    EntityManager.AddComponentData(entity, new ObserverPlayerErrorMessage { index = _index, error = CommandError.NotEnoughCard });
                                    continue;
                                }

                                if (_need_soft > profile.currency.soft)
                                {
                                    //-- TODO: errors
                                    var entity = EntityManager.CreateEntity();
                                    EntityManager.AddComponentData(entity, new ObserverPlayerErrorMessage { index = _index, error = CommandError.NotEnoughSoft });
                                    continue;
                                }

                                // SOFT
                                var newEnt = EntityManager.CreateEntity();
                                var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = -(int)_need_soft,
                                    source_id = _card_sid,
                                    currencyType = CurrencyType.Soft,
                                    changeSourceType = CurrencyChangeSourceType.CardUpgrade
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                profile.currency.soft -= _need_soft;

                                var exp_for_player = Levels.Instance.GetCountToUpgradeCard(_card_sid, _card_profile.level, UpgradeCostType.CardsAccountExp);

                                // Для обучения. За самую первую и принудительую прокачку карты мы даем чуть больше опыта
                                if (_card_sid == 3 && _card_profile.level == 1)
                                    exp_for_player += 2;

                                profile.level.exp += exp_for_player;

                                bool takeLevel = false;
                                uint expToLevelUp;
                                while (profile.level.exp >= (expToLevelUp = Levels.Instance.GetToUpgradeCount(profile.level.level, UpgradeCostType.ExpAccount)))
                                {
                                    profile.level.level++;
                                    profile.level.exp -= expToLevelUp;
                                    takeLevel = true;
                                }

                                if (takeLevel)
                                {
                                    var levelUpEntity = EntityManager.CreateEntity();
                                    EntityManager.AddComponentData(levelUpEntity, new ObserverPlayerProfileLevelUp { index = _index });
                                }

                                _card_profile.count -= (ushort)_need_cards;
                                _card_profile.level++;

                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                            }
                            break;
                        #endregion

                        #region DeckCommandType.Change
                        case DeckCommandType.Change:
                            {
                                var _deck_index = _message.ReadByte();
                                if (_deck_index >= profile.sets.Count)
                                {
                                    // TODO: errors
                                    continue;
                                }
                                profile.config.deck = _deck_index;
                                
                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                            }
                            break;
                        #endregion

                        #region DeckCommandType.Modify
                        case DeckCommandType.Modify:
                            {
                                var _deck_index = profile.config.deck;
                                var _deck_card = _message.ReadUShort();
                                var _collection_card = _message.ReadUShort();
                                if(_deck_card== 0 && _collection_card == 0)
                                {
                                    for (byte setIndex=0; setIndex < profile.sets.Count; setIndex++)
                                    {
                                        if (profile.sets[setIndex].list.Count < 8)
                                        {
                                            byte m = (byte)(8 - profile.sets[setIndex].list.Count);
                                            for (byte b = 0; b < m; b++)
                                                profile.sets[setIndex].list.Add(0);
                                        }
                                    }
                                    var entity = EntityManager.CreateEntity();
                                    EntityManager.AddComponentData(entity, new ObserverPlayerProfileRequest { index = _index });
                                    break;
                                }
                                if (_deck_card == 0 && _collection_card > 0)
                                {
                                    bool isTrue = false;
                                    for(byte idCard = 0; idCard < profile.sets[_deck_index].list.Count; idCard++)
                                    {
                                        if (profile.sets[_deck_index].list[idCard] == 0)
                                        {
                                            profile.sets[_deck_index].list[idCard]=_collection_card;
                                            var entity = EntityManager.CreateEntity();
                                            EntityManager.AddComponentData(entity, new ObserverPlayerProfileRequest { index = _index });
                                            isTrue = true;
                                            break;
                                        }
                                    }
                                    if(isTrue)
                                        continue;

                                }
                                if (_deck_card > 0 && _collection_card == 0)
                                {
                                    bool isTrue = false;
                                    for (byte idCard = 0; idCard < profile.sets[_deck_index].list.Count; idCard++)
                                    {
                                        if (profile.sets[_deck_index].list[idCard] == _deck_card)
                                        {
                                            profile.sets[_deck_index].list[idCard] = 0;
                                            var entity = EntityManager.CreateEntity();
                                            EntityManager.AddComponentData(entity, new ObserverPlayerProfileRequest { index = _index });
                                            isTrue = true;
                                            break;
                                        }
                                    }
                                    if (isTrue)
                                        continue;
                                }
                                if (_deck_index >= profile.sets.Count)
                                {
                                    // TODO: errors
                                    continue;
                                }

                                if (!profile.cards.ContainsKey(_collection_card))
                                {
                                    // TODO: errors
                                    continue;
                                }       

                                if (!profile.sets[_deck_index].list.Contains(_deck_card))
                                {
                                    // TODO: errors
                                    continue;
                                }

                                var card_index = profile.sets[_deck_index].list.IndexOf(_deck_card);
                                profile.sets[_deck_index].list[card_index] = _collection_card;
                            }
                            break;
                        #endregion

                        #region DeckCommandType.ChangeSort
                        case DeckCommandType.ChangeSort:
                            {
                                var sortID = _message.ReadByte();
                                profile.config.sort = sortID;
                            }
                            break;
                        #endregion

                        #region DeckCommandType.View
                        case DeckCommandType.View:
                            {
                                var _card_index = _message.ReadUShort();
                                //UnityEngine.Debug.LogError(_card_index);
                                if (profile.cards.ContainsKey(_card_index))
                                {
                                    profile.cards[_card_index].isNew = false;
                                }
                                else
                                {
                                    var entity = EntityManager.CreateEntity();
                                    return;
                                }

                               // var entity = EntityManager.CreateEntity();
                               // EntityManager.AddComponentData(entity, new ObserverPlayerProfileRequest { index = _index });
                            }
                            break;
                            #endregion
                    }
                }
            }
            
            _entities.Dispose();
            _requests.Dispose();
            _messages.Dispose();
            //EntityManager.DestroyEntity(_query_requests);
        }
    }
}
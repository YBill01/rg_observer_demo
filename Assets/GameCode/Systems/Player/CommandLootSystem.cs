using System;
using Unity.Entities;
using Legacy.Database;
using Unity.Collections;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class CommandLootSystem : ComponentSystem
    {
        private EntityQuery _query_requests;

        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        private NativeQueue<ClientRequestLoots> _energy;

        protected override void OnCreate()
        {
            _query_requests = GetEntityQuery(
               ComponentType.ReadOnly<CommandRequest>(),
               ComponentType.ReadOnly<CommandLootTag>(),
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

            var _current_time = System.DateTime.UtcNow;
            var _settings = Database.Settings.Instance.Get<LootSettings>();

            for (int i = 0; i < _requests.Length; ++i)
            {
                var _index = _requests[i].index;
                var _message = _messages[i];
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
                    var _command = (LootCommandType)_message.ReadByte();
                    UnityEngine.Debug.Log($"CommandLootSystem >> Command: {_command}");
                    profile.loots.UpdateTimers();
                    switch (_command)
                    {
                        #region LootCommandType.Arrive
                        case LootCommandType.Arrive:
                            {
                                var _box_index = _message.ReadByte();

                                var _loot_box = profile.loots.boxes[_box_index - 1];

                                if (_loot_box.index == 0)
                                {
                                    // TODO: no box in slot
                                    continue;
                                }

                                _loot_box.arrived = true;
                            }
                            break;
                        #endregion

                        #region LootCommandType.Open
                        case LootCommandType.Open:
                            {
                                var _box_index = _message.ReadByte();
                                
                                if (!profile.loots.CanOpenBox && !profile.battlePass.isPremiumBought)
                                {
                                    // TODO: открывается другой сундук
                                    EntityManager.DestroyEntity(_entities[i]);
                                    break;
                                }

                                if (!profile.loots.CanOpenNextBox && profile.battlePass.isPremiumBought)
                                {
                                    // TODO: открывается другой сундук в очереди
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }
                                
                                if (_box_index > _settings.slots)
                                {
                                    // TODO: errors
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }

                                if (_box_index == profile.loots.index)
                                {
                                    // TODO: уже открывается
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }

                                if (profile.battlePass.isPremiumBought && _box_index == profile.loots.nextIndex)
                                {
                                    // TODO: уже открывается
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }

                                var _loot_box = profile.loots.boxes[_box_index - 1];

                                if (_loot_box.index == 0)
                                {
                                    // TODO: no box in slot
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }

                                if (!Loots.Instance.Get(_loot_box.index, out BinaryLoot box))
                                {
                                    // TODO: No such box in binary
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }

                                if (profile.loots.CanOpenBox)
                                {
                                    profile.loots.index = _box_index;
                                    _loot_box.timer = _current_time.AddSeconds(box.time).ToUniversalTime();
                                    _loot_box.started = true;
                                }
                                else if (profile.battlePass.isPremiumBought)
                                {
                                    profile.loots.nextIndex = _box_index;
                                }
                                
                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });
                            }

                            break;
                        #endregion

                        #region LootCommandType.Reward
                        case LootCommandType.Reward:
                            {
                                var _box_index = _message.ReadByte();
                                var _loot_box = profile.loots.boxes[_box_index - 1];

                                if (!_loot_box.isOpened)
                                {
                                    // TODO: errors
                                    EntityManager.DestroyEntity(_entities[i]);

                                    var _diff = _loot_box.timer.ToLocalTime() - DateTime.Now;
                                    UnityEngine.Debug.Log($"CommandLootSystem >> Time Error: {_diff.TotalSeconds}s");
                                    continue;
                                }

                                if (!Loots.Instance.Get(_loot_box.index, out BinaryLoot lootbox))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: errors
                                    continue;
                                }

                                PlayerUpdateLootEvent _update;

                                if (!lootbox.Open(_loot_box.battlefield, _settings.percent, out _update))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: errors
                                    continue;
                                }

                                // HARD
                                var newEnt = EntityManager.CreateEntity();
                                var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.hard,
                                    source_id = _loot_box.index,
                                    currencyType = CurrencyType.Hard,
                                    changeSourceType = CurrencyChangeSourceType.LootBox
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                // SOFT
                                newEnt = EntityManager.CreateEntity();
                                newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.soft,
                                    source_id = _loot_box.index,
                                    currencyType = CurrencyType.Soft,
                                    changeSourceType = CurrencyChangeSourceType.LootBox
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                profile.loots.boxes[_box_index - 1] = new PlayerProfileLootBox();

                                profile.currency.hard += _update.currency.hard;
                                profile.currency.soft += _update.currency.soft;
                                //profile.currency.shard += _update.currency.shard;

                                bool changesInSet = false;
                                foreach (var index in _update.cards.Keys)
                                {
                                    if (changesInSet)
                                        profile.AddCards(index, _update.cards[index]);
                                    else
                                        changesInSet = profile.AddCards(index, _update.cards[index]);
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

                        #region LootCommandType.Skip
                        case LootCommandType.Skip:
                            {
                                var _box_index = _message.ReadByte();

                                if (_box_index > _settings.slots)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: errors
                                    continue;
                                }

                                var _loot_box = profile.loots.boxes[_box_index - 1];

                                if (_loot_box.index == 0)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: no box in slot
                                    continue;
                                }

                                if (_loot_box.isOpened)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: AlreadyOpened
                                    continue;
                                }

                                if (!Loots.Instance.Get(_loot_box.index, out BinaryLoot box))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: No such box in binary
                                    continue;
                                }

                                float _delta;
                                if (_box_index == profile.loots.index)
								{
                                    _delta = (float)(_loot_box.timer.ToLocalTime() - DateTime.Now).TotalSeconds;
                                }
							    else
								{
									if (profile.arenaBoosterTime.IsActive)
									{
                                        _delta = profile.arenaBoosterTime.GetSecondsToOpen(box.time);
                                    }
									else
									{
                                        _delta = box.time;
                                    }
                                }
                                
                                var _shardsToSkip = Loots.PriceToSkip(_delta, _settings);

                                if (profile.currency.hard < _shardsToSkip)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: Need more hard
                                    continue;
                                }
                                profile.currency.hard -= _shardsToSkip;


                                if (!Loots.Instance.Get(_loot_box.index, out BinaryLoot lootbox))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: errors
                                    continue;
                                }

                                PlayerUpdateLootEvent _update;

                                if (!lootbox.Open(_loot_box.battlefield, _settings.percent, out _update))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    // TODO: errors
                                    continue;
                                }

                                // HARD
                                var newEnt = EntityManager.CreateEntity();
                                var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.hard,
                                    source_id = _loot_box.index,
                                    currencyType = CurrencyType.Hard,
                                    changeSourceType = CurrencyChangeSourceType.LootBox
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                // SOFT
                                newEnt = EntityManager.CreateEntity();
                                newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.soft,
                                    source_id = _loot_box.index,
                                    currencyType = CurrencyType.Soft,
                                    changeSourceType = CurrencyChangeSourceType.LootBox
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                profile.loots.boxes[_box_index - 1] = new PlayerProfileLootBox();

                                profile.currency.hard += _update.currency.hard;
                                profile.currency.soft += _update.currency.soft;
                                //profile.currency.shard += _update.currency.shard;

                                bool changesInSet = false;
                                foreach (var index in _update.cards.Keys)
                                {
                                    if (changesInSet)
                                        profile.AddCards(index, _update.cards[index]);
                                    else
                                        changesInSet = profile.AddCards(index, _update.cards[index]);
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
                                /*
                                // HARD
                                var newEnt = EntityManager.CreateEntity();
                                var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = -_shardsToSkip,
                                    source_id = _loot_box.index,
                                    currencyType = CurrencyType.Hard,
                                    changeSourceType = CurrencyChangeSourceType.LootBoxSpeedUp
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                profile.currency.hard -= _shardsToSkip;

                                _loot_box.timer = DateTime.UtcNow;
                                _loot_box.started = true;

                                if (_box_index == profile.loots.index)
                                {
                                    if (profile.loots.nextIndex != 0)
                                    {
                                        profile.loots.index = profile.loots.nextIndex;
                                        profile.loots.nextIndex = 0;

                                        var nextBox = profile.loots.boxes[profile.loots.index - 1];
                                        nextBox.started = true;
                                        if (!Loots.Instance.Get(nextBox.index, out BinaryLoot binaryNextBox))
                                        {
                                            EntityManager.DestroyEntity(_entities[i]);

                                            // TODO: No such box in binary
                                            continue;
                                        }
                                        nextBox.timer = DateTime.UtcNow.AddSeconds(binaryNextBox.time);
                                    }
                                    else
                                    {
                                        profile.loots.index = 0;
                                    }
                                }
                                else if (_box_index == profile.loots.nextIndex)
                                {
                                    profile.loots.nextIndex = 0;
                                }

                                var newEntity = EntityManager.CreateEntity();
                                EntityManager.AddComponentData(newEntity, new ObserverPlayerProfileRequest { index = _index });*/
                            }
                            break;
                        #endregion

                        #region LootCommandType.Boost
                        case LootCommandType.Boost:
                            {
                                var _box_index = _message.ReadByte();

                                var _loot_box = profile.loots.boxes[_box_index - 1];

                                if (_loot_box.started == false)
                                {
                                    // TODO: box is not opened
                                    continue;
                                }

                                if (!profile.arenaBoosterTime.IsActive || _loot_box.isBoostered)
								{
                                    // TODO: booster is not activated
                                    continue;
                                }

                                uint secondsToOpen = profile.arenaBoosterTime.GetSecondsToOpen(_loot_box.CalculateSecondsToOpen);

                                _loot_box.secondsbooster = (ushort)secondsToOpen;
                                _loot_box.timer = _current_time.AddSeconds(secondsToOpen).ToUniversalTime();

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

            //EntityManager.DestroyEntity(_query_requests);
        }
    }
}
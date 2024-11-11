using Legacy.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]
    public class CommandShopSystem : ComponentSystem
    {
        private AuthorizationSystem _auth_system;
        private EntityQuery _query_requests;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
        {
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<CommandRequest>(),
                ComponentType.ReadOnly<CommandShopTag>(),
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

            for (int i = 0; i < _requests.Length; ++i)
            {
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                var _index = _requests[i].index;
                var _message = _messages[i];

                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profileInstance))
                {
                    var _command = (ShopCommandType)_message.ReadByte();

                    switch (_command)
                    {
                        #region ShopCommandType.Action
                        case ShopCommandType.Action:
                            {
                                var _offer_sid = _message.ReadUShort();
                                UnityEngine.Debug.Log($"Try buy action. Offer: {_offer_sid}");

                                if (!Shop.Instance.Actions.Get(_offer_sid, out BinaryAction offer))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }
                                
                                bool isLootAdd;
								if (offer.price.soft > 0)
								{
                                    // SOFT
                                    var newEnt = EntityManager.CreateEntity();
                                    var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                    {
                                        player_index = _index,
                                        difference = -offer.price.soft,
                                        source_id = offer.index,
                                        currencyType = CurrencyType.Soft,
                                        changeSourceType = CurrencyChangeSourceType.ShopOffer
                                    };
                                    EntityManager.AddComponentData(newEnt, newComponent);

                                    profileInstance.currency.soft -= (uint)offer.price.soft;

                                    isLootAdd = true;
                                }
                                else if (offer.price.hard > 0)
								{
                                    // HARD
                                    var newEnt = EntityManager.CreateEntity();
                                    var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                    {
                                        player_index = _index,
                                        difference = -offer.price.hard,
                                        source_id = offer.index,
                                        currencyType = CurrencyType.Hard,
                                        changeSourceType = CurrencyChangeSourceType.ShopOffer
                                    };
                                    EntityManager.AddComponentData(newEnt, newComponent);

                                    profileInstance.currency.hard -= (uint)offer.price.hard;

                                    isLootAdd = true;
                                }
                                else
								{
                                    FixedString4096 receipt = _message.ReadString4096();
                                    UnityEngine.Debug.Log($"Try buy bank. ReceiptLength: {receipt.Length}");
                                    UnityEngine.Debug.Log($"Try buy bank. Receipt: {receipt}");
                                    if (IAPValidator.ValidateReceipt(receipt, out ObserverPlayerPaymentResult paymentResult))
                                    {
                                        UnityEngine.Debug.Log($"Receipt valid!");

                                        isLootAdd = true;
                                    }
                                    else
                                    {
                                        UnityEngine.Debug.Log($"Error! Receipt invalid!");

                                        //TODO: errors Invalid purchase receipt
                                        EntityManager.DestroyEntity(_entities[i]);
                                        continue;
                                    }
                                    paymentResult.player_index = _index;
                                    EntityManager.AddComponentData(EntityManager.CreateEntity(), paymentResult);
                                }

                                if (!isLootAdd)
								{
                                    continue;
								}

                                if (Rewards.Instance.Get(offer.treasure, out BinaryReward reward))
								{
                                    if (reward.soft > 0)
								    {
                                        // SOFT
                                        var newEnt = EntityManager.CreateEntity();
                                        var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = reward.soft,
                                            source_id = reward.index,
                                            currencyType = CurrencyType.Soft,
                                            changeSourceType = CurrencyChangeSourceType.ShopReward
                                        };
                                        EntityManager.AddComponentData(newEnt, newComponent);

                                        profileInstance.currency.soft += reward.soft;
                                    }
                                    if (reward.hard > 0)
								    {
                                        // HARD
                                        var newEnt = EntityManager.CreateEntity();
                                        var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = reward.hard,
                                            source_id = reward.index,
                                            currencyType = CurrencyType.Hard,
                                            changeSourceType = CurrencyChangeSourceType.ShopReward
                                        };
                                        EntityManager.AddComponentData(newEnt, newComponent);

                                        profileInstance.currency.hard += reward.hard;
                                    }
                                }

                                profileInstance.actions.AddCountBuy(_offer_sid);

                                UnityEngine.Debug.Log($"Buyed action. Offer: {_offer_sid}");

                                break;
                            }
                        #endregion

                        #region ShopCommandType.ActionLootBox
                        case ShopCommandType.ActionLootBox:
                            {
                                var _offer_sid = _message.ReadUShort();
                                UnityEngine.Debug.Log($"Try buy action. Offer: {_offer_sid}");

                                if (!Shop.Instance.Actions.Get(_offer_sid, out BinaryAction offer))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }

                                if (Rewards.Instance.Get(offer.treasure, out BinaryReward reward))
                                {
                                    if (reward.lootbox > 0)
                                    {
                                        if (!Loots.Instance.Get(reward.lootbox, out BinaryLoot lootbox))
                                        {
                                            EntityManager.DestroyEntity(_entities[i]);

                                            continue;
                                        }

                                        var _arena_settings = Settings.Instance.Get<ArenaSettings>();

                                        PlayerUpdateLootEvent _update;

                                        if (!lootbox.Open(_arena_settings.queue[profileInstance.CurrentArena.number], 0, out _update))
                                        {
                                            EntityManager.DestroyEntity(_entities[i]);

                                            continue;
                                        }

                                        // SOFT
                                        var newEnt = EntityManager.CreateEntity();
                                        var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = (int)_update.currency.soft,
                                            source_id = reward.index,
                                            currencyType = CurrencyType.Soft,
                                            changeSourceType = CurrencyChangeSourceType.ShopReward
                                        };
                                        EntityManager.AddComponentData(newEnt, newComponent);

                                        // HARD
                                        newEnt = EntityManager.CreateEntity();
                                        newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = (int)_update.currency.hard,
                                            source_id = reward.index,
                                            currencyType = CurrencyType.Hard,
                                            changeSourceType = CurrencyChangeSourceType.ShopReward
                                        };
                                        EntityManager.AddComponentData(newEnt, newComponent);

                                        profileInstance.currency.hard += _update.currency.hard;
                                        profileInstance.currency.soft += _update.currency.soft;

                                        bool changesInSet = false;
                                        foreach (var index in _update.cards.Keys)
                                        {
                                            if (changesInSet)
                                                profileInstance.AddCards(index, _update.cards[index]);
                                            else
                                                changesInSet = profileInstance.AddCards(index, _update.cards[index]);
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
                                    }
                                }

                                break;
                            }
                        #endregion

                        #region ShopCommandType.Bank
                        case ShopCommandType.Bank:
                            {
                                var _offer_sid = _message.ReadUShort();
                                UnityEngine.Debug.Log($"Try buy bank. Offer: {_offer_sid}");

                                if (!Shop.Instance.Bank.Get(_offer_sid, out BinaryBank offer))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }                                

                                switch (offer.type)
                                {
                                    case CurrencyType.Hard:
                                        FixedString4096 receipt = _message.ReadString4096();
                                        UnityEngine.Debug.Log($"Try buy bank. ReceiptLength: {receipt.Length}");
                                        UnityEngine.Debug.Log($"Try buy bank. Receipt: {receipt}");
                                        if (IAPValidator.ValidateReceipt(receipt, out ObserverPlayerPaymentResult paymentResult))
                                        {
                                            UnityEngine.Debug.Log($"Receipt valid!");

                                            // HARD
                                            var newEn = EntityManager.CreateEntity();
                                            var newComponen = new ObserverPlayerCurrencyChangeEventInfo
                                            {
                                                player_index = _index,
                                                difference = (int)offer.count,
                                                source_id = offer.index,
                                                currencyType = CurrencyType.Hard,
                                                changeSourceType = CurrencyChangeSourceType.ShopBank
                                            };
                                            EntityManager.AddComponentData(newEn, newComponen);

                                            profileInstance.currency.hard += offer.count;
                                            profileInstance.payer = true;
                                        }
                                        else
                                        {
                                            UnityEngine.Debug.Log($"Error! Receipt invalid!");

                                            //TODO: errors Invalid purchase receipt
                                            EntityManager.DestroyEntity(_entities[i]);
                                            continue;
                                        }
                                        paymentResult.player_index = _index;
                                        EntityManager.AddComponentData(EntityManager.CreateEntity(), paymentResult);

                                        break;
                                    case CurrencyType.Soft:
                                        if (offer.hardPrice > profileInstance.currency.hard)
                                        {
                                            EntityManager.DestroyEntity(_entities[i]);

                                            continue;
                                        }

                                        // HARD
                                        var newEnt = EntityManager.CreateEntity();
                                        var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = -offer.hardPrice,
                                            source_id = offer.index,
                                            currencyType = CurrencyType.Hard,
                                            changeSourceType = CurrencyChangeSourceType.ShopBank
                                        };
                                        EntityManager.AddComponentData(newEnt, newComponent);

                                        // SOFT
                                        newEnt = EntityManager.CreateEntity();
                                        newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = (int)offer.count,
                                            source_id = offer.index,
                                            currencyType = CurrencyType.Soft,
                                            changeSourceType = CurrencyChangeSourceType.ShopBank
                                        };
                                        EntityManager.AddComponentData(newEnt, newComponent);

                                        profileInstance.currency.hard -= offer.hardPrice;
                                        profileInstance.currency.soft += offer.count;
                                        break;
                                    //case CurrencyType.Shards:
                                        //profileInstance.currency.hard -= offer.hardPrice;
                                        //profileInstance.currency.shard += offer.count;
                                        //break;
                                }

                                break;
                            }
                        #endregion

                        #region ShopCommandType.LootBox
                        case ShopCommandType.LootBox:
                            {
                                var _offer_sid = _message.ReadUShort();

                                var _settings = Settings.Instance.Get<LootSettings>();
                                var _arena_settings = Settings.Instance.Get<ArenaSettings>();

                                if (!Shop.Instance.LootBox.Get(_offer_sid, out BinaryMarketLoot offer))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }

                                if (!Loots.Instance.Get(offer.lootbox, out BinaryLoot lootbox))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }

                                if (offer.hardPrice > profileInstance.currency.hard)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }

                                if (profileInstance.CurrentArena.number + 1 < offer.arena)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }

                                // HARD
                                var newEnt = EntityManager.CreateEntity();
                                var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = -offer.hardPrice,
                                    source_id = offer.index,
                                    currencyType = CurrencyType.Hard,
                                    changeSourceType = CurrencyChangeSourceType.ShopLootBox
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                profileInstance.currency.hard -= offer.hardPrice;

								PlayerUpdateLootEvent _update;

								if (!lootbox.Open(_arena_settings.queue[profileInstance.CurrentArena.number], 0, out _update))
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }

                                // HARD
                                newEnt = EntityManager.CreateEntity();
                                newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.hard,
                                    source_id = offer.index,
                                    currencyType = CurrencyType.Hard,
                                    changeSourceType = CurrencyChangeSourceType.ShopLootBox
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                // SOFT
                                newEnt = EntityManager.CreateEntity();
                                newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = (int)_update.currency.soft,
                                    source_id = offer.index,
                                    currencyType = CurrencyType.Soft,
                                    changeSourceType = CurrencyChangeSourceType.ShopLootBox
                                };
                                EntityManager.AddComponentData(newEnt, newComponent);

                                profileInstance.currency.hard += _update.currency.hard;
                                profileInstance.currency.soft += _update.currency.soft;
                                //profileInstance.currency.shard += _update.currency.shard;

                                bool changesInSet = false;
                                foreach (var index in _update.cards.Keys)
                                {
                                    if (changesInSet)
                                        profileInstance.AddCards(index, _update.cards[index]);
                                    else
                                        changesInSet = profileInstance.AddCards(index, _update.cards[index]);
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
                                break;
                            }
                        #endregion

                        #region ShopCommandType.Daily
                        case ShopCommandType.Daily:
                            {
                                var _offer_sid = _message.ReadUShort();

                                if (profileInstance.dailyDeals.offers.Count > _offer_sid)
                                {
                                    var dailyDeal = profileInstance.dailyDeals.offers[_offer_sid];
                                    if (dailyDeal != null)
                                    {
                                        if (dailyDeal.buyed)
                                        {
                                            //TODO: errors
                                            continue;
                                        }

                                        if (dailyDeal.hard > profileInstance.currency.hard ||
                                            dailyDeal.soft > profileInstance.currency.soft)
                                        {
                                            //TODO: errors
                                            continue;
                                        }

                                        switch (dailyDeal.type)
                                        {
                                            case DailyDealsTreasureType.Cards:
                                                profileInstance.AddCards(dailyDeal.treasure.tid, (ushort)dailyDeal.treasure.count);
                                                break;
                                            case DailyDealsTreasureType.Hard:

                                                // HARD
                                                var newEnt = EntityManager.CreateEntity();
                                                var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                                {
                                                    player_index = _index,
                                                    difference = (int)dailyDeal.treasure.count,
                                                    source_id = dailyDeal.treasure.tid,
                                                    currencyType = CurrencyType.Hard,
                                                    changeSourceType = CurrencyChangeSourceType.DailyDeals
                                                };
                                                EntityManager.AddComponentData(newEnt, newComponent);

                                                profileInstance.currency.hard += dailyDeal.treasure.count;
                                                break;
                                            case DailyDealsTreasureType.Soft:

                                                // SOFT
                                                var newEn = EntityManager.CreateEntity();
                                                var newComponen = new ObserverPlayerCurrencyChangeEventInfo
                                                {
                                                    player_index = _index,
                                                    difference = (int)dailyDeal.treasure.count,
                                                    source_id = dailyDeal.treasure.tid,
                                                    currencyType = CurrencyType.Soft,
                                                    changeSourceType = CurrencyChangeSourceType.DailyDeals
                                                };
                                                EntityManager.AddComponentData(newEn, newComponen);

                                                profileInstance.currency.soft += dailyDeal.treasure.count;
                                                break;
                                            case DailyDealsTreasureType.LootBoX:
                                                if (!Loots.Instance.Get(dailyDeal.treasure.tid, out BinaryLoot lootbox))
                                                {
                                                    // TODO: errors
                                                    continue;
                                                }

                                                var _settings = Settings.Instance.Get<LootSettings>();
												PlayerUpdateLootEvent _update;

												//TODO add arena id from profile
												if (!lootbox.Open(1, _settings.percent, out _update))
                                                {
                                                    // TODO: errors
                                                    continue;
                                                }

                                                // HARD
                                                newEnt = EntityManager.CreateEntity();
                                                newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                                {
                                                    player_index = _index,
                                                    difference = (int)_update.currency.hard,
                                                    source_id = dailyDeal.treasure.tid,
                                                    currencyType = CurrencyType.Hard,
                                                    changeSourceType = CurrencyChangeSourceType.DailyDeals
                                                };
                                                EntityManager.AddComponentData(newEnt, newComponent);

                                                // SOFT
                                                newEnt = EntityManager.CreateEntity();
                                                newComponent = new ObserverPlayerCurrencyChangeEventInfo
                                                {
                                                    player_index = _index,
                                                    difference = (int)_update.currency.soft,
                                                    source_id = dailyDeal.treasure.tid,
                                                    currencyType = CurrencyType.Soft,
                                                    changeSourceType = CurrencyChangeSourceType.DailyDeals
                                                };
                                                EntityManager.AddComponentData(newEnt, newComponent);

                                                profileInstance.currency.hard += _update.currency.hard;
                                                profileInstance.currency.soft += _update.currency.soft;
                                                //profileInstance.currency.shard += _update.currency.shard;

                                                foreach (var index in _update.cards.Keys)
                                                {
                                                    profileInstance.AddCards(index, _update.cards[index]);
                                                }

                                                //_update_loot_box(_collection, _index, profileInstance.loots);

                                                if (_auth_system.Sessions.TryGetValue(_index, out Entity entity))
                                                {
                                                    var _response = default(NetworkMessageRaw);
                                                    _response.Write((byte)ObserverPlayerMessage.OpenLootResult);
                                                    _update.Serialize(ref _response);

                                                    var _player =
                                                        EntityManager.GetComponentData<ObserverPlayerClient>(entity);
                                                    _response.Send(
                                                        _player_system.Driver,
                                                        _player_system.ReliablePeline,
                                                        _player.connection
                                                    );
                                                }

                                                break;
                                        }

                                        // SOFT
                                        var newEntity = EntityManager.CreateEntity();
                                        var newCompon = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = -(int)dailyDeal.soft,
                                            source_id = dailyDeal.treasure.tid,
                                            currencyType = CurrencyType.Soft,
                                            changeSourceType = CurrencyChangeSourceType.DailyDeals
                                        };
                                        EntityManager.AddComponentData(newEntity, newCompon);

                                        // HARD
                                        newEntity = EntityManager.CreateEntity();
                                        newCompon = new ObserverPlayerCurrencyChangeEventInfo
                                        {
                                            player_index = _index,
                                            difference = -(int)dailyDeal.hard,
                                            source_id = dailyDeal.treasure.tid,
                                            currencyType = CurrencyType.Hard,
                                            changeSourceType = CurrencyChangeSourceType.DailyDeals
                                        };
                                        EntityManager.AddComponentData(newEntity, newCompon);

                                        profileInstance.currency.hard -= dailyDeal.hard;
                                        profileInstance.currency.soft -= dailyDeal.soft;
                                        profileInstance.dailyDeals.BuyOffer(_offer_sid);
                                    }
                                }

                                break;
                            }

                        #endregion
                            
                        #region ShopCommandType.NotEnoughCoins
                        case ShopCommandType.NotEnoughCoins:
                            {
                                var _offerHard = _message.ReadInt();
                                uint _need = (uint)System.Math.Ceiling((decimal)_offerHard / 16); 
                                if (_need < 1) _need = 1;
                                Debug.Log("need hard "+ _need);
                                if ((_need) > profileInstance.currency.hard)
                                {
                                    var entity = EntityManager.CreateEntity();
                                    EntityManager.AddComponentData(entity, new ObserverPlayerErrorMessage { index = _index, error = CommandError.NotEnoughHard });
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }

                                // HARD
                                var newEntity = EntityManager.CreateEntity();
                                var newCompon = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = -(int)_need,
                                    source_id = 0,
                                    currencyType = CurrencyType.Hard,
                                    changeSourceType = CurrencyChangeSourceType.CurrencyExchange
                                };
                                EntityManager.AddComponentData(newEntity, newCompon);

                                // SOFT
                                newEntity = EntityManager.CreateEntity();
                                newCompon = new ObserverPlayerCurrencyChangeEventInfo
                                {
                                    player_index = _index,
                                    difference = _offerHard,
                                    source_id = 0,
                                    currencyType = CurrencyType.Soft,
                                    changeSourceType = CurrencyChangeSourceType.CurrencyExchange
                                };
                                EntityManager.AddComponentData(newEntity, newCompon);

                                profileInstance.currency.hard -= _need;
                                profileInstance.currency.soft += (uint)_offerHard;
                                
                                break;
                            }
                        #endregion

                        #region ShopCommandType.BattlePass
                        case ShopCommandType.BattlePass:
                            {

                                var battlePassData = Shop.Instance.BattlePass.GetCurrent();

                                if (battlePassData == null)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }

                                if (profileInstance.battlePass.isPremiumBought)
                                {
                                    EntityManager.DestroyEntity(_entities[i]);

                                    continue;
                                }


                                FixedString4096 receipt = _message.ReadString4096();
                                if (IAPValidator.ValidateReceipt(receipt, out ObserverPlayerPaymentResult paymentResult))
                                {
                                    profileInstance.battlePass.isPremiumBought = true;
                                    profileInstance.payer = true;
                                }
                                else
                                {
                                    EntityManager.DestroyEntity(_entities[i]);
                                    continue;
                                }
                                paymentResult.player_index = _index;
                                EntityManager.AddComponentData(EntityManager.CreateEntity(), paymentResult);
                                break;
                            }

                        #endregion
                    }
                }

                EntityManager.AddComponentData(EntityManager.CreateEntity(),
                    new ObserverPlayerProfileRequest { index = _index });
                
            }
            
            _entities.Dispose();
            _requests.Dispose();
            _messages.Dispose();
        }
    }
}
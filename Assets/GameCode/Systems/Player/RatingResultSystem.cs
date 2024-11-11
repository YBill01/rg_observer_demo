using System;
using System.Diagnostics;
using Unity.Entities;
using Unity.Collections;
using Legacy.Database;
using Unity.Mathematics;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class RatingResultSystem : ComponentSystem
    {

        private EntityQuery _query_results;
        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
        {
            _query_results = GetEntityQuery(
                ComponentType.ReadOnly<ObserverBattleRatingResult>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
            _player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();

            RequireForUpdate(_query_results);
        }

        protected override void OnUpdate()
        {
            var _requests = _query_results.ToComponentDataArray<ObserverBattleRatingResult>(Allocator.Temp);
            //var _collection = _auth_system.Database.GetCollection<PlayerProfileInstance>("users");
            var _entities = _query_results.ToEntityArray(Allocator.Temp);
            bool isWins = false;
            for (int i = 0; i < _requests.Length; ++i)
            {
                var _request = _requests[i];

                PlayerProfileInstance _playerProfile = GetProfile(_request.player);

                if (_playerProfile != null)
                {
                    _playerProfile.session.port = 0;

                    var _winner_side = (BattlePlayerSide)_request.result.winner;
                    if (_request.profile.is_bot) continue;

                    if (_auth_system.Sessions.TryGetValue(_playerProfile._id, out Entity playerConnectionEntity))
                    {
                        EntityManager.RemoveComponent<ObserverPlayerInBattle>(playerConnectionEntity);
                    }

                    if (Battlefields.Instance.Get(_request.battlefied, out BinaryBattlefields battlefield))
                    {

                        // build reward
                        var _loots_settings = Database.Settings.Instance.Get<LootSettings>();
                        var _arena_settings = Database.Settings.Instance.Get<ArenaSettings>();
                        var _hero = _request.profile.hero;

                        var _rating_reward = default(BattleRatingResultReward);
                        _rating_reward.result = _request.result;

                        if (_winner_side == _request.side)
                        {
                            isWins = true;

                            var _get_rating = battlefield.rating_rules.victory.GetRating();
                            _rating_reward.rating = _request.profile.rating.current + _get_rating;

                            _rating_reward.rating = math.min(_rating_reward.rating, 1599); //TODO 22:08 oclock / sdelat normalno

                            if (_rating_reward.rating > _rating_reward.maxRating)
                            {
                                _rating_reward.maxRating = math.max(_rating_reward.rating, _request.profile.rating.max);
                            }

                            //new arena booster
                            var currentArena = Settings.Instance.Get<ArenaSettings>().GetArenaData((ushort)_playerProfile.rating.max);
                            var newArena = Settings.Instance.Get<ArenaSettings>().GetArenaData((ushort)(_rating_reward.maxRating));
                            //var newArena = Settings.Instance.Get<ArenaSettings>().GetArenaData((ushort) (_playerProfile.rating.max + _rating_reward.maxRating));
                            if (currentArena.number != newArena.number)
                            {
                                _playerProfile.arenaBoosterTime.ApplyNewTime();
                            }

                            // lootbox
                            if (_request.profile.freeslot < _loots_settings.slots)
                            {
                                _rating_reward.lootbox = new BattleRatingResultRewardBox
                                {
                                    slot = _request.profile.freeslot,
                                    index = _playerProfile.config.GetNextBox(),
                                    battlefield = Settings.Instance.Get<ArenaSettings>().queue[_playerProfile.CurrentArena.number]
                                };
                            }

                            _playerProfile.rating.max = _rating_reward.maxRating;
                            // TODO: soft count (reset every day)
                            _rating_reward.soft = battlefield.rating_rules.victory.soft.amount;
                        }
                        else
                        {

                            var _get_rating = battlefield.rating_rules.lose.GetRating();
                            if (_request.profile.rating.current >= _get_rating)
                            {
                                // TODO: battlefield.rating_rules.checkpoint ?? (_arena_start_rating = 0)
                                var _arena_start_rating = _arena_settings.StartRating(_playerProfile.CurrentArena.index);
                                _rating_reward.rating = math.max(_arena_start_rating, _request.profile.rating.current - _get_rating);
                            }
                            else
                            {
                                _rating_reward.rating = 0;
                            }
                        }
                        _rating_reward.hero = _hero;
                        _rating_reward.isWinner = isWins;
                        var stars = 0;

                        switch (_request.side)
                        {
                            case BattlePlayerSide.Left:
                                stars = _rating_reward.result.left.takestars;
                                _rating_reward.startInBattle = _rating_reward.result.left.takestars;

                                break;
                            case BattlePlayerSide.Right:
                                stars = _rating_reward.result.right.takestars;
                                _rating_reward.startInBattle = _rating_reward.result.right.takestars;
                                break;
                        }

                        // database update
                        //DatabaseRatingReward(_collection, _request.player, _rating_reward);

                        // SOFT
                        var newEnt = EntityManager.CreateEntity();
                        var newComponent = new ObserverPlayerCurrencyChangeEventInfo
                        {
                            player_index = _request.player,
                            difference = _rating_reward.soft,
                            source_id = _request.battlefied,
                            currencyType = CurrencyType.Soft,
                            changeSourceType = CurrencyChangeSourceType.Battle
                        };
                        EntityManager.AddComponentData(newEnt, newComponent);

                        // profile update
                        _playerProfile.rating.current = _rating_reward.rating;
                        _playerProfile.currency.soft += _rating_reward.soft;
                        //_playerProfile.heroes[_playerProfile.SelectedHero].exp = _rating_reward.hero.exp;

                        #region battlestatistic
                        _playerProfile.battleStatistic.AddBattle(); // Add Battle
                        if (isWins)
                        {
                            _playerProfile.battleStatistic.AddWins();  // add wins 
                        }
                        else
                        {
                            _playerProfile.battleStatistic.AddDefeats();
                        }

                        #endregion battlestatistic

                        _playerProfile.battlePass.stars += stars;

                        if (_rating_reward.lootbox.index > 0)
                        {
                            _playerProfile.loots.boxes[_rating_reward.lootbox.slot].index = _rating_reward.lootbox.index;
                            _playerProfile.loots.boxes[_rating_reward.lootbox.slot].battlefield = _rating_reward.lootbox.battlefield;
                        }

                        // session send message
                        if (_auth_system.Sessions.TryGetValue(_request.player, out Entity connect))
                        {
                            var _message = default(NetworkMessageRaw);
                            _message.Write((byte)ObserverPlayerMessage.BattleRatingResult);
                            _rating_reward.Serialize(ref _message);

                            var _player = EntityManager.GetComponentData<ObserverPlayerClient>(connect);
                            if (_player.connection.IsCreated)
                            {
                                _player.status = ObserverPlayerStatus.Authorized;
                                PostUpdateCommands.SetComponent(connect, _player);

                                _message.Send(
                                    _player_system.Driver,
                                    _player_system.ReliablePeline,
                                    _player.connection
                                );
                            }
                        }
                    }
                    var updater = _playerProfile.GetDBUpdater() as DBUpdater<PlayerProfileInstance>;
                    updater.Update();
                }
            }

            _entities.Dispose();
            _requests.Dispose();

            EntityManager.DestroyEntity(_query_results);
        }

        PlayerProfileInstance GetProfile(uint index)
        {
            PlayerProfileInstance _playerProfile = null;
            if (_auth_system.Profiles.TryGetValue(index, out PlayerProfileInstance profile))
            {
                _playerProfile = profile;
            }
            else
            {
                //UnityEngine.Debug.Log($"CantGetProfile in RatingResultSystem. Player index: {index}");
            }
            return _playerProfile;
        }

        // private async void DatabaseRatingReward(
        // 	IMongoCollection<PlayerProfileInstance> collection,
        // 	uint index,
        // 	BattleRatingResultReward reward
        // )
        // {
        // 	var _filter = new BsonDocument("_id", new BsonDocument("$eq", index));
        // 	var _options = new FindOneAndUpdateOptions<PlayerProfileInstance> { IsUpsert = false };
        //
        //           var _update = new UpdateDefinitionBuilder<PlayerProfileInstance>()
        //               .Set(n => n.rating.current, reward.rating)
        //               .Set(n => n.rating.max, reward.maxRating)
        //               .Inc(n => n.currency.soft, reward.soft)
        //               .Set($"heroes.{reward.hero.index}.exp", reward.hero.exp)
        //               .Set(n => n.session.time, System.DateTime.UtcNow)
        //               .Set(n => n.session.port, 0);
        //
        //           if (reward.lootbox.index > 0)
        // 	{
        //               _update = _update.Set($"loots.boxes.{reward.lootbox.slot}", new PlayerProfileLootBox
        //               {
        //                   index = reward.lootbox.index,
        //                   battlefield = reward.lootbox.battlefield
        //               });
        // 	}
        //
        // 	UnityEngine.Debug.Log("_reward: " + reward);
        //
        // 	await collection.FindOneAndUpdateAsync(_filter, _update, _options);
        // }

    }
}


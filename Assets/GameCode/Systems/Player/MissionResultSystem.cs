using Unity.Entities;
using Unity.Collections;
using Legacy.Database;
using Unity.Mathematics;
using System;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class MissionResultSystem : ComponentSystem
    {

        private EntityQuery _query_results;
        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
        {
            _query_results = GetEntityQuery(
                ComponentType.ReadOnly<ObserverBattleMissionResult>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
            _player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();

            RequireForUpdate(_query_results);
        }

        float _get_arena_percent(ushort index, float percent)
        {
            var _arena_settings = Database.Settings.Instance.Get<ArenaSettings>();

            float _arena_percent = 0;
            for (byte i = 0; i < _arena_settings.queue.length; ++i)
            {
                if (_arena_settings.queue[i] == index)
                {
                    return _arena_percent;
                }
                _arena_percent += percent;
            }

            return 0f;
        }

        protected override void OnUpdate()
        {
            var _requests = _query_results.ToComponentDataArray<ObserverBattleMissionResult>(Allocator.Temp);
            //var _collection = _auth_system.Database.GetCollection<PlayerProfileInstance>("users");
            var _entities = _query_results.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < _requests.Length; ++i)
            {
                var _request = _requests[i];

                if (Battlefields.Instance.Get(_request.battlefied, out BinaryBattlefields battlefield))
                {
                    if (Missions.Instance.Get(_request.mission, out BinaryMission mission))
                    {
                        // build reward
                        var _loots_settings = Database.Settings.Instance.Get<LootSettings>();

                        var _hero = _request.profile.hero;

                        var _mission_reward = default(BattleRatingResultReward);

                        // hero. За все миссии мы даем 20 опыта. 
                        //_hero.exp = math.min(_hero.exp + 20, _max_experience);

                        byte stars = 0;
                        //reward
                        if (_request.winnerSide == BattlePlayerSide.Left)
                        {
                            if (_request.tutorail == 4)
                            {
                                _mission_reward.rating = 26;
                                _mission_reward.maxRating = 26;
                            }

                            if (Rewards.Instance.Get(mission.reward, out BinaryReward reward))
                            {
                                // lootbox
                                if (reward.lootbox > 0 && _request.profile.freeslot < _loots_settings.slots)
                                {
                                    _mission_reward.lootbox = new BattleRatingResultRewardBox
                                    {
                                        slot = _request.profile.freeslot,
                                        index = reward.lootbox,
                                        battlefield = _request.battlefied
                                    };
                                }
                            }
                        }
                        _mission_reward.result = _request.result;
                        // TODO: soft count (reset every day)
                        if (_request.winnerSide == BattlePlayerSide.Left)
                            _mission_reward.soft = battlefield.rating_rules.victory.soft.amount;
                        else
                            _mission_reward.soft = battlefield.rating_rules.lose.soft.amount;

                        _mission_reward.hero = _hero;

                        bool isWins = false;
                        var _winner_side = (BattlePlayerSide)_request.result.winner;
                        if (_winner_side == BattlePlayerSide.Left)
                        {
                            isWins = true;
                        }
                        _mission_reward.isWinner = isWins;
                        // database update
                        //DatabaseRatingReward(_collection, _request.player, _mission_reward);

                        // profile update
                        if (_auth_system.Profiles.TryGetValue(_request.player, out PlayerProfileInstance profile))
                        {
                            // SOFT
                            var newEntity = EntityManager.CreateEntity();
                            var newCompon = new ObserverPlayerCurrencyChangeEventInfo
                            {
                                player_index = _request.player,
                                difference = _mission_reward.soft,
                                source_id = mission.index,
                                currencyType = CurrencyType.Soft,
                                changeSourceType = CurrencyChangeSourceType.MissionReward
                            };
                            EntityManager.AddComponentData(newEntity, newCompon);
                            profile.session.port = 0;

                            if (_request.tutorail <= 4)
                            {
                                if (_request.winnerSide == BattlePlayerSide.Left)//if victory
                                {
                                    profile.tutorial.hard_tutorial_state++;
                                }
                                else if (_request.tutorail == 4)
                                {
                                    profile.battleStatistic.AddDefaultTutor4();
                                }
                             }
                            profile.currency.soft += _mission_reward.soft;
                            //profile.heroes[profile.SelectedHero].exp = _mission_reward.hero.exp;
                            profile.session.time = System.DateTime.UtcNow;
                            profile.rating.current = _mission_reward.rating;
                            profile.rating.max = _mission_reward.maxRating;

                            stars = _mission_reward.result.left.takestars;
                            _mission_reward.startInBattle = stars;

                            if (_mission_reward.lootbox.index > 0)
                            {
                                profile.loots.boxes[_mission_reward.lootbox.slot].index = _mission_reward.lootbox.index;
                                profile.loots.boxes[_mission_reward.lootbox.slot].battlefield = _mission_reward.lootbox.battlefield;
                                if (Loots.Instance.Get(_mission_reward.lootbox.index, out BinaryLoot loot_box_binary))
                                {
                                    if(loot_box_binary.time < 1)
                                    {
                                        profile.loots.boxes[_mission_reward.lootbox.slot].started = true;
                                        profile.loots.boxes[_mission_reward.lootbox.slot].timer = DateTime.Now;
                                    }
                                }
                                
                            }

                            if (_request.tutorail == 4)
                            {
                                // Для активации буста открытия сундуков при переходе с туториальной арены на обычную арену (experemental)
                                profile.arenaBoosterTime.ApplyNewTime();
                            }

                            var updater = profile.GetDBUpdater() as DBUpdater<PlayerProfileInstance>;
                            updater.Update();
                        }

                        // session send message
                        if (_auth_system.Sessions.TryGetValue(_request.player, out Entity connect))
                        {
                            EntityManager.RemoveComponent<ObserverPlayerInBattle>(connect);
                            var _message = default(NetworkMessageRaw);
                            _message.Write((byte)ObserverPlayerMessage.BattleMissionResult);
                            _mission_reward.Serialize(ref _message);

                            var _player = EntityManager.GetComponentData<ObserverPlayerClient>(connect);
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
 
                EntityManager.DestroyEntity(_entities[i]);

            }

            _entities.Dispose();
            _requests.Dispose();

        }

        // private async void DatabaseRatingReward(
        //     IMongoCollection<PlayerProfileInstance> collection,
        //     uint index,
        //     BattleRatingResultReward reward
        // )
        // {
        //     var _filter = new BsonDocument("_id", new BsonDocument("$eq", index));
        //     var _options = new FindOneAndUpdateOptions<PlayerProfileInstance> { IsUpsert = false };
        //
        //     var _update = new UpdateDefinitionBuilder<PlayerProfileInstance>()
        //         //.Set(n => n.rating.current, reward.rating)
        //         //.Set(n => n.rating.max, reward.maxRating)
        //         .Inc(n => n.currency.soft, reward.soft)
        //         .Set($"heroes.{reward.hero.index}.exp", reward.hero.exp)
        //         .Set(n => n.session.time, System.DateTime.UtcNow);
        //
        //     if (reward.lootbox.index > 0)
        //     {
        //         _update = _update.Set($"loots.boxes.{reward.lootbox.slot}", new PlayerProfileLootBox
        //         {
        //             index = reward.lootbox.index,
        //             battlefield = reward.lootbox.battlefield
        //         });
        //     }
        //
        //     UnityEngine.Debug.Log("_reward: " + reward);
        //
        //     await collection.FindOneAndUpdateAsync(_filter, _update, _options);
        // }

    }
}


using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using Legacy.Database;
using System.Collections.Generic;
using System;
using System.Linq;
using static Legacy.Observer.MatchmakingAuthorizedSystem;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(MatchmakingSystems))]
    [UpdateAfter(typeof(MatchmakingDisposeSystem))]

    public class MatchmakingAuthorizedSystem : ComponentSystem
    {
        public struct _attach_info
        {
            public MatchmakingType type;
            public Entity entity;
            public Entity group;
            public uint index;
            public uint rating;
            public float cardsavarage;
            public int win_lose_rate;
            public int hero_lvl;
        }

        struct _battle_info
        {
            public MatchmakingType type;
            public uint rating;
        }

        private AuthorizationSystem _auth_system;

        private EntityQuery _query_battles;
        private EntityQuery _query_authorized;

        private NativeList<_attach_info> _attach;
        private NativeHashMap<Entity, _battle_info> _battles;
        private NativeQueue<_attach_info> _create;
        private NativeQueue<_attach_info> _result;

        protected override void OnCreate()
        {
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_battles = GetEntityQuery(
                ComponentType.ReadOnly<ObserverBattle>(),
                ComponentType.Exclude<ObserverBattleServerRequest>()
            );

            _query_authorized = GetEntityQuery(
                ComponentType.ReadOnly<ObserverPlayerClient>(),
                ComponentType.ReadOnly<ObserverPlayerAuthorized>(),
                ComponentType.ReadOnly<MatchmakingRequest>(),
                ComponentType.Exclude<ObserverPlayerInBattle>()
            );

            _create = new NativeQueue<_attach_info>(Allocator.Persistent);
            _result = new NativeQueue<_attach_info>(Allocator.Persistent);
            _attach = new NativeList<_attach_info>(Allocator.Persistent);
            _battles = new NativeHashMap<Entity, _battle_info>(512, Allocator.Persistent);

            RequireForUpdate(_query_authorized);
        }

        protected override void OnDestroy()
        {
            _create.Dispose();
            _attach.Dispose();
            _battles.Dispose();
            _result.Dispose();
        }

        protected override void OnUpdate()
        {
            _battles.Clear();
            Unity.Mathematics.Random _random = new Unity.Mathematics.Random(NetworkSystems.RandomInt);

            MatchmakingSettings Matchmaking_settings = Settings.Instance.Get<MatchmakingSettings>();

            if (!_query_battles.IsEmptyIgnoreFilter)
            {
                var _battle_job = new CollectBattlesJob
                {
                    battles = _battles.AsParallelWriter()
                }.Schedule(_query_battles);

                _battle_job.Complete();
            }

            UnityEngine.Debug.Log("[Observer] >> MatchmakingAuthorizedSystem");

            var _collect_job = new CreateRequestJob
            {
                create = _create.AsParallelWriter(),
                attach = _attach.AsParallelWriter(),
                battles = _battles
            }.Schedule(_query_authorized);

            _collect_job.Complete();

            // create new battle groups
            while (_create.TryDequeue(out _attach_info info))
            {
                UnityEngine.Debug.Log("[Observer] >> Matchmaking Create Request");
                UnityEngine.Debug.Log("[Observer] >> Matchmaking Create Request  info.type" + info.type);
                PostUpdateCommands.RemoveComponent<MatchmakingRequest>(info.entity);


                if (_auth_system.Profiles.TryGetValue(info.index, out PlayerProfileInstance profile))
                {
                    var _group = new ObserverBattleGroup
                    {
                        current = 1,
                        need = 1,
                        type = info.type,

                        rating = profile.rating.current
                    };
                    switch (info.type)
                    {
                        case MatchmakingType.BattlePvP1x1:
                            _group.need = 2;
                            break;
                        case MatchmakingType.BattleBotxBot:
                            _group.need = 2;
                            break;
                    }

                    var _battle_entity = EntityManager.CreateEntity();
                    var _profile_hero = profile.heroes[profile.SelectedHero];





                    var bot_add_time = info.type == MatchmakingType.BattleBotxBot ? (byte)0 : Matchmaking_settings.bot_waiting_time.Get(_random);
                    #region skills
                    ushort[] hero_skills = Heroes.Instance.GetSkills(profile.SelectedHero);
                    var _skills = new List<BattlePlayerProfileHeroSkill>();
                    for (byte i = 0; i < hero_skills.Length; i++)
                    {
                        _skills.Add(new BattlePlayerProfileHeroSkill
                        {
                            index = hero_skills[i],
                            level = _profile_hero.level
                        });
                    }
                    #endregion

                    EntityManager.AddComponentData(_battle_entity, new ObserverBattle
                    {
                        group = _group,
                        expire = NetworkSystems.ElapsedMilliseconds + bot_add_time * 1000,
                        player1 = new ObserverBattlePlayer
                        {
                            playerID = info.index,
                            profile = new BattlePlayerProfile
                            {

                                avarageCardsLevel = CountAvarageCardsLevel(profile),
                                win_lose_rate = profile.battleStatistic.WinLoseRate,
                                freeslot = profile.loots.GetFreeIndex,
                                name = profile.name,
                                hero = new BattlePlayerProfileHero
                                {
                                    index = profile.SelectedHero,
                                    //exp = _profile_hero.exp,
                                    level = _profile_hero.level,
                                    skill1 = _skills.Count > 0 ? _skills[0] : default,
                                    skill2 = _skills.Count > 1 ? _skills[1] : default
                                },
                                rating = new BattlePlayerRating
                                {
                                    current = profile.rating.current,
                                    max = profile.rating.max
                                }
                            },
                            deck = BattlePlayerDeck.Shuffle(profile.SelectedDeck, _random)
                        }
                    });
                }
            }


            if (!_query_battles.IsEmptyIgnoreFilter)
            {
                var _battle_job = new FilterBattlesJob
                {
                    attach = _attach,
                    result = _result.AsParallelWriter()
                }.Schedule(_query_battles);

                _battle_job.Complete();
            }


            while (_result.TryDequeue(out _attach_info info))
            {
                UnityEngine.Debug.Log("[Observer] >> Matchmaking Attach Request");

                PostUpdateCommands.RemoveComponent<MatchmakingRequest>(info.entity);
                if (_auth_system.Profiles.TryGetValue(info.index, out PlayerProfileInstance profile))
                {

                    var _battle = EntityManager.GetComponentData<ObserverBattle>(info.group);
                    var _profile_hero = profile.heroes[profile.SelectedHero];

                    #region skills
                    var _skills = new List<BattlePlayerProfileHeroSkill>();
                    var heroSkills = Heroes.Instance.GetSkills(profile.SelectedHero);
                    for (byte i = 0; i < heroSkills.Length; i++)
                    {
                        _skills.Add(new BattlePlayerProfileHeroSkill
                        {
                            index = heroSkills[i],
                            level = _profile_hero.level
                        });
                    }
                    #endregion

                    _battle[_battle.group.current] = new ObserverBattlePlayer
                    {
                        playerID = info.index,
                        profile = new BattlePlayerProfile
                        {
                            win_lose_rate = profile.battleStatistic.WinLoseRate,
                            avarageCardsLevel = CountAvarageCardsLevel(profile),
                            freeslot = profile.loots.GetFreeIndex,
                            name = profile.name,
                            hero = new BattlePlayerProfileHero
                            {
                                index = profile.SelectedHero,
                                //exp = _profile_hero.exp,
                                level = _profile_hero.level,
                                skill1 = _skills.Count > 0 ? _skills[0] : default,
                                skill2 = _skills.Count > 1 ? _skills[1] : default
                            },
                            rating = new BattlePlayerRating
                            {
                                current = profile.rating.current,
                                max = profile.rating.max
                            }
                        },
                        deck = BattlePlayerDeck.Shuffle(profile.SelectedDeck, _random)
                    };
                    _battle.group.current++;
                    EntityManager.SetComponentData(info.group, _battle);

                    if (_battle.group.IsComplete)
                    {
                        EntityManager.AddComponentData(info.group, default(ObserverBattleServerRequest));
                    }
                }
            }
        }

        private static float CountAvarageCardsLevel(PlayerProfileInstance profile)
        {
            var playerProfileDeck = profile.sets[profile.config.deck];
            var playerProfileCards = profile.cards;
            var avarage = 0;
            for (int j = 0; j < playerProfileDeck.list.Count; j++)
            {
                var playerCard = playerProfileCards[playerProfileDeck.list[j]];
                //    Cards.Instance.Get(playerProfileDeck.list[j], out BinaryCard binaryCard);//to count rarity
                avarage += playerCard.level;
            }

            var myCardsAvarage = 1.0f * avarage / playerProfileDeck.list.Count;
            return myCardsAvarage;
        }


        //    [Unity.Burst.BurstCompile]
        struct CreateRequestJob : IJobForEachWithEntity<ObserverPlayerAuthorized, MatchmakingRequest>
        {
            public NativeQueue<_attach_info>.ParallelWriter create;
            public NativeList<_attach_info>.ParallelWriter attach;
            [ReadOnly] public NativeHashMap<Entity, _battle_info> battles;

            public void Execute(
                Entity entity,
                int index,
                [ReadOnly] ref ObserverPlayerAuthorized auth,
                [ReadOnly] ref MatchmakingRequest request
            )
            {
                if (battles.Count() > 0)
                {
                    var _rating_list = battles.GetValueArray(Allocator.Temp);
                    var _battle_entities = battles.GetKeyArray(Allocator.Temp);
                    var _group_entity = Entity.Null;
                    var _closest_rating = uint.MaxValue;
                    for (int i = 0; i < _rating_list.Length; ++i)
                    {
                        var _battle_info = _rating_list[i];
                        if (_battle_info.type == request.type)
                        {
                            var _abs_rating = System.Convert.ToUInt32(math.abs((int)_battle_info.rating - (int)request.rating));
                            if (_abs_rating < _closest_rating)
                            {
                                _closest_rating = _abs_rating;
                                _group_entity = _battle_entities[i];
                            }
                        }
                    }
                    attach.AddNoResize(new _attach_info
                    {
                        index = auth.index,
                        entity = entity,
                        rating = request.rating,
                        group = _group_entity,
                        cardsavarage = request.avarage_cards,
                        win_lose_rate = request.winLoseRate,
                        type = request.type,
                        hero_lvl = request.hero_lvl
                    });
                }

                create.Enqueue(new _attach_info
                {
                    index = auth.index,
                    entity = entity,
                    type = request.type
                });
            }
        }

        [Unity.Burst.BurstCompile]
        struct CollectBattlesJob : IJobForEachWithEntity<ObserverBattle>
        {
            public NativeHashMap<Entity, _battle_info>.ParallelWriter battles;

            public void Execute(Entity entity, int index, [ReadOnly] ref ObserverBattle battle)
            {
                battles.TryAdd(entity, new _battle_info
                {
                    rating = battle.group.rating,
                    type = battle.group.type
                });
            }
        }

        struct FilterBattlesJob : IJobForEachWithEntity<ObserverBattle>
        {
            [ReadOnly] public NativeList<_attach_info> attach;
            public NativeQueue<_attach_info>.ParallelWriter result;

            public void Execute(Entity entity, int index, [ReadOnly] ref ObserverBattle battle)
            {
                var currentMyRating = battle.player1.profile.rating.current;
                var currentmYWinLoseRate = battle.player1.profile.win_lose_rate;
                var playerId = battle.player1.playerID;

                //my rating
                var minmaxRating = Settings.Instance.Get<MatchmakingSettings>().GetMinMaxRatingRange((int)currentMyRating, currentmYWinLoseRate);
                UnityEngine.Debug.Log($"min available rating = {minmaxRating.Item1} || max available rating = {minmaxRating.Item2}");

                //my avarage
                float myCardsAvarage = battle.player1.profile.avarageCardsLevel;
                UnityEngine.Debug.Log("myCardsAvarage = " + myCardsAvarage);

                //my lose win rate
                var losewinrate = battle.player1.profile.win_lose_rate;
                //my delta avarage
                var deltaCardsAvarage = Settings.Instance.Get<MatchmakingSettings>().GetDeltaLvlCardsAvarageRatingRange(losewinrate);

                var deltaHeroLvl = Settings.Instance.Get<MatchmakingSettings>().GetDeltaHeroLvlgRange(losewinrate);


                //get others profiles in this rating

                NativeList<_attach_info> closest = new NativeList<_attach_info>(Allocator.Temp);

                foreach (var element in attach)//for
                {
                    if (element.index == playerId) continue;
                    if (element.rating >= minmaxRating.Item1 && element.rating <= minmaxRating.Item2)
                    {
                        closest.Add(element);
                    }
                }
                var profilesInAvailableRating = closest;

                UnityEngine.Debug.Log("profilesInAvailableRating count = " + profilesInAvailableRating.Length);

                var availableProfilesArray = profilesInAvailableRating.ToArray();
                var sortedProfile = availableProfilesArray
                    .Where(x => x.index != playerId && math.abs(math.abs(x.cardsavarage) - math.abs(myCardsAvarage)) <= deltaCardsAvarage)
                    .OrderBy((x) => math.abs(myCardsAvarage - x.cardsavarage))
                    .FirstOrDefault();

                UnityEngine.Debug.Log("sortedProfiles first id = " + sortedProfile.index);


                if (sortedProfile.index == default)
                {
                    sortedProfile = profilesInAvailableRating.ClosestTo(currentmYWinLoseRate, playerId);
                }
                //if (sortedProfile.index == default)
                //{
                //    sortedProfile = attach.ClosestTo(currentmYWinLoseRate, playerId);
                //}

                if (sortedProfile.index != default)
                {
                    //просто проверка 
                    float otherCardsAvarage = sortedProfile.cardsavarage;
                    UnityEngine.Debug.Log("otherCardsAvarage = " + otherCardsAvarage);
                    //

                    result.Enqueue(sortedProfile);
                }

                closest.Dispose();


            }
        }

    }
}

public static class ProfileCollectionExtention
{

    public static _attach_info ClosestTo(this NativeList<_attach_info> collection, int targetWinLoseRate, uint id)
    {
        _attach_info closest = default;
        var minDifference = int.MaxValue;
        foreach (var element in collection)
        {
            if (element.index == id) continue;
            var difference = Math.Abs((long)element.win_lose_rate - targetWinLoseRate);
            if (minDifference > difference)
            {
                minDifference = (int)difference;
                closest = element;
            }
        }

        return closest;
    }
    public static _attach_info SortByCardsAvarage(this NativeList<_attach_info> collection, float targetAvarage, float deltaAvarage, uint id)
    {
        var sortedCollection = collection
            .Where(x => x.index != id && math.abs(math.abs(x.cardsavarage) - math.abs(targetAvarage)) <= deltaAvarage)
            .OrderBy((x) => math.abs(targetAvarage - x.cardsavarage))
            .FirstOrDefault();

        return sortedCollection;
    }

}

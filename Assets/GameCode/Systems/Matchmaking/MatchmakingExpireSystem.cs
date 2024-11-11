using Legacy.Database;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(MatchmakingSystems))]
    [UpdateAfter(typeof(MatchmakingAuthorizedSystem))]

    public class MatchmakingExpireSystem : JobComponentSystem
    {
        private EntityQuery _query_battles;
        private BeginSimulationEntityCommandBufferSystem _barrier;

        protected override void OnCreate()
        {
            _query_battles = GetEntityQuery(
                ComponentType.ReadWrite<ObserverBattle>(),
                ComponentType.Exclude<ObserverBattleServerRequest>(),
                ComponentType.Exclude<ObserverBattleServerResponse>(),
                ComponentType.Exclude<ObserverBattleServerWaiting>()
            );
            _barrier = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            RequireForUpdate(_query_battles);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {

            inputDeps = new ExpireWaitingJob
            {
                buffer = _barrier.CreateCommandBuffer().AsParallelWriter(),
                time = NetworkSystems.ElapsedMilliseconds,
                random = NetworkSystems.RandomInt
            }.Schedule(_query_battles, inputDeps);

            _barrier.AddJobHandleForProducer(inputDeps);

            return inputDeps;
        }

        struct ExpireWaitingJob : IJobForEachWithEntity<ObserverBattle>
        {
            public long time;
            public uint random;
            public EntityCommandBuffer.ParallelWriter buffer;

            public void Execute(Entity entity, int index, ref ObserverBattle battle)
            {
                if (time > battle.expire)
                {
                    var _random = new Unity.Mathematics.Random(random);
                    switch (battle.group.type)
                    {
                        case MatchmakingType.BattlePvP1x1:
                            {
                                var _arena_settings = Database.Settings.Instance.Get<ArenaSettings>();
                                if (_arena_settings.RatingBattlefield(battle.group.rating, out BinaryBattlefields binary))
                                {
                                    if (binary.bots.Count > 0)
                                    {
                                        //uint maxLeftRating = 0;
                                        //BinaryBot bot = default;

                                        //for (int i = 0; i < binary.bots.Count; ++i)
                                        //{
                                        //    if (Bots.Instance.Get(binary.bots[i], out BinaryBot tempBot))
                                        //    {
                                        //        if (tempBot.rating <= battle.player1.profile.rating.current)
                                        //        {
                                        //            if (tempBot.rating >= maxLeftRating)
                                        //            {
                                        //                bot = tempBot;
                                        //                maxLeftRating = tempBot.rating;
                                        //            }
                                        //        }
                                        //    }
                                        //}
                                        BinaryBot bot = Bots.Instance.GetRandomBot(battle.player1.profile.rating.current, 3);
                                        Bots.Instance.RandomizeBotName(ref bot);

                                        //if (bot.index == 0)
                                        //    Bots.Instance.Get(binary.bots[0], out bot);

                                        if (bot.index > 0)
                                        {
                                            battle.player2 = new ObserverBattlePlayer
                                            {
                                                playerID = 0,
                                                deck = BattlePlayerDeck.Shuffle(bot.deck, _random),

                                                profile = new BattlePlayerProfile
                                                {
                                                    is_bot = true,
                                                    bot_disabled = bot.disabled_at_start,
                                                    bot_frequency = bot.brain_frequency,
                                                    canUseTankAndRange = bot.canUseTankRange,
                                                    name = bot.title,
                                                    rating = new BattlePlayerRating
                                                    {
                                                        current = (uint)Unity.Mathematics.math.floor(bot.rating + 0.1f * bot.rating * _random.NextFloat(-1, 1))
                                                    },
                                                    hero = new BattlePlayerProfileHero
                                                    {
                                                        index = bot.hero,
                                                        level = bot.hero_lvl
                                                    },
                                                }
                                            };
                                            battle.group.need = 2;
                                            battle.group.current = 2;

                                            buffer.AddComponent<ObserverBattleServerRequest>(index, entity);
                                            UnityEngine.Debug.Log($"Battle Expire Player:{battle.player1.playerID} >> add bot");
                                        }
                                    }
                                }
                                break;
                            }
                        case MatchmakingType.BattleBotxBot:
                            {
                                var listBots = new List<BinaryBot>();
                                ushort id_bot1 = 104;
                                ushort id_bot2 = 105;

                                if(Bots.Instance.Get(id_bot1, out BinaryBot bot1))
                                {
                                    listBots.Add(bot1);
                                }

                                if (Bots.Instance.Get(id_bot2, out BinaryBot bot2))
                                {
                                    listBots.Add(bot2);
                                }
                                if (listBots.Count > 1)
                                {
                                    battle.player1 = new ObserverBattlePlayer
                                    {
                                        playerID = 0,
                                        deck = BattlePlayerDeck.Shuffle(listBots[0].deck, _random),
                                        profile = new BattlePlayerProfile
                                        {
                                            is_bot = true,                                            
                                            bot_frequency = listBots[0].brain_frequency,
                                            canUseTankAndRange = listBots[0].canUseTankRange,
                                            name = listBots[0].title,
                                            rating = new BattlePlayerRating
                                            {
                                                current = (uint)Unity.Mathematics.math.floor(listBots[0].rating + 0.1f * listBots[0].rating * _random.NextFloat(-1, 1))
                                            },
                                            hero = new BattlePlayerProfileHero
                                            {
                                                index = listBots[0].hero,
                                                // TODO: level from BinaryHero
                                                level = 1
                                            }
                                        }
                                    };
                                    battle.player2 = new ObserverBattlePlayer
                                    {
                                        playerID = 0,
                                        deck = BattlePlayerDeck.Shuffle(listBots[1].deck, _random),

                                        profile = new BattlePlayerProfile
                                        {
                                            is_bot = true,
                                            bot_frequency = listBots[1].brain_frequency,
                                            canUseTankAndRange = listBots[1].canUseTankRange,
                                            name = listBots[1].title,
                                            rating = new BattlePlayerRating
                                            {
                                                current = (uint)Unity.Mathematics.math.floor(listBots[1].rating + 0.1f * listBots[1].rating * _random.NextFloat(-1, 1))
                                            },
                                            hero = new BattlePlayerProfileHero
                                            {
                                                index = listBots[1].hero,
                                                // TODO: level from BinaryHero
                                                level = 1
                                            },
                                        }
                                    };
                                    battle.group.need = 2;
                                    battle.group.current = 2;

                                    buffer.AddComponent<ObserverBattleServerRequest>(index, entity);
                                    UnityEngine.Debug.Log($"ADD 2 BOTS");
                                }

                                break;
                            }
                    }
                }
            }
        }
    }
}


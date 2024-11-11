using Legacy.Database;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

    public class SandboxSystem : ComponentSystem
    {
        private EntityQuery _query_requests;
        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
        {
            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<ObserverPlayerClient>(),
                ComponentType.ReadOnly<ObserverPlayerAuthorized>(),
                ComponentType.ReadOnly<BattleInstanceSandbox>()
            );
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
            _player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();
        }

        protected override void OnUpdate()
        {
            var _buffer = new EntityCommandBuffer(Allocator.TempJob);
            var _random = new Random(NetworkSystems.RandomInt);
            Entities
                .WithAllReadOnly<ObserverPlayerClient>()
                .WithAllReadOnly<ObserverPlayerAuthorized>()
                .WithAllReadOnly<BattleInstanceSandbox>()
            .ForEach((Entity entity, ref ObserverPlayerClient client, ref BattleInstanceSandbox battle_tutorial, ref ObserverPlayerAuthorized auth) =>
            {
                _buffer.RemoveComponent<BattleInstanceSandbox>(entity);

                if (_auth_system.Profiles.TryGetValue(auth.index, out PlayerProfileInstance profile))
                {
                    // player hero
                    var _player_hero = profile.SelectedHero;
                    var _player_deck = profile.SelectedDeck;

                    if (Bots.Instance.Get(40, out BinaryBot enemy_bot, false))
                    {
                 
                            var _entity = _buffer.CreateEntity();
                        // 1 player ??
                        UnityEngine.Debug.LogError("ObserverBattle is sandbox");
                            var _observer_battle = new ObserverBattle
                            {
                                isSandbox = 1,
                                campaign = new BattleInstanceCampaign { index = ushort.MaxValue, mission = 1, tutorialIndex = battle_tutorial.index },
                                group = new ObserverBattleGroup
                                {
                                    type = MatchmakingType.BattlePvE1x1,
                                    current = 2,
                                    need = 2
                                },
                                player1 = new ObserverBattlePlayer
                                {
                                    profile = new BattlePlayerProfile
                                    {
                                        freeslot = profile.loots.GetFreeIndex,
                                        hero = new BattlePlayerProfileHero
                                        {
                                            index = _player_hero,
                                            level = profile.heroes[profile.SelectedHero].level,
                                            //exp = profile.heroes[profile.SelectedHero].exp
                                        }
                                    },
                                    playerID = auth.index,
                                    deck = BattlePlayerDeck.PrepareDeck(_player_deck)
                                },
                                player2 = new ObserverBattlePlayer
                                {
                                    profile = new BattlePlayerProfile
                                    {
                                        bot_disabled = true,
                                        is_bot = true,
                                        bot_frequency = enemy_bot.brain_frequency,
                                        canUseTankAndRange = enemy_bot.canUseTankRange,
                                        hero = new BattlePlayerProfileHero
                                        {
                                            index = enemy_bot.hero,
                                            level = (byte)1
                                        },
                                        name = enemy_bot.title
                                    },
                                    playerID = 0,
                                    deck = BattlePlayerDeck.Shuffle(enemy_bot.deck, _random)
                                }
                            };
                            _buffer.AddComponent(_entity, _observer_battle);
                        _buffer.AddComponent<ObserverBattleServerRequest>(_entity);
                        var _message = default(NetworkMessageRaw);
                        _message.Write((byte)ObserverPlayerMessage.Sandbox);

                        _message.Send(
                            _player_system.Driver,
                            _player_system.ReliablePeline,
                            client.connection
                        );
                        return;
                    }
                }

                UnityEngine.Debug.Log("player session error");
            });

            _buffer.Playback(EntityManager);
            _buffer.Dispose();
        }

    }
}


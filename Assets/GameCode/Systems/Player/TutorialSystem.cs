using Legacy.Database;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

	public class TutorialSystem : ComponentSystem
	{
        private EntityQuery _query_requests;
        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
		{
            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<ObserverPlayerClient>(),
                ComponentType.ReadOnly<ObserverPlayerAuthorized>(),
                ComponentType.ReadOnly<BattleInstanceTutorial>()
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
				.WithAllReadOnly<BattleInstanceTutorial>()
			.ForEach((Entity entity, ref ObserverPlayerClient client, ref BattleInstanceTutorial battle_tutorial, ref ObserverPlayerAuthorized auth) => 
			{
				_buffer.RemoveComponent<BattleInstanceTutorial>(entity);

                if (_auth_system.Profiles.TryGetValue(auth.index, out PlayerProfileInstance profile))
                {
                    if (Tutorial.Instance.Get(battle_tutorial.index, out BinaryTutorial tutorial))
                    {
                        if (Missions.Instance.Get(tutorial.mission, out BinaryMission mission))
                        {
                            // player hero
                            var _player_hero = profile.SelectedHero;
                            var _player_deck = profile.SelectedDeck;

                            if (mission.player > 0)
                            {
                                if (Bots.Instance.Get(mission.player, out BinaryBot player_bot, false))
                                {
                                    _player_hero = player_bot.hero;

                                    for (int i = 0; i < player_bot.deck.Count; ++i)
                                    {
                                        for (int j = 0; j < _player_deck.Count; ++j)
                                        {
                                            if (player_bot.deck[i].index == _player_deck[j].index)
                                            {
                                                player_bot.deck[i] = new BinaryBattleCard 
                                                { 
                                                    index = player_bot.deck[i].index, 
                                                    level = _player_deck[j].level 
                                                };
                                                return;
                                            }
                                        }
                                    }

                                    _player_deck = player_bot.deck;
                                }
                            }

                            if (Bots.Instance.Get(mission.enemy, out BinaryBot enemy_bot, false))
                            {
                                var _entity = _buffer.CreateEntity(); 
                                // 1 player ??

                                var _observer_battle = new ObserverBattle
                                {
                                    campaign = new BattleInstanceCampaign { index = ushort.MaxValue,mission  = tutorial.mission ,tutorialIndex = battle_tutorial .index},
                                    tutorial = battle_tutorial,
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
                                            bot_disabled = enemy_bot.disabled_at_start,
                                            is_bot = true,
                                            bot_frequency = enemy_bot.brain_frequency,
                                            canUseTankAndRange = enemy_bot.canUseTankRange,
                                            hero = new BattlePlayerProfileHero
                                            {
                                                index = enemy_bot.hero,
                                                level = (byte)(tutorial.index == 3 ? 2 : 1)
                                            },
                                            name = enemy_bot.title
                                        },
                                        playerID = 0,
                                        deck = BattlePlayerDeck.Shuffle(enemy_bot.deck, _random)
                                    }
                                };
                                _buffer.AddComponent(_entity, _observer_battle);
                                _buffer.AddComponent<ObserverBattleServerRequest>(_entity);

                                //var _message = default(NetworkMessageRaw);
                                //_message.Write((byte)ObserverPlayerMessage.BattleReady);
                                //_observer_battle.Serialize(ref _message);

                                //_message.Send(
                                //    _player_system.Driver,
                                //    _player_system.ReliablePeline,
                                //    client.connection
                                //);

                                return;
                            }
                            UnityEngine.Debug.Log($"tutorial enemy:{mission.enemy} error");
                            return;
                        }
                        UnityEngine.Debug.Log($"mission:{tutorial.mission} error");
                        return;
                    }
                    UnityEngine.Debug.Log($"tutorial:{tutorial.mission} error");
                    return;
                }

                UnityEngine.Debug.Log("player session error");
			});

			_buffer.Playback(EntityManager);
			_buffer.Dispose();
		}

    }
}


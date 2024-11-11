using Legacy.Database;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]

	public class CampaignsSystem : ComponentSystem
	{
        private EntityQuery _query_requests;
        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
		{
            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<ObserverPlayerClient>(),
                ComponentType.ReadOnly<ObserverPlayerAuthorized>(),
                ComponentType.ReadOnly<BattleInstanceCampaign>()
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
				.WithAllReadOnly<BattleInstanceCampaign>()
			.ForEach((Entity entity, ref ObserverPlayerClient client, ref BattleInstanceCampaign campaign, ref ObserverPlayerAuthorized auth) => 
			{
				_buffer.RemoveComponent<BattleInstanceCampaign>(entity);

                if (_auth_system.Profiles.TryGetValue(auth.index, out PlayerProfileInstance profile))
                {
                    if (Missions.Instance.Get(campaign.mission, out BinaryMission binary))
                    {
                        // player hero
                        var _player_hero = profile.SelectedHero;
                        var _player_deck = profile.SelectedDeck;

                        if (binary.player > 0)
                        {
                            if (Bots.Instance.Get(binary.player, out BinaryBot player_bot))
                            {
                                _player_hero = player_bot.hero;
                                _player_deck = player_bot.deck;
                            }
                        }

                        if (Bots.Instance.Get(binary.enemy, out BinaryBot enemy_bot))
                        {
                            // 1 player ??
                            var _observer_battle = new ObserverBattle
                            {
                                campaign = campaign,
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
                                            index = _player_hero
                                        }
                                    },
                                    playerID = auth.index,
                                    deck = BattlePlayerDeck.Shuffle(_player_deck, _random)
                                },
                                player2 = new ObserverBattlePlayer
                                {
                                    profile = new BattlePlayerProfile
                                    {
                                        is_bot = true,
                                        hero = new BattlePlayerProfileHero
                                        {
                                            index = enemy_bot.hero
                                        }
                                    },
                                    playerID = 0,
                                    deck = BattlePlayerDeck.Shuffle(enemy_bot.deck, _random)
                                }
                            };

                            var _message = default(NetworkMessageRaw);
                            _message.Write((byte)ObserverPlayerMessage.Campaign);
                            _observer_battle.Serialize(ref _message);

                            _message.Send(
                                _player_system.Driver,
                                _player_system.ReliablePeline,
                                client.connection
                            );

                            return;
                        }
                        UnityEngine.Debug.Log($"campaign enemy:{binary.enemy} error");
                        return;
                    }
                    UnityEngine.Debug.Log($"mission:{campaign.mission} error");
                    return;
                }

                UnityEngine.Debug.Log("player session error");
			});

			_buffer.Playback(EntityManager);
			_buffer.Dispose();
		}

    }
}


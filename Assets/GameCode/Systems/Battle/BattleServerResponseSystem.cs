using Legacy.Database;
using Legacy.Network;
using MongoDB.Bson;
using MongoDB.Driver;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(BattleSystems))]

	public class BattleServerResponseSystem : ComponentSystem
	{

        struct _init_battle
        {
            public ObserverBattle battle;
            public Entity entity;
            public FixedString64 server;
            public ushort port;           
        }

		private EntityQuery _query_response;
		private EntityQuery _query_waiting;
        private NativeQueue<_init_battle> _battles;
        private ObserverPlayerSystem _player_system;
        private AuthorizationSystem _auth_system;

        protected override void OnCreate()
		{
			_query_response = GetEntityQuery(
				ComponentType.ReadOnly<ObserverBattleServerResponse>()
			);

			_query_waiting = GetEntityQuery(
				ComponentType.ReadOnly<ObserverBattle>(),
				ComponentType.ReadOnly<ObserverBattleServerWaiting>()
			);

            _battles = new NativeQueue<_init_battle>(Allocator.Persistent);
            _player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            RequireForUpdate(_query_response);
            RequireForUpdate(_query_waiting);
        }

        protected override void OnDestroy()
        {
            _battles.Dispose();
        }

        protected override void OnUpdate()
		{

            PostUpdateCommands.DestroyEntity(_query_response);

            var _response_job = new ReponseJob
            {
                response = _query_response.ToComponentDataArray<ObserverBattleServerResponse>(Allocator.TempJob),
                battles = _battles.AsParallelWriter()
            }.Schedule(_query_waiting);

            _response_job.Complete();

            if (_battles.Count > 0)
            {
                while (_battles.TryDequeue(out _init_battle info))
                {
                    if (info.battle.group.current > 0)
                    {
                        PostUpdateCommands.DestroyEntity(info.entity);
                        for (byte k = 0; k < info.battle.group.current; ++k)
                        {
                            var _player = info.battle[k];

                            if (_auth_system.Sessions.TryGetValue(_player.playerID, out Entity playerConnectEntity))
                            {
                                if (_auth_system.Profiles.TryGetValue(_player.playerID, out var playerProfile))
                                {
                                    playerProfile.session.port = info.port;
                                    playerProfile.session.server = info.server.ToString();
                                }
                                var _client = EntityManager.GetComponentData<ObserverPlayerClient>(playerConnectEntity);
                                _client.status = ObserverPlayerStatus.Playing;

                                ObserverPlayerInBattle inBattle = new ObserverPlayerInBattle
                                {
                                    expireTime = 6 * 60 * 1000,
                                    serverIP = new FixedString32(info.server),
                                    port = info.port
                                };
                                EntityManager.AddComponentData(playerConnectEntity, inBattle);

                                var _message = default(NetworkMessageRaw);
                                _message.Write((byte)ObserverPlayerMessage.BattleReady);
                                _message.Write(info.server);
                                _message.Write(info.port);

                                var enemiesLength = info.battle.group.current - 1;
                                _message.Write((byte)enemiesLength);
                                for(byte l = 0; l < info.battle.group.current; ++l)
                                {
                                    if (l == k) continue;
                                    info.battle[l].Serialize(ref _message);
                                }

                                _message.Send(
                                    _player_system.Driver,
                                    _player_system.ReliablePeline,
                                    _client.connection
                                );

                                EntityManager.SetComponentData(playerConnectEntity, _client);
                            }
                        }
                    }
                }
            }

        }

        [Unity.Burst.BurstCompile]
		struct ReponseJob : IJobForEachWithEntity<ObserverBattle, ObserverBattleServerWaiting>
		{
			[ReadOnly, DeallocateOnJobCompletion] public NativeArray<ObserverBattleServerResponse> response;
            public NativeQueue<_init_battle>.ParallelWriter battles;

            public void Execute(
				Entity entity, 
				int index, 
				[ReadOnly] ref ObserverBattle battle,
				[ReadOnly] ref ObserverBattleServerWaiting status
			)
			{
				for (int i = 0; i < response.Length; ++i)
				{
					var _response = response[i];
                    if (_response.connect != status.connect) continue;

                    battles.Enqueue(new _init_battle
                    {
                        battle = battle,
                        entity = entity,
                        port = _response.port,
                        server = _response.ip
                    });
                }
			}
		}

	}
}
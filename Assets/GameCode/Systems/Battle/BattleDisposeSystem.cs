using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;

using Legacy.Database;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(BattleSystems))]
	[UpdateAfter(typeof(BattleServerRequestSystem))]

	public class BattleDisposeSystem : JobComponentSystem
	{
		private EntityQuery _query_battles;

		private BeginInitializationEntityCommandBufferSystem _barrier;
		private ObserverPlayerSystem _player_system;
        private AuthorizationSystem _auth_system;

        protected override void OnCreate()
		{
			_query_battles = GetEntityQuery(
				ComponentType.ReadOnly<ObserverBattle>(),
				ComponentType.ReadOnly<BattleInstanceDestroy>()
			);

			_barrier = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
			_player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
        }

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			inputDeps = new DestroyBattleJob
			{
				buffer = _barrier.CreateCommandBuffer().AsParallelWriter(),
				driver = _player_system.DriverConcurrent,
				peline = _player_system.ReliablePeline,
				clients = GetComponentDataFromEntity<ObserverPlayerClient>(false),
                sessions = _auth_system.Sessions
			}.ScheduleSingle(_query_battles, inputDeps);


			return inputDeps;
		}

		[Unity.Burst.BurstCompile]
		struct DestroyBattleJob : IJobForEachWithEntity<ObserverBattle>
		{
			public EntityCommandBuffer.ParallelWriter buffer;
			public NetworkPipeline peline;
			public ComponentDataFromEntity<ObserverPlayerClient> clients;
			public NetworkDriver.Concurrent driver;
            [ReadOnly] public NativeHashMap<uint, Entity> sessions;

            public void Execute(
				Entity entity, 
				int index, 
				[ReadOnly] ref ObserverBattle battle
			)
			{
				buffer.DestroyEntity(index, entity);				

				for (byte i = 0; i < battle.group.current; ++i)
				{
					var _player = battle[i];
                    if (sessions.TryGetValue(_player.playerID, out Entity connect))
                    {
                        if (clients.HasComponent(connect))
                        {
							var _client = clients[connect];
							driver.BeginSend(peline, _client.connection, out DataStreamWriter _writer, 1);
							_writer.WriteByte((byte)ObserverPlayerMessage.MatchmakingError);							
                            driver.EndSend( _writer);
							_client.status = ObserverPlayerStatus.Authorized;
							clients[connect] = _client;
                        }
                    }
				}
			}
		}

	}
}


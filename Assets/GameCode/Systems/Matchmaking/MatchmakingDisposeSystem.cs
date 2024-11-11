using Legacy.Database;
using Legacy.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Legacy.Observer
{
	[UpdateInGroup(typeof(MatchmakingSystems))]
	
	public class MatchmakingDisposeSystem : JobComponentSystem
	{
		private EndSimulationEntityCommandBufferSystem _barrier;
        private EntityQuery _query_battles;
		private EntityQuery _query_events;
		private NativeHashMap<uint, bool> _disconnected;

		protected override void OnCreate()
		{
			_barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
			_query_battles = GetEntityQuery(
				ComponentType.ReadWrite<ObserverBattle>()
			);

			_query_events = GetEntityQuery(
				ComponentType.ReadOnly<ObserverEventDisconnect>()
			);

			_disconnected = new NativeHashMap<uint, bool>(256, Allocator.Persistent);

			RequireForUpdate(_query_events);
		}

		protected override void OnDestroy()
		{
			_disconnected.Dispose();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{

			_disconnected.Clear();

			inputDeps = new CollectJob
			{
				buffer = _barrier.CreateCommandBuffer().AsParallelWriter(),
				disconnected = _disconnected.AsParallelWriter()
			}.Schedule(_query_events, inputDeps);

			if (!_query_battles.IsEmptyIgnoreFilter)
			{
				inputDeps = new DisposeJob
				{
					buffer = _barrier.CreateCommandBuffer().AsParallelWriter(),
                    disconnected = _disconnected
				}.Schedule(_query_battles, inputDeps);
			}

			_barrier.AddJobHandleForProducer(inputDeps);

			return inputDeps;
		}

		[Unity.Burst.BurstCompile]
		struct CollectJob : IJobForEachWithEntity<ObserverEventDisconnect>
		{
			public EntityCommandBuffer.ParallelWriter buffer;
			public NativeHashMap<uint, bool>.ParallelWriter disconnected;
			public void Execute(Entity entity, int index, [ReadOnly] ref ObserverEventDisconnect disconnect)
			{
				disconnected.TryAdd(disconnect.index, disconnect.isPlayerCancel);
				buffer.DestroyEntity(index, entity);
			}
		}

		[Unity.Burst.BurstCompile]
		struct DisposeJob : IJobForEachWithEntity<ObserverBattle>
		{
			public EntityCommandBuffer.ParallelWriter buffer;
			[ReadOnly] public NativeHashMap<uint, bool> disconnected;

            public void Execute(
				Entity entity, 
				int index,
				ref ObserverBattle battle
			)
			{
				for (byte i = 0; i < battle.group.current; ++i)
				{
					var player = battle[i];
                    if (disconnected.TryGetValue(player.playerID, out bool cancel))
                    {
                        if (cancel)
                        {
                            battle.group.current--;
                            battle[i] = battle[battle.group.current];
                        }
                    }
                }

				if (battle.group.IsEmpty)
				{
					buffer.DestroyEntity(index, entity);
				}
				
			}
		}

	}
}


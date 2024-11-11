using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Legacy.Database;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(BattleSystems))]

	public class BattleServerWaitingSystem : JobComponentSystem
	{
		private EntityQuery _query_waiting;
		private BeginInitializationEntityCommandBufferSystem _barrier;

		protected override void OnCreate()
		{
			_query_waiting = GetEntityQuery(
				ComponentType.ReadOnly<ObserverBattle>(),
				ComponentType.ReadOnly<ObserverBattleServerWaiting>()
			);

			_barrier = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

			RequireForUpdate(_query_waiting);
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			inputDeps = new DenyJob
			{
				buffer = _barrier.CreateCommandBuffer().AsParallelWriter(),
				mask = GetComponentDataFromEntity<ObserverGameClient>(true)
			}.Schedule(_query_waiting, inputDeps);

			_barrier.AddJobHandleForProducer(inputDeps);

			return inputDeps;
		}

		struct DenyJob : IJobForEachWithEntity<ObserverBattleServerWaiting>
		{
			internal EntityCommandBuffer.ParallelWriter buffer;
			[ReadOnly] internal ComponentDataFromEntity<ObserverGameClient> mask;

			public void Execute(Entity entity, int index, [ReadOnly] ref ObserverBattleServerWaiting status)
			{
				if (!mask.HasComponent(status.connect))
				{
					buffer.RemoveComponent<ObserverBattleServerWaiting>(index, entity);
					//buffer.AddComponent<ObserverBattleServerRequest>(index, entity);
				}
			}
		}

	}
}
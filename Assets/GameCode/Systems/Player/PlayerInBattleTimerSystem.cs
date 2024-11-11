using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

namespace Legacy.Observer
{
	[UpdateInGroup(typeof(NetworkSystems))]

	public class PlayerInBattleTimerSystem : JobComponentSystem
	{
		private EndSimulationEntityCommandBufferSystem _barrier;

		protected override void OnCreate()
		{
			_barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
            ushort deltaTime = NetworkSystems._delta_time;
            EntityCommandBuffer.ParallelWriter buffer = _barrier.CreateCommandBuffer().AsParallelWriter();

            inputDeps = Entities
                .WithAll<ObserverPlayerClient, ObserverPlayerInBattle>()
                .ForEach(
                (
                    Entity entity, 
                    int entityInQueryIndex,
                    ref ObserverPlayerInBattle playerInBattle
                ) =>
                {
                    playerInBattle.expireTime -= deltaTime;

                    if (playerInBattle.expireTime < deltaTime)
                    {
                        buffer.RemoveComponent<ObserverPlayerInBattle>(entityInQueryIndex, entity);
                    }
                }).Schedule(inputDeps);

			return inputDeps;
		}
	}
}
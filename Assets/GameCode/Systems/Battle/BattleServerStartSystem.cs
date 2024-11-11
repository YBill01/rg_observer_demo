using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Legacy.Database;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(BattleSystems))]

    public class BattleServerStartSystem : SystemBase
    {
        private EntityQuery _query_battles;

        private BeginInitializationEntityCommandBufferSystem _barrier;

        protected override void OnCreate()
        {
            _query_battles = GetEntityQuery(
                ComponentType.ReadOnly<ObserverBattle>(),
                ComponentType.ReadOnly<ObserverBattleServerRequest>()
            );

            _barrier = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

            RequireForUpdate(_query_battles);
        }

        protected override void OnUpdate()
        {
            var driver = ServerConnection.Instance.Driver;
            var pipeline = ServerConnection.Instance.ReliablePipeline;
            var buffer = _barrier.CreateCommandBuffer();


            Entities
                .WithStructuralChanges()
                .WithNone<ObserverBattleServerStarted>()
               .ForEach(
           (
               Entity entity,
               ref ObserverBattle battle,
               in ObserverBattleServerRequest request
           ) =>
           {
#if UNITY_EDITOR

#else
               ServerConnection.Instance.StartNewServer();
#endif

               buffer.AddComponent(entity, new ObserverBattleServerStarted { });

           }).WithoutBurst().Run();

        }
    }
}
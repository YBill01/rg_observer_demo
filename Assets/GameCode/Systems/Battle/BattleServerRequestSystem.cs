using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Legacy.Database;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(BattleSystems))]

    public class BattleServerRequestSystem : SystemBase
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
            var battle_entities = _query_battles.ToEntityArray(Allocator.TempJob);
            var battles = _query_battles.ToComponentDataArray<ObserverBattle>(Allocator.TempJob);
            var driver = ServerConnection.Instance.Driver;
            var pipeline = ServerConnection.Instance.ReliablePipeline;
            var buffer = _barrier.CreateCommandBuffer();


            Entities
               .WithAll<ObserverGameClientReadyTag>()
               .WithNone<ObserverGameDisconnectRequest, ObserverGameDisconnect, ObserverBattleServerPlayers>()
               .WithDisposeOnCompletion(battle_entities)
               .WithDisposeOnCompletion(battles)
               .ForEach(
           (
               Entity entity,
               ref ObserverGameClientStats stats,
               in ObserverGameClient server
           ) =>
           {
               for (int i = 0; i < battles.Length; ++i)
               {
                   var _battle = battles[i];


                   var _message = default(NetworkMessageRaw);
                   _message.Write((byte)ObserverGameMessage.Players);
                   _battle.Serialize(ref _message);
                   _message.Send(driver, pipeline, server.connection);

                   // add waiting tag
                   buffer.RemoveComponent<ObserverBattleServerRequest>(battle_entities[i]);
                   buffer.AddComponent(battle_entities[i], new ObserverBattleServerWaiting
                   {
                       connect = entity,
                   });
                   buffer.AddComponent(entity, new ObserverBattleServerPlayers { });

               }
           }).WithoutBurst().Run();
        }
    }
}
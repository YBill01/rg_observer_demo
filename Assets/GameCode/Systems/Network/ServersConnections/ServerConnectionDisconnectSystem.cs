using Legacy.Database;
using System;

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;


/// <summary>
/// Система дисконнектит сервера.
/// Работает в основном потоке. Соединение разорвать можно только в основном потоке.
/// </summary>
namespace Legacy.Observer
{
    [UpdateInGroup(typeof(NetworkSystems))]
    [UpdateAfter(typeof(ServerConnectionReceiveSystem))]

    public class ServerConnectionDisconnectSystem : ComponentSystem
    {
        EntityQuery _query_disconnect;

        protected override void OnCreate()
        {
            _query_disconnect = GetEntityQuery(
                    ComponentType.ReadOnly<ObserverGameClient>(),
                    ComponentType.ReadOnly<ObserverGameDisconnectRequest>()
                );
            RequireForUpdate(_query_disconnect);
        }

        protected override void OnUpdate()
        {
            var entities = _query_disconnect.ToEntityArray(Allocator.Temp);
            var clients = _query_disconnect.ToComponentDataArray<ObserverGameClient>(Allocator.Temp);
            for(int i = 0; i < entities.Length; i++)
            {
                ServerConnection.Instance.Driver.Disconnect(clients[i].connection);
                EntityManager.DestroyEntity(entities[i]);
            }
            entities.Dispose();
            clients.Dispose();
        }
    }
}


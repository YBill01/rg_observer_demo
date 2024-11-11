using Legacy.Database;
using System;

using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;


/// <summary>
/// Система просто принимает новый коннект от сервера к обсерверу
/// создает сущность с коннектом и статами коннекта (порт и тд)
/// </summary>
namespace Legacy.Observer
{
    [UpdateInGroup(typeof(NetworkSystems))]

    public class ServerConnectionAcceptSystem : ComponentSystem
    {
        private EndInitializationEntityCommandBufferSystem _barrier;
        protected override void OnCreate()
        {            
            _barrier = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            //#if UNITY_EDITOR
            //            ServerConnection.Instance.StartFromEditor();
            //#else
            //            ServerConnection.Instance.StartNewServer();
            //#endif
            ServerConnection.Instance.Create();
            RequireSingletonForUpdate<ServerNetworkDriver>();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer buffer = _barrier.CreateCommandBuffer();
            NetworkDriver driver = ServerConnection.Instance.Driver;
            driver.ScheduleUpdate().Complete();

            NetworkConnection _connect;

            while ((_connect = driver.Accept()) != default)
            {
                if (_connect.PopEvent(driver, out DataStreamReader reader) != NetworkEvent.Type.Empty)
                {
                    _connect.Disconnect(driver);
                    continue;
                }

                UnityEngine.Debug.Log("Server Connection: " + _connect.InternalId);

                var _entity = buffer.CreateEntity();
                buffer.AddComponent(_entity, new ObserverGameClient
                {
                    connection = _connect,
                    status = ObserverGameStatus.Connected
                });
                buffer.AddComponent(_entity, new ObserverGameClientStats
                {
                    port = 0
                });
            }
        }

        protected override void OnDestroy()
        {
            ServerConnection.Instance.Dispose();
            base.OnDestroy();
        }
    }
}


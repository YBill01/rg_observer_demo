using Unity.Entities;
using Legacy.Database;
using Unity.Collections;
using Legacy.Network;
using System.Linq;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(NetworkSystems))]
    [UpdateAfter(typeof(DisconnectSystem))]
    public class PlayerProfileLevelUpSystem : ComponentSystem
    {
        private AuthorizationSystem _auth_system;
        private ObserverPlayerSystem _player_system;

        protected override void OnCreate()
        {
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
            _player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();
        }
        protected override void OnUpdate()
        {
            var _buffer = new EntityCommandBuffer(Allocator.TempJob);

            Entities
                .ForEach((Entity entity, ref ObserverPlayerProfileLevelUp request) =>
                {
                    _buffer.DestroyEntity(entity);

                    var _message = default(NetworkMessageRaw);
                    _message.Write((byte)ObserverPlayerMessage.LevelUp);

                    if (_auth_system.Sessions.TryGetValue(request.index, out Entity connect))
                    {
                        var _player = EntityManager.GetComponentData<ObserverPlayerClient>(connect);
                        _message.Send(
                            _player_system.Driver,
                            _player_system.ReliablePeline,
                            _player.connection
                        );
                    }
                });

            _buffer.Playback(EntityManager);
            _buffer.Dispose();
        }
    }
}
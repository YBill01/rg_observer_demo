using Legacy.Database;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using static Legacy.Observer.AuthorizationSessionSystem;

namespace Legacy.Observer
{

    [UpdateInGroup(typeof(PlayerSystems))]
	public class ObsereverPlayerErrorSystem : ComponentSystem
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
                .ForEach((Entity entity, ref ObserverPlayerErrorMessage error) =>
                {
                    _buffer.DestroyEntity(entity);

                    if (!_auth_system.Profiles.TryGetValue(error.index, out PlayerProfileInstance profile))
                        return;

                    profile.Actualize();

                    var _message = default(NetworkMessageRaw);
                    _message.Write((byte)ObserverPlayerMessage.Error);
                    //  profile.Serialize(ref _message);
                    //   _message.Write((uint)error.error);
                    error.Serialize(ref _message);

                    if (_auth_system.Sessions.TryGetValue(error.index, out Entity connect))
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
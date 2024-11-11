
using Unity.Entities;

using Legacy.Database;
using Unity.Collections;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(NetworkSystems))]
    public class PlayerCurrencyChangeEventSystem : ComponentSystem
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
            var _buffer = new EntityCommandBuffer(Allocator.Temp);

            Entities
                .ForEach((Entity entity, ref ObserverPlayerCurrencyChangeEventInfo info) =>
                {
                    _buffer.DestroyEntity(entity);

                    if (!_auth_system.Profiles.TryGetValue(info.player_index, out PlayerProfileInstance profile))
                        return;

                    if (_auth_system.Sessions.TryGetValue(info.player_index, out Entity connect))
                    {
                        var _message = default(NetworkMessageRaw);
                        _message.Write((byte)ObserverPlayerMessage.CurrencyChangeEvent);
                        info.Serialize(ref _message);

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
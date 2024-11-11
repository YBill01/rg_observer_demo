using Legacy.Database;
using Unity.Collections;
using Unity.Entities;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]
    public class CommandAccountSystem : ComponentSystem
    {
        private AuthorizationSystem _auth_system;
        private EntityQuery _query_upgrade;

        protected override void OnCreate()
        {
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_upgrade = GetEntityQuery(
                ComponentType.ReadOnly<CommandRequest>(),
                ComponentType.ReadOnly<CommandAccountTag>(),
                ComponentType.ReadOnly<NetworkMessageRaw>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );
        }

        protected override void OnUpdate()
        {
            var _requests = _query_upgrade.ToComponentDataArray<CommandRequest>(Allocator.TempJob);
            var _messages = _query_upgrade.ToComponentDataArray<NetworkMessageRaw>(Allocator.TempJob);
            var _entities = _query_upgrade.ToEntityArray(Allocator.TempJob);

            for (int i = 0; i < _requests.Length; ++i)
            {
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                var _index = _requests[i].index;
                var _message = _messages[i];
                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
                    var _command = (AccountCommandType)_message.ReadByte();
                    switch (_command)
                    {

                        #region AccountCommandType.UpdateName
                        case AccountCommandType.UpdateName:
                            {
                                string name = _message.ReadString64().ToString();
                                profile.name = name;
                            }
                            break;
                        #endregion
                        #region AccountCommandType.UpdateLanguage
                        case AccountCommandType.UpdateLanguage:
                            {
                                byte k = _message.ReadByte();
                                profile.playerSettings.language =(Language)(k);
                                EntityManager.AddComponentData(EntityManager.CreateEntity(),
                                 new ObserverPlayerProfileRequest { index = _index });

                            }
                            break;
                        #endregion
                    }
                }
                
            }

            _entities.Dispose();
            _requests.Dispose();
            _messages.Dispose();

            //EntityManager.DestroyEntity(_query_upgrade);
        }

    }
}
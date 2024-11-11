using System.Collections.Generic;
using Legacy.Database;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Legacy.Observer
{
    [UpdateInGroup(typeof(PlayerSystems))]
    public class CommandPlayerAppVersionUpdateSystem : ComponentSystem
    {
        private AuthorizationSystem _auth_system;
        private EntityQuery _query_requests;

        protected override void OnCreate()
        {
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<CommandRequest>(),
                ComponentType.ReadOnly<AppVersionUpdateTag>(),
                ComponentType.ReadOnly<NetworkMessageRaw>(),
                ComponentType.Exclude<CommandCompleteTag>()
            );
        }

        protected override void OnUpdate()
        {
            var _requests = _query_requests.ToComponentDataArray<CommandRequest>(Allocator.TempJob);
            var _entities = _query_requests.ToEntityArray(Allocator.TempJob);
            var _gameSetting = Settings.Instance.Get<BaseGameSettings>();

            for (int i = 0; i < _requests.Length; ++i)
            {
                EntityManager.AddComponentData(_entities[i], new CommandCompleteTag());

                var _index = _requests[i].index;
                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
                    if (!Application.version.Equals(profile.customRewards.ClientAppVersion))
                    {
                        profile.customRewards.ClientAppVersion = Application.version;
                        profile.currency.hard += _gameSetting.appVersionUpdateReward;
                    }
                }

                EntityManager.AddComponentData(EntityManager.CreateEntity(),
                    new ObserverPlayerProfileRequest { index = _index });
            }

            _entities.Dispose();
            _requests.Dispose();
        }
    }
}
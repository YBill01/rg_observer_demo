using Unity.Entities;
using Unity.Collections;
using Legacy.Database;

namespace Legacy.Observer
{

    [UpdateInGroup(typeof(PlayerSystems))]
    public class CommandDBUpdateSystem : ComponentSystem
	{
		private AuthorizationSystem _auth_system;
		private EntityQuery _query_requests;

		protected override void OnCreate()
		{
			_auth_system = World.GetOrCreateSystem<AuthorizationSystem>();

            _query_requests = GetEntityQuery(
                ComponentType.ReadOnly<CommandRequest>(),
                ComponentType.ReadOnly<CommandCompleteTag>()
            );
        }

		protected override void OnUpdate()
		{
            //var _collection = _auth_system.Database.GetCollection<PlayerProfileInstance>("users");
            var _requests = _query_requests.ToComponentDataArray<CommandRequest>(Allocator.TempJob);

            for (int i = 0; i < _requests.Length; ++i)
            {
                var _index = _requests[i].index;

                if (_auth_system.Profiles.TryGetValue(_index, out PlayerProfileInstance profile))
                {
	                var updater = profile.GetDBUpdater() as DBUpdater<PlayerProfileInstance>;
	                updater.Update();
                }

            }

            _requests.Dispose();
            EntityManager.DestroyEntity(_query_requests);
        }

	}
}
using Legacy.Database;
using Unity.Collections;
using Unity.Entities;

namespace Legacy.Observer
{
	[UpdateInGroup(typeof(NetworkSystems))]
	[UpdateAfter(typeof(AuthorizationSystem))]

	public class DisconnectSystem : ComponentSystem
	{
		private EntityQuery _query_disconnected;
		private EntityQuery _query_connected;
		private AuthorizationSystem _auth_system;
		private ObserverPlayerSystem _player_system;

		protected override void OnCreate()
		{
			_query_disconnected = GetEntityQuery(
				ComponentType.ReadOnly<ObserverPlayerClient>(),
				ComponentType.ReadOnly<ObserverPlayerDisconnect>(),
				ComponentType.Exclude<ObserverPlayerInBattle>()
			);

			_query_connected = GetEntityQuery(
				ComponentType.ReadOnly<ObserverPlayerClient>(),
				ComponentType.ReadOnly<ObserverPlayerAuthorized>(),
				ComponentType.Exclude<ObserverPlayerDisconnect>()
			);

			_auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
			_player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();
		}

		protected override void OnUpdate()
		{
			var is_reconnect = new NativeArray<bool>(1, Allocator.TempJob);
			var _entities = _query_disconnected.ToEntityArray(Allocator.TempJob);
			for (int i = 0; i < _entities.Length; ++i)
			{
				var _entity = _entities[i];
				PostUpdateCommands.DestroyEntity(_entity);

				var _client = EntityManager.GetComponentData<ObserverPlayerClient>(_entity);
				_player_system.Driver.Disconnect(_client.connection);

				if (EntityManager.HasComponent<ObserverPlayerAuthorized>(_entity))
				{
					var _auth = EntityManager.GetComponentData<ObserverPlayerAuthorized>(_entity);

					is_reconnect[0] = false;
					var _job_session = new CheckReconnectJob
					{
						is_reconnect = is_reconnect,
						player_index = _auth.index
					}.Schedule(_query_connected);

					_job_session.Complete();

					var _cancel_entity = EntityManager.CreateEntity();
					EntityManager.AddComponentData(_cancel_entity, new ObserverEventDisconnect
					{
						index = _auth.index
					});

					if (is_reconnect[0])
						continue;

					_auth_system.Sessions.Remove(_auth.index);

					if (_auth_system.Profiles.TryRemove(_auth.index, out PlayerProfileInstance profile))
					{
						UnityEngine.Debug.Log("DisconnectSystem >> Remove Sessions|Profiles -> " + profile._id);
					}
				}
			}
			_entities.Dispose();
			is_reconnect.Dispose();
		}

		[Unity.Burst.BurstCompile]
		struct CheckReconnectJob : IJobForEachWithEntity<ObserverPlayerAuthorized>
		{
			internal NativeArray<bool> is_reconnect;
			internal uint player_index;
			public void Execute(Entity entity, int index, [ReadOnly] ref ObserverPlayerAuthorized player)
			{
				if (player.index == player_index)
					is_reconnect[0] = true;
			}
		}

	}
}


using Legacy.Database;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using static Legacy.Observer.AuthorizationSessionSystem;

namespace Legacy.Observer
{

    /*[UpdateInGroup(typeof(PlayerSystems))]
	public class ErrorSystem : ComponentSystem
	{
		private EntityQuery _error_query;
		private EntityQuery _query_complete;
		private AuthorizationSystem _auth_system;
		private ObserverPlayerSystem _player_system;

		private NativeQueue<_auth_info> _auth;

		protected override void OnCreate()
		{
			_error_query = GetEntityQuery(
				ComponentType.ReadOnly<UserErrorData>()
				);
			_query_complete = GetEntityQuery(
				ComponentType.ReadOnly<ObserverPlayerClient>(),
				ComponentType.ReadOnly<ObserverPlayerAuthorized>(),
				ComponentType.Exclude<ObserverPlayerDisconnect>()
			);

			_auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
			_player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();

			_auth = new NativeQueue<_auth_info>(Allocator.Persistent);
			RequireForUpdate(_error_query);
		}

		protected override void OnDestroy()
		{
			_auth.Dispose();
		}

		protected override void OnUpdate()
		{
			var _job_session = new CollectSessionsJob
			{
				auth = _auth.AsParallelWriter()
			}.Schedule(_query_complete);

			_job_session.Complete();

			var errors = _error_query.ToComponentDataArray<UserErrorData>(Allocator.TempJob);
			while (_auth.TryDequeue(out _auth_info info))
			{
				foreach (var error_data in errors)
				{
					var _email = error_data.email.ToString();
					if (_email != info.email.ToString()) continue;
					if (_auth_system.Profiles.TryGetValue(_email, out PlayerProfileInstance profile))
					{
						var _client = EntityManager.GetComponentData<ObserverPlayerClient>(info.entity);

						var _message = default(NetworkMessageRaw);
						_message.Write((byte)ObserverPlayerMessage.RequestError);
						error_data.Serialize(ref _message);

						_message.Send(
							_player_system.Driver,
							_player_system.ReliablePeline,
							_client.connection
						);
					}
				}
			}
			errors.Dispose();
			EntityManager.DestroyEntity(_error_query);
		}

		[Unity.Burst.BurstCompile]
		struct CollectSessionsJob : IJobForEachWithEntity<ObserverPlayerAuthorized>
		{
			public NativeQueue<_auth_info>.ParallelWriter auth;

			public void Execute(Entity entity, int index, [ReadOnly] ref ObserverPlayerAuthorized session)
			{
				auth.Enqueue(new _auth_info
				{
					email = session.email,
					entity = entity
				});
			}
		}
	}*/
}
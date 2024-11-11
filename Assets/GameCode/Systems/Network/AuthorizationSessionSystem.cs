using Unity.Entities;
using Legacy.Database;
using Unity.Collections;

namespace Legacy.Observer
{
	[UpdateInGroup(typeof(NetworkSystems))]
	[UpdateAfter(typeof(DisconnectSystem))]

	public class AuthorizationSessionSystem : ComponentSystem
	{
		enum _auth_type
		{
			Wait,
			Complete,
			Error
		}

		public struct _auth_info
		{
			public Entity entity;
			public uint index;
		}

		private EntityQuery _query_complete;
		private AuthorizationSystem _auth_system;
		private ObserverPlayerSystem _player_system;

		private EndSimulationEntityCommandBufferSystem _barrier;
		private NativeQueue<_auth_info> _auth;

		protected override void OnCreate()
		{
			_query_complete = GetEntityQuery(
				ComponentType.ReadOnly<ObserverPlayerClient>(),
				ComponentType.ReadOnly<ObserverPlayerSession>(),
				ComponentType.Exclude<ObserverPlayerDisconnect>()
			);

			_auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
			_player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();
			_barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
			_auth = new NativeQueue<_auth_info>(Allocator.Persistent);
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

			while (_auth.TryDequeue(out _auth_info info))
			{
				if (_auth_system.Profiles.TryGetValue(info.index, out PlayerProfileInstance profile))
				{
					if (_auth_system.Sessions.ContainsKey(info.index))
					{
						_auth_system.Sessions.Remove(info.index);
					}

					if (_auth_system.Sessions.TryAdd(info.index, info.entity))
					{
						UnityEngine.Debug.Log("AuthorizationSessionSystem >> New Session -> " + info.index);

						EntityManager.RemoveComponent<ObserverPlayerSession>(info.entity);
						EntityManager.AddComponentData(info.entity, new ObserverPlayerAuthorized
						{
							index = profile._id
						});

						// update client info
						var _client = EntityManager.GetComponentData<ObserverPlayerClient>(info.entity);
						_client.status = ObserverPlayerStatus.Authorized;
						_client.index = profile._id;
						EntityManager.SetComponentData(info.entity, _client);

						// send auth complete
						SendAuthorizedMessage(profile, _client);

						return;
					}
				}
			}
		}

		private void SendAuthorizedMessage(PlayerProfileInstance profile, ObserverPlayerClient _client)
		{
			var _diff_time = System.DateTime.UtcNow - profile.session.time;
			GameDebug.Log($"Time after last session: {_diff_time.TotalMinutes}");

			var _message = default(NetworkMessageRaw);
			_message.Write((byte)ObserverPlayerMessage.Authorization);

			//Update ProfileByTime
			profile.Actualize();

			profile.Serialize(ref _message);

			if (profile.session.port > 0)
			{
				_message.Write(true);
				_message.Write(profile.session.server);
				_message.Write(profile.session.port);
			}
			else
			{
				_message.Write(false);
			}

			_message.Send(
				_player_system.Driver,
				_player_system.ReliablePeline,
				_client.connection
			);
		}

		[Unity.Burst.BurstCompile]
		struct CollectSessionsJob : IJobForEachWithEntity<ObserverPlayerSession>
		{
			public NativeQueue<_auth_info>.ParallelWriter auth;

			public void Execute(Entity entity, int index, [ReadOnly] ref ObserverPlayerSession session)
			{
				auth.Enqueue(new _auth_info
				{
					index = session.index,
					entity = entity
				});
			}
		}
	}
}


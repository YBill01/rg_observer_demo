/*using Legacy.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Unity.Collections;
using Unity.Entities;

namespace Legacy.Observer
{
	[UpdateInGroup(typeof(NetworkSystems))]

	public class RequestSessionSystem : ComponentSystem
	{
		private EntityQuery _query;
		private AuthorizationSystem _auth_system;

		protected override void OnCreate()
		{
			_query = GetEntityQuery(
				ComponentType.ReadOnly<DatabaseRequestSession>()
			);
			_auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
		}

		protected override void OnUpdate()
		{
			var _collection = _auth_system.Database.GetCollection<PlayerProfileInstance>("users");

			var _requests = _query.ToComponentDataArray<DatabaseRequestSession>(Allocator.TempJob);
			for (int i = 0; i < _requests.Length; ++i)
			{
				_database(_collection, _requests[i]);
			}
			_requests.Dispose();

			// destroy
			EntityManager.DestroyEntity(_query);
		}

		private async void _database(
			IMongoCollection<PlayerProfileInstance> collection,
			DatabaseRequestSession request
		)
		{
			var _filter = new BsonDocument("email", new BsonDocument("$eq", request.email.ToString()));
			var _options = new FindOneAndUpdateOptions<PlayerProfileInstance> { IsUpsert = false };

			var _update = new UpdateDefinitionBuilder<PlayerProfileInstance>()
				.Set(n => n.session, new PlayerProfileSession
				{
					port = request.port,
					time = System.DateTime.UtcNow
				});

			await collection.FindOneAndUpdateAsync(_filter, _update, _options);
		}
	}
}*/
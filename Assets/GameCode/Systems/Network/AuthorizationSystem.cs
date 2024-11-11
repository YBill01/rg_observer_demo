using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

using Unity.Collections;
using Unity.Entities;

using Legacy.Database;
using System.Collections.Concurrent;
using Unity.Mathematics;
using System;

namespace Legacy.Observer
{
	[UpdateInGroup(typeof(NetworkSystems))]

	public class AuthorizationSystem : ComponentSystem
	{
		const int AuthLimit = 10;

		private EntityQuery _query_connections;
		private ObserverPlayerSystem _player_system;

		private ConcurrentDictionary<uint, PlayerProfileInstance> _profiles;
		public ConcurrentDictionary<uint, PlayerProfileInstance> Profiles => _profiles;

		private NativeHashMap<uint, Entity> _sessions;		
		public NativeHashMap<uint, Entity> Sessions => _sessions;

		private MongoClient _client;

		private IMongoDatabase _database;
		public IMongoDatabase Database { get => _database; }

		protected override void OnCreate()
		{
			_query_connections = GetEntityQuery(
				ComponentType.ReadOnly<ObserverPlayerClient>(),
				ComponentType.ReadOnly<ObserverPlayerAuthorization>(),
				ComponentType.Exclude<ObserverPlayerDisconnect>()
			);

			_player_system = World.GetOrCreateSystem<ObserverPlayerSystem>();

			_profiles = new ConcurrentDictionary<uint, PlayerProfileInstance>();
			_sessions = new NativeHashMap<uint, Entity>(1024, Allocator.Persistent);
#if UNITY_EDITOR
			_client = new MongoClient("mongodb://88.99.198.202:27017");
#else
			_client = new MongoClient("mongodb://127.0.0.1:27017");
#endif
			_database = _client.GetDatabase("game_legacy");
		}

		protected override void OnDestroy()
		{
			_profiles.Clear();
			_sessions.Dispose();
		}

		protected override void OnUpdate()
		{
			// auth requests 
			var _collection = _database.GetCollection<PlayerProfileInstance>("users");
			var _entities = _query_connections.ToEntityArray(Allocator.TempJob);
			var _length = math.min(_entities.Length, AuthLimit);
			for (int i = 0; i < _length; ++i)
			{
				var _entity = _entities[i];
				// temp remove
				PostUpdateCommands.RemoveComponent<ObserverPlayerAuthorization>(_entity);

				// thread database request
				var _auth = EntityManager.GetComponentData<ObserverPlayerAuthorization>(_entity);
				_load_profile(_collection, _auth, _entity, (uint)i, BinaryDatabase.appVersion);
			}

			_entities.Dispose();
		}

		async void _load_profile(
			IMongoCollection<PlayerProfileInstance> _collection,
			ObserverPlayerAuthorization auth,
			Entity entity,
			uint index,
			string appVersion
		)
		{
			PlayerProfileInstance _profile;
			var _filter = new BsonDocument("devices.device_id", new BsonDocument("$eq", auth.device_id.ToString()));

			using (var cursor = await _collection.FindAsync(_filter))
			{
				_profile = await cursor.SingleOrDefaultAsync();
				if (_profile == null)
				{
					uint _max_index = 1;
					var _last_user = await _collection.Aggregate()
						.SortByDescending((a) => a._id)
						.Project(p => new { p._id })						
						.Limit(1)
						.SingleOrDefaultAsync();

					if (_last_user != null)
					{
						_max_index = _last_user._id + 1;
					}
					_profile = new PlayerProfileInstance { _id = _max_index + index, name = auth.name.ToString()};
					_profile.devices.Add(new PlayerProfileDevice
					{
						device_id = auth.device_id.ToString(),
						device_model = auth.device_model.ToString(),
						operating_system = auth.operating_system.ToString(),
						memory_size = auth.memory_size
					});
					_profile.playerSettings.language = (Language)auth.language;
					_profile.customRewards.ClientAppVersion = appVersion;

					await _collection.InsertOneAsync(_profile);
				}
				
				// set DBUpdater
				var updaterProfile = new DBUpdater<PlayerProfileInstance>(_collection, _profile._id);
				_profile.SetDBUpdater(updaterProfile);

				if (EntityManager.Exists(entity))
				{
					GameDebug.Log("AuthorizationSystem >> profile -> " + _profile._id);

					if (_profiles.ContainsKey(_profile._id))
					{

                        GameDebug.Log("AuthorizationSystem >> Profile exists in profiles -> " + _profile._id);
                        if (_sessions.TryGetValue(_profile._id, out Entity _entity))
                        {
                            GameDebug.Log("AuthorizationSystem >> Session exists in sessions -> " + _profile._id);

                            var _client = EntityManager.GetComponentData<ObserverPlayerClient>(_entity);
                            _player_system.Driver.Disconnect(_client.connection);
                            _sessions.Remove(index);

                            //Если был в бою
                            if (EntityManager.HasComponent<ObserverPlayerInBattle>(_entity))
                            {
                                var _inBattle = EntityManager.GetComponentData<ObserverPlayerInBattle>(_entity);
                                EntityManager.AddComponentData(entity, _inBattle);
                                _profile.session.port = _inBattle.port;
                                _profile.session.server = _inBattle.serverIP.ToString();
                                //TODO: Check if battle is valid.
                            }

                            EntityManager.DestroyEntity(_entity);
                            GameDebug.Log("AuthorizationSystem >> Remove old session from sessions -> " + _profile._id);
                        }                        
                    }
                    
                    _profiles.AddOrUpdate(_profile._id, _profile, (k, v) => { return _profile; });                        
                    

                    if (!EntityManager.HasComponent<ObserverPlayerAuthorized>(entity))
                    {
                        EntityManager.AddComponentData(entity, new ObserverPlayerSession
                        {
                            index = _profile._id
                        });
                    }
                }
			}
		}
	}
}


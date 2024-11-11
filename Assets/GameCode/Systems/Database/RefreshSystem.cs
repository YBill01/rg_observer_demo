using Unity.Entities;

namespace Legacy.Observer
{
	public class RefreshSystem : ComponentSystem
	{
		/*struct _refresh_event : IComponentData { };

		private DateTime _last_write;
		private EntityQuery _query;
		private BeginPresentationEntityCommandBufferSystem _barrier;
		private Stopwatch _timer;*/

		protected override void OnCreate()
		{
			/*_last_write = Database.BinaryDatabase.Instance.LastWrite;
			_query = GetEntityQuery(
				ComponentType.ReadOnly<_refresh_event>()
			);
			_barrier = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
			_timer = new Stopwatch();
			_timer.Start();*/

			Database.BinaryDatabase.Instance.Read(true);
		}

		protected override void OnDestroy()
		{
			Database.BinaryDatabase.Instance.Dispose();
		}

		protected override void OnUpdate()
		{
			/*if (!_query.IsEmptyIgnoreFilter)
			{
				var _start = _timer.ElapsedMilliseconds;

				EntityManager.DestroyEntity(_query);
				Database.BinaryDatabase.Instance.Dispose();
				Database.BinaryDatabase.Instance.Read(true);

				UnityEngine.Debug.Log("Reload Database: " + (_timer.ElapsedMilliseconds - _start));

				return;
			}

			RefreshDatabase(_barrier.CreateCommandBuffer());*/
		}

		/*private async void RefreshDatabase(EntityCommandBuffer buffer)
		{
			await Task.Run(() => 
			{
				var _next_write = Database.BinaryDatabase.Instance.LastWrite;
				if (_last_write != _next_write)
				{
					_last_write = _next_write;
					var _reload = buffer.CreateEntity();
					buffer.AddComponent<_refresh_event>(_reload);
				}
			});
		}*/

	}
}
using System.Diagnostics;
using Unity.Entities;
using Unity.Mathematics;

namespace Legacy.Observer
{
	public class NetworkSystems : ComponentSystemGroup
	{
		public static NetworkSystems _instance;

		private Random _random;
		private Stopwatch _timer;
		public static ushort _delta_time;
		private long _last_time;

		public static uint RandomPercent { get { return _instance._random.NextUInt(101u); } }
		public static uint RandomInt { get { return _instance._random.NextUInt(); } }
		public static long ElapsedMilliseconds { get { return _instance._timer.ElapsedMilliseconds; } }

		protected override void OnCreate()
		{
			_instance = this;
			_random = new Random((uint)UnityEngine.Random.Range(1, uint.MaxValue));
			_random.NextUInt();

			_timer = new Stopwatch();
			_timer.Start();
			base.OnCreate();
		}

		protected override void OnUpdate()
		{
			_delta_time = (ushort)(_timer.ElapsedMilliseconds - _last_time);
			_last_time = _timer.ElapsedMilliseconds;
			base.OnUpdate();
		}
	}
}

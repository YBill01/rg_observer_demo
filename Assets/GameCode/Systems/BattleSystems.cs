
using Unity.Entities;

namespace Legacy.Observer
{
	[UpdateAfter(typeof(MatchmakingSystems))]
	public class BattleSystems : ComponentSystemGroup
	{
	}
}

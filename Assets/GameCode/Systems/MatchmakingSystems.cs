
using Unity.Entities;

namespace Legacy.Observer
{
	[UpdateAfter(typeof(NetworkSystems))]
	public class MatchmakingSystems : ComponentSystemGroup
	{
	}
}

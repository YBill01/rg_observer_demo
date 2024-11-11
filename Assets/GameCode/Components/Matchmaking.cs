using Unity.Entities;
using Legacy.Database;
using Unity.Networking.Transport;

namespace Legacy.Observer
{

	public struct SelectedDeck
	{
		public ushort Card1;
		public ushort Card2;
		public ushort Card3;
		public ushort Card4;
		public ushort Card5;
		public ushort Card6;
		public ushort Card7;
		public ushort Card8;

		public void setup(ushort[] list)
		{
			Card1 = list[0];
			Card2 = list[1];
			Card3 = list[2];
			Card4 = list[3];
			Card5 = list[4];
			Card6 = list[5];
			Card7 = list[6];
			Card8 = list[7];
		}

		public uint[] uintPresentation()
		{
			return new uint[]
			{
				(uint)Card1,
				(uint)Card2,
				(uint)Card3,
				(uint)Card4,
				(uint)Card5,
				(uint)Card6,
				(uint)Card7,
				(uint)Card8,
			};

		}
	}

	public struct MatchmakingRequest : IComponentData
	{
		public MatchmakingType type;
		public uint rating;
		public float avarage_cards;
		public int winLoseRate;
		public int hero_lvl;
	}

	public struct MatchmakingResponse : IComponentData
	{
        public NetworkConnection Connect;
		public ObserverBattlePlayer Enemy;
	}

	public struct MatchmakingCancel : IComponentData
	{
		public Entity connect;
	}

	public struct MatchmakingBattle : IComponentData
	{
		public int BattleIndex;
	}

}

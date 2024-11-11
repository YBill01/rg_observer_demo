using Unity.Entities;
using Unity.Networking.Transport;
using Legacy.Database;
using Unity.Collections;
using System;

namespace Legacy.Observer
{
    public struct ObserverGameClient : IComponentData
	{
		public NetworkConnection connection;
		public ObserverGameStatus status;
		public float alive;
	}

	public struct ServerNetworkDriver : IComponentData
	{
		
	}

	public struct ObserverGameClientStats : IComponentData
	{
		public ushort port;

		public FixedString64 ip;

        public override string ToString()
        {
            return $"BattlesCount:  port: {port}, IP: {ip}";
        }
    }

	public struct ObserverGameDisconnect : IComponentData
	{

	}
	
	public struct ObserverGameClientActiveTag : IComponentData
	{

	}
	public struct ObserverGameClientPlayersTag : IComponentData
	{

	}
	public struct ObserverGameClientReadyTag : IComponentData
	{

	}
	public struct ObserverGameDisconnectRequest : IComponentData
	{

	}

	// 3 sec timer to transfer data observer <-> game
	public struct BattleInstanceLinking : IComponentData
	{
		public float expire;
	}

    public struct ObserverBattleServerRequest : IComponentData { }
    public struct ObserverBattleClientRequest : IComponentData { }
    public struct ObserverBattleServerPlaying : IComponentData { }
    public struct ObserverBattleServerFinished : IComponentData { }
    public struct ObserverBattleServerExpired : IComponentData { }
    public struct ObserverBattleServerStarted : IComponentData { }
    public struct ObserverBattleServerPlayers : IComponentData { }
    public struct ObserverBattleServerWaiting : IComponentData
    {
        public Entity connect;
    }

    public struct ObserverBattleServerResponse : IComponentData
    {
        public Entity connect;
        public ushort port;
        public FixedString64 ip;
    }

    public struct ObserverBattleServerComplete : IComponentData { }

    public struct BattleInstanceConnectedTag : IComponentData
	{

	}


	public struct BattleInstanceInitialized : ISystemStateComponentData
	{
		public ushort ProcessIndex;
	}

	public struct BattleInstanceDestroy : IComponentData
	{
	}

	public struct BattleInstancePort : IComponentData
	{
		public ushort Value;
	}

	public struct ObserverPlayerClient : IComponentData
	{
		public NetworkConnection connection;
		public ObserverPlayerStatus status;

		public uint index;
		public uint rating;
		public float alive;
	}

	public struct ObserverPlayerAuthorization : IComponentData
	{
		public FixedString64 name;
		public FixedString64 device_id;
		public FixedString64 device_model;
		public FixedString64 operating_system;
		//public FixedString64 language;
		public Byte language;
		public int memory_size;
	}

	public struct ObserverPlayerSession : IComponentData
	{
		public uint index;
	}

	public struct ProfileInitTag : IComponentData { }
	public struct ObserverPlayerAuthorized : IComponentData
	{
		public uint index;
	}
	
	public struct ObserverPlayerProfileRequest : IComponentData
	{
		public uint index;
	}    

    public struct ObserverPlayerProfileLevelUp : IComponentData
	{
		public uint index;
	}

	public struct ObserverPlayerDisconnect : IComponentData
	{

	}

	public struct ObserverPlayerInBattle : IComponentData
	{
		public uint expireTime;
        public ushort port;
        public FixedString32 serverIP;
	}

	public struct ObserverBattleRatingResult : IComponentData
	{
		public BattlePlayer battlePlayer;
		public BattleEnemy enemy;
		public uint player;
		public ushort battlefied;
		public BattlePlayerSide side;

        public BattlePlayerProfile profile;
        public BattlePlayerStats stats;
        public BattleInstanceResult result;
    }

	public struct ObserverBattleMissionResult : IComponentData
	{
		public uint player;
		public ushort battlefied;
		public ushort tutorail;
		public ushort mission;
		public BattlePlayerSide winnerSide;

		public BattleInstanceResult result;
		public BattlePlayerProfile profile;
		public BattlePlayerStats stats;
	}

	public struct ObserverTutorialUpdateState : IComponentData
	{
		public uint player;

		public ushort hard_tutorial_state;
		public int menu_tutorial_state;
		public ushort menu_tutorial_step;
		public ushort senario_index;
	}


	public struct ObserverEventDisconnect : IComponentData
	{
		public uint index;
		public bool isPlayerCancel;
	}

	public struct ObserverPlayerCommand : IComponentData
	{

	}


	public struct ClientRequestLoots : IComponentData
	{
		public LootsType type;
		public byte index;
		public uint player;
	}

	public struct ClientRequestLootsQueue : IComponentData
	{
		public byte index;
		public uint player;
	}

	public struct ClientRequestLootsSkip : IComponentData
	{
		public byte index;
		public uint player;
	}

}

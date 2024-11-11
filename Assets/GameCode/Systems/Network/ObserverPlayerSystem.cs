using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;

using Legacy.Database;
using Unity.Networking.Transport.Utilities;

namespace Legacy.Observer
{
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(NetworkSystems))]
    [UpdateAfter(typeof(AuthorizationSessionSystem))]

    public class ObserverPlayerSystem : SystemBase
    {
        public const int DisconnectTimeout = 3;

        private EndSimulationEntityCommandBufferSystem _barrier;
        private AuthorizationSystem _auth_system;

        private NetworkPipeline _reliable_peline;
        private NetworkPipeline _unreliable_pipeline;

        public NetworkPipeline ReliablePeline => _reliable_peline;
        public NetworkPipeline UnreliablePeline => _unreliable_pipeline;

        private NetworkDriver _driver;
        public NetworkDriver Driver => _driver;

        private NetworkDriver.Concurrent _driver_concurrent;
        public NetworkDriver.Concurrent DriverConcurrent => _driver_concurrent;

        protected override void OnCreate()
        {
            var reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };
            _driver = NetworkDriver.Create(reliabilityParams);
            _auth_system = World.GetOrCreateSystem<AuthorizationSystem>();
            _unreliable_pipeline = _driver.CreatePipeline(typeof(NullPipelineStage));
            _reliable_peline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            var _addres = NetworkEndPoint.AnyIpv4;
            _addres.Port = 6668;
            if (_driver.Bind(_addres) != 0)
                throw new Exception("Failed to bind to port: " + _addres.Port);
            else
            {
                UnityEngine.Debug.Log(string.Format("Player Host: {0} ", _addres.Port));
                _driver.Listen();
            }

            _driver_concurrent = _driver.ToConcurrent();
            _barrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnDestroy()
        {
            if (_driver.IsCreated)
            {
                _driver.Dispose();
            }
        }

        struct ConnectionAcceptJob : IJob
        {
            public EntityCommandBuffer buffer;
            public NetworkDriver driver;
            internal EntityArchetype archetype;

            public void Execute()
            {
                NetworkConnection _connect;
                while ((_connect = driver.Accept()) != default)
                {
                    if (_connect.PopEvent(driver, out DataStreamReader reader) != NetworkEvent.Type.Empty)
                    {
                        _connect.Disconnect(driver);
                        continue;
                    }

                    UnityEngine.Debug.Log("New Server Connection: " + _connect.InternalId);

                    var _entity = buffer.CreateEntity(archetype);
                    buffer.SetComponent(_entity, new ObserverGameClient
                    {
                        connection = _connect,
                        status = ObserverGameStatus.Connected
                    });
                    buffer.AddComponent(_entity, new ObserverGameClientStats
                    {
                        port = 0
                    });
                }
            }
        }

        protected override void OnUpdate()
        {
            if (!_driver.IsCreated)
                return;

            var buffer = _barrier.CreateCommandBuffer();
            _driver.ScheduleUpdate().Complete();

            NetworkConnection _connect;

            while ((_connect = _driver.Accept()) != default)
            {
                if (_connect.PopEvent(_driver, out DataStreamReader reader) != NetworkEvent.Type.Empty)
                {
                    _connect.Disconnect(_driver);
                    continue;
                }
                UnityEngine.Debug.Log("New Connection: " + _connect.InternalId);

                var _entity = buffer.CreateEntity();
                buffer.AddComponent(_entity, new ObserverPlayerClient
                {
                    connection = _connect,
                    status = ObserverPlayerStatus.Connected
                });
            }


            var delta = Time.DeltaTime;

            var inBattle = GetComponentDataFromEntity<ObserverPlayerInBattle>(true);
            Entities
                .WithNone<ObserverPlayerDisconnect>()
                .ForEach((Entity entity, ref ObserverPlayerClient client) =>
                {
                    if (!client.connection.IsCreated)
                    {
                        client.status = ObserverPlayerStatus.Disconnect;
                        buffer.AddComponent<ObserverPlayerDisconnect>(entity);
                        return;
                    }

                    // ping
                    if (client.status > ObserverPlayerStatus.Disconnect)
                    {
                        client.alive += delta;
                        if (client.alive > 0.4f)
                        {
                            client.alive = 0;
                            _driver.BeginSend(_unreliable_pipeline, client.connection, out DataStreamWriter _writer, 1);
                            _writer.WriteByte((byte)ObserverPlayerMessage.Alive);
                            _driver.EndSend(_writer);
                        }
                    }
                    if (inBattle.HasComponent(entity))
                    {

                    }

                    NetworkEvent.Type _event;
                    while ((_event = _driver.PopEventForConnection(client.connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
                    {
                        //UnityEngine.Debug.Log("[Observer >> Player] >> SocketEventType: " + _event);
                        switch (_event)
                        {

                            case NetworkEvent.Type.Disconnect:
                                client.connection = default;
                                return;

                            case NetworkEvent.Type.Data:
                                var _protocol = (ObserverPlayerMessage)reader.ReadByte();
                                //UnityEngine.Debug.Log("[Observer >> Player] >> protocol: " + _protocol);
                                var _message = default(NetworkMessageRaw);
                                _message.Write(reader);
                                _message.size = 0;

                                switch (_protocol)
                                {
                                    case ObserverPlayerMessage.UserCommand:
                                        CreateCommand(client.index, _message, ref buffer);
                                        break;

                                    case ObserverPlayerMessage.Authorization:
                                        //UnityEngine.Debug.Log("ObserverPlayerMessage.Authorization >> prev status: " + client.status);

                                        if (client.status == ObserverPlayerStatus.Connected)
                                        {
                                            client.status = ObserverPlayerStatus.Authorized;

                                            var _authorization = default(ObserverPlayerAuthorization);
                                            _authorization.name = _message.ReadString64();
                                            _authorization.device_id = _message.ReadString64();
                                            _authorization.device_model = _message.ReadString64();
                                            _authorization.operating_system = _message.ReadString64();
                                            _authorization.memory_size = _message.ReadInt();
                                            _authorization.language = _message.ReadByte();
                                            buffer.AddComponent(entity, _authorization);
                                        }
                                        break;

                                    case ObserverPlayerMessage.UpdatedProfile:
                                        if (client.status == ObserverPlayerStatus.Authorized)
                                        {
                                            var newEntity = buffer.CreateEntity();
                                            buffer.AddComponent(newEntity, new ObserverPlayerProfileRequest { index = client.index });
                                        }
                                        break;

                                    case ObserverPlayerMessage.Campaign:
                                        if (client.status == ObserverPlayerStatus.Authorized)
                                        {
                                            client.status = ObserverPlayerStatus.Campaign;
                                            buffer.AddComponent(entity, new BattleInstanceCampaign
                                            {
                                                index = _message.ReadUShort(),
                                                mission = _message.ReadUShort()
                                            });
                                        }
                                        break;
                                    case ObserverPlayerMessage.Tutorial:
                                        if (client.status == ObserverPlayerStatus.Authorized)
                                        {
                                            client.status = ObserverPlayerStatus.Campaign;
                                            buffer.AddComponent(entity, new BattleInstanceTutorial
                                            {
                                                index = _message.ReadUShort()
                                            });
                                        }
                                        break;
                                    case ObserverPlayerMessage.Sandbox:
                                        if (client.status == ObserverPlayerStatus.Authorized)
                                        {
                                            client.status = ObserverPlayerStatus.Campaign;
                                            buffer.AddComponent(entity, new BattleInstanceSandbox
                                            {
                                                index = _message.ReadUShort()
                                            });
                                            //var _writer = _driver.BeginSend(_reliable_peline, client.connection, 1);
                                            //_writer.WriteByte((byte)ObserverPlayerMessage.Sandbox);
                                            //_driver.EndSend(_writer);
                                        }
                                        break;

                                    case ObserverPlayerMessage.Matchmaking1x1:
                                        //UnityEngine.Debug.Log("ObserverPlayerMessage.Matchmaking1x1 >> prev status: " + client.status);
                                        if (client.status == ObserverPlayerStatus.Authorized)
                                        {
                                            if (_auth_system.Profiles.TryGetValue(client.index, out PlayerProfileInstance profile))
                                            {
                                                client.status = ObserverPlayerStatus.Matchmaking;

                                                var avarageCards = CountAvarageCardsLevel(profile);
                                                profile.heroes.GetByIndex(profile.SelectedHero, out PlayerProfileHero profileHero);
                                                buffer.AddComponent(entity, new MatchmakingRequest
                                                {
                                                    winLoseRate = profile.battleStatistic.WinLoseRate,
                                                    type = MatchmakingType.BattlePvP1x1,
                                                    rating = profile.rating.current,
                                                    avarage_cards = avarageCards,
                                                    hero_lvl = profileHero.level
                                                });
                                            }
                                        }
                                        break;
                                    case ObserverPlayerMessage.BattleBotxBot:
                                        //UnityEngine.Debug.Log("ObserverPlayerMessage.Matchmaking1x1 >> prev status: " + client.status);

                                        buffer.AddComponent(entity, new MatchmakingRequest
                                        {
                                            type = MatchmakingType.BattleBotxBot,
                                        });
                                        break;

                                    case ObserverPlayerMessage.MatchmakingCancel:
                                        if (client.status == ObserverPlayerStatus.Matchmaking)
                                        {
                                            client.status = ObserverPlayerStatus.Authorized;
                                            var _cancel_entity = buffer.CreateEntity();
                                            buffer.AddComponent(_cancel_entity, new ObserverEventDisconnect
                                            {
                                                index = client.index,
                                                isPlayerCancel = true
                                            });

                                            // TODO: send in MatchmakingDisposeSystem ?
                                            _driver.BeginSend(_reliable_peline, client.connection, out DataStreamWriter _writer, 1);
                                            _writer.WriteByte((byte)ObserverPlayerMessage.MatchmakingCancel);
                                            _driver.EndSend(_writer);
                                        }
                                        break;

                                    case ObserverPlayerMessage.Matchmaking2x2:
                                        // TODO: 4 players ? with friend ?
                                        break;

                                    case ObserverPlayerMessage.BattleExit:
                                        //UnityEngine.Debug.Log("ObserverPlayerMessage.BattleExit >> prev status: " + client.status);
                                        if (client.status > ObserverPlayerStatus.Authorized)
                                        {
                                            client.status = ObserverPlayerStatus.Authorized;
                                        }
                                        break;
                                    //case ObserverPlayerMessage.MissionFinish:
                                    //    {
                                    //        if (client.status > ObserverPlayerStatus.Authorized)
                                    //        {
                                    //            client.status = ObserverPlayerStatus.Authorized;
                                    //        }

                                    //        var _battlefield = _message.ReadUShort();
                                    //        var _tutorail = _message.ReadUShort();
                                    //        var _mission = _message.ReadUShort();
                                    //        var _player = _message.ReadUInt();
                                    //        var _winnerSide = (BattlePlayerSide)_message.ReadByte();

                                    //        var _profile = default(BattlePlayerProfile);
                                    //        _profile.Deserialize(ref _message);

                                    //        var _stats = default(BattlePlayerStats);
                                    //        _stats.Deserialize(ref _message);

                                    //        var _entity = buffer.CreateEntity();
                                    //        buffer.AddComponent(_entity, new ObserverBattleMissionResult
                                    //        {
                                    //            player = _player,
                                    //            battlefied = _battlefield,
                                    //            tutorail = _tutorail,
                                    //            mission = _mission,
                                    //            profile = _profile,
                                    //            stats = _stats,
                                    //            winnerSide = _winnerSide
                                    //        });
                                    //    }
                                    //    break;
                                    case ObserverPlayerMessage.TutorialUpdate:
                                        {
                                            var _hard_state = _message.ReadUShort();
                                            var _soft_state = _message.ReadUShort();
                                            var _menu_state = _message.ReadInt();
                                            var _scenario_index = _message.ReadUShort();
                                            var _step = _message.ReadUShort();
                                            var _player = _message.ReadUInt();

                                            var _entity = buffer.CreateEntity();
                                            buffer.AddComponent(_entity, new CommandRequest() { index = _player });
                                            buffer.AddComponent(_entity, new ObserverTutorialUpdateState
                                            {
                                                player = _player,
                                                hard_tutorial_state = _hard_state,
                                                menu_tutorial_state = _menu_state,
                                                menu_tutorial_step = _step,
                                                senario_index = _scenario_index
                                            });
                                        }
                                        break;
                                    default:
                                        //UnityEngine.Debug.Log("Undefined ObserverPlayerMessage: " + _protocol);
                                        break;
                                }
                                break;
                        }
                    }

                })
                .WithoutBurst()
                .Run();
        }
        private static float CountAvarageCardsLevel(PlayerProfileInstance profile)
        {
            var playerProfileDeck = profile.sets[profile.config.deck];
            var playerProfileCards = profile.cards;
            var avarage = 0;
            for (int j = 0; j < playerProfileDeck.list.Count; j++)
            {
                var playerCard = playerProfileCards[playerProfileDeck.list[j]];
                //    Cards.Instance.Get(playerProfileDeck.list[j], out BinaryCard binaryCard);//to count rarity
                avarage += playerCard.level;
            }

            var myCardsAvarage = 1.0f * avarage / playerProfileDeck.list.Count;
            return myCardsAvarage;
        }
        private void CreateCommand(uint userID, NetworkMessageRaw commandMessage, ref EntityCommandBuffer buffer)
        {
            var _command = (UserCommandType)commandMessage.ReadByte();
            var _entity = buffer.CreateEntity();
            buffer.AddComponent(_entity, new CommandRequest { index = userID });
            buffer.AddComponent(_entity, commandMessage);

            switch (_command)
            {
                case UserCommandType.DeckUpdate:
                    {
                        buffer.AddComponent(_entity, default(CommandDeckTag));
                    }
                    break;

                case UserCommandType.HeroUpdate:
                    {
                        buffer.AddComponent(_entity, default(CommandHeroTag));
                    }
                    break;

                case UserCommandType.LootUpdate:
                    {
                        buffer.AddComponent(_entity, default(CommandLootTag));
                    }
                    break;
                case UserCommandType.ArenaUpdate:
                    {
                        buffer.AddComponent(_entity, default(CommandArenaTag));
                    }
                    break;
                case UserCommandType.AccountUpdate:
                    {
                        buffer.AddComponent(_entity, default(CommandAccountTag));
                    }
                    break;
                case UserCommandType.ShopUpdate:
                    {
                        buffer.AddComponent(_entity, default(CommandShopTag));
                    }
                    break;
                case UserCommandType.BattlePass:
                    {
                        buffer.AddComponent(_entity, default(CommandBattlePassTag));
                    }
                    break;
                case UserCommandType.Analytics:
                    {
                        buffer.AddComponent(_entity, default(CommandAnalyticTag));
                    }
                    break;
                case UserCommandType.UpdateAppVersion:
                    {
                        buffer.AddComponent(_entity, default(AppVersionUpdateTag));
                    }
                    break;

            }
        }
    }
}


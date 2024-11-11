using Legacy.Database;
using System;

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

/// <summary>
/// Система принимает сообщения от уже установленного в ServerConnectionAcceptSystem соединения
/// </summary>
namespace Legacy.Observer
{
    [UpdateInGroup(typeof(NetworkSystems))]
    [UpdateAfter(typeof(ServerConnectionAcceptSystem))]
    public class ServerConnectionReceiveSystem : JobComponentSystem
    {

        public const int DisconnectTimeout = 15;

        private EndInitializationEntityCommandBufferSystem _barrier;

        protected override void OnCreate()
        {            
            _barrier = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityCommandBuffer.ParallelWriter buffer = _barrier.CreateCommandBuffer().AsParallelWriter();
            float delta = Time.DeltaTime;
            NetworkDriver.Concurrent driver = ServerConnection.Instance.DriverConcurrent;
            NetworkPipeline unreliable = ServerConnection.Instance.UnreliablePipeline;

            inputDeps = Entities
                .WithNone<ObserverGameDisconnect, ObserverGameDisconnectRequest>()
                .ForEach(
                (
                    Entity entity,
                    int entityInQueryIndex,
                    ref ObserverGameClient client,
                    ref ObserverGameClientStats stats
                ) =>
                {
                    if (!client.connection.IsCreated)
                    {
                        buffer.AddComponent<ObserverGameDisconnectRequest>(entityInQueryIndex, entity);
                        return;
                    }

                    if (client.status > ObserverGameStatus.Disconnected)
                    {
                        client.alive += delta;
                        if (client.alive > 0.4f)
                        {
                            client.alive = 0;
                            driver.BeginSend(unreliable, client.connection, out DataStreamWriter _writer, 1);
                            _writer.WriteByte((byte)ObserverGameMessage.Alive);
                            driver.EndSend(_writer);
                        }
                    }

                    NetworkEvent.Type _event;
                    while ((_event = driver.PopEventForConnection(client.connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
                    {
                        switch (_event)
                        {
                            case NetworkEvent.Type.Disconnect:
                                client.connection = default;
                                GameDebug.Log($"Message from server Disconnect from socket event.");

                                buffer.AddComponent<ObserverGameDisconnectRequest>(entityInQueryIndex, entity);
                             //       ServerConnection.Instance.StartNewServer();//////////////////////////////////////////////////////////////////
                                return;

                            case NetworkEvent.Type.Data:
                                var _protocol = (ObserverGameMessage)reader.ReadByte();

                                var _message = default(NetworkMessageRaw);
                                _message.Write(reader);
                                _message.size = 0;

                                switch (_protocol)
                                {
                                    case ObserverGameMessage.Ready:
                                        {
                                            GameDebug.Log($"Message from Server Ready: {ObserverGameMessage.Ready}.");

                                            if (client.status == ObserverGameStatus.Connected)
                                            {
                                                client.status = ObserverGameStatus.Complete;

                                                stats.ip = _message.ReadString64();
                                                stats.port = _message.ReadUShort();
                                            }

                                            GameDebug.Log($"Server stats: {stats}");
                                            buffer.AddComponent(entityInQueryIndex, entity, default(ObserverGameClientReadyTag));

                                            //add conponent server ready
                                            //system --> server ready and battle with waiting
                                            //
                                        }
                                        break;

                                    case ObserverGameMessage.Players:
                                        {
                                            if (client.status == ObserverGameStatus.Complete)
                                            {
                                                var _entity = buffer.CreateEntity(entityInQueryIndex);
                                                buffer.AddComponent(entityInQueryIndex, _entity, new ObserverBattleServerResponse
                                                {
                                                    connect = entity,
                                                    ip = stats.ip,
                                                    port = stats.port
                                                });
                                            }

                                        }
                                        break;

                                    case ObserverGameMessage.ResultMission:
                                        {
                                            var _battlefield = _message.ReadUShort();

                                            BattleInstanceResult _result = default;
                                            _result.Deserialize(ref _message);
                                            var bbb = 123;
                                            BattleInstanceCampaign _campaign = default;
                                            _campaign.Deserialize(ref _message);
                                            //var _battlefield = _message.ReadUShort();
                                            //var _tutorail = _message.ReadUShort();
                                            //var _mission = _message.ReadUShort();
                                            //var _player = _message.ReadUInt();
                                            //var _winnerSide = (BattlePlayerSide)_message.ReadByte();

                                            //var _profile = default(BattlePlayerProfile);
                                            //_profile.Deserialize(ref _message);

                                            //var _stats = default(BattlePlayerStats);
                                            //_stats.Deserialize(ref _message);
                                            var _players_count = _message.ReadByte();

                                            for (byte i = 0; i < _players_count; ++i)
                                            {
                                                var _index = _message.ReadUInt();
                                                var _side = (BattlePlayerSide)_message.ReadByte();

                                                var _profile = default(BattlePlayerProfile);
                                                _profile.Deserialize(ref _message);

                                                var _stats = default(BattlePlayerStats);
                                                _stats.Deserialize(ref _message);

                                                var missionResult = new ObserverBattleMissionResult
                                                {
                                                    player = _index,
                                                    profile = _profile,
                                                    stats = _stats,
                                                    battlefied = _battlefield,
                                                    winnerSide =(Legacy.Database.BattlePlayerSide)_result.winner,
                                                    mission = _campaign.mission,
                                                    tutorail = _campaign.tutorialIndex,
                                                    result = _result

                                                };
                                                var _entity = buffer.CreateEntity(entityInQueryIndex);
                                                buffer.AddComponent(entityInQueryIndex, _entity, missionResult);
                                            }

                                        }
                                        break;
                                    case ObserverGameMessage.ResultRating:
                                        {
                                            var _battlefield = _message.ReadUShort();

                                            var _result = default(BattleInstanceResult);
                                            _result.Deserialize(ref _message);

                                            var _players_count = _message.ReadByte();
                                            var raintingResult = new ObserverBattleRatingResult[_players_count];

                                            for (byte i = 0; i < _players_count; ++i)
                                            {
                                                var _index = _message.ReadUInt();
                                                var _side = (BattlePlayerSide)_message.ReadByte();

                                                var _profile = default(BattlePlayerProfile);
                                                _profile.Deserialize(ref _message);

                                                var _stats = default(BattlePlayerStats);
                                                _stats.Deserialize(ref _message);

                                                raintingResult[i] = new ObserverBattleRatingResult
                                                {
                                                    player = _index,
                                                    side = _side,
                                                    profile = _profile,
                                                    stats = _stats,
                                                    battlefied = _battlefield,
                                                    result = _result
                                                };
                                            }
                                            for (byte i = 0; i < _players_count; ++i)
                                            {
                                                byte enemyId = i == 0 ? (byte)1 : (byte)0;
                                                raintingResult[i].enemy = new BattleEnemy()
                                                {
                                                    name = raintingResult[enemyId].profile.name,
                                                };
                                                var _entity = buffer.CreateEntity(entityInQueryIndex);
                                                buffer.AddComponent(entityInQueryIndex, _entity, raintingResult[i]);
                                            }

                                        }
                                        break;
                                }
                                break;
                        }
                    }
                })
                .WithoutBurst()
                .Schedule(inputDeps);

            _barrier.AddJobHandleForProducer(inputDeps);

            return inputDeps;
        }
    }
}


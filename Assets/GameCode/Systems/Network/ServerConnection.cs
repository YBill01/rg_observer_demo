using System;
using System.Diagnostics;
using System.IO;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

/// <summary>
/// Открывает слушателя сокет (driver) на порту 6667 захардкожено
/// и дает публичный доступ для других систем, чтобы использовать этот драйвер
/// </summary>
namespace Legacy.Observer
{

    public class ServerConnection
    {

        private static ServerConnection _instance = null;
        public static ServerConnection Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new ServerConnection();
                }
                return _instance;
            }
        }

        internal void StartFromEditor()
        {
            //just create singleton
        }

        private NetworkDriver _driver;
        private NetworkDriver.Concurrent _driver_concurrent;
        private Entity driverEntity;

        public NetworkDriver Driver => _driver;
        public NetworkDriver.Concurrent DriverConcurrent => _driver_concurrent;

        private NetworkPipeline _reliable_pipeline;
        public NetworkPipeline ReliablePipeline => _reliable_pipeline;

        private NetworkPipeline _unreliable_pipeline;
        public NetworkPipeline UnreliablePipeline => _unreliable_pipeline;

        public ServerConnection()
        {
        //    Create();
        }

        public void Create()
        {
            
            var reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };

            _driver = NetworkDriver.Create(reliabilityParams);

            _unreliable_pipeline = _driver.CreatePipeline(typeof(NullPipelineStage));
            _reliable_pipeline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            var _addres = NetworkEndPoint.AnyIpv4;
            _addres.Port = 6669;
            if (_driver.Bind(_addres) != 0)
                throw new Exception("Failed to bind to port: " + _addres.Port);
            else
            {
                UnityEngine.Debug.Log(string.Format("Game Host: {0} ", _addres.Port));
                _driver.Listen();
            }

            _driver_concurrent = _driver.ToConcurrent();
            driverEntity = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity();
            World.DefaultGameObjectInjectionWorld.EntityManager.AddComponent<ServerNetworkDriver>(driverEntity);
        }

        public void StartNewServer()
        {
            try
            {
                string path = Directory.GetCurrentDirectory();

                Process _battle = new Process();

                _battle.StartInfo.UseShellExecute = true;
                _battle.StartInfo.WorkingDirectory = Path.Combine(path, @"../server");
#if UNITY_EDITOR
                _battle.StartInfo.WorkingDirectory = Path.Combine(path, @"../../builds/windows/Server");
                _battle.StartInfo.FileName = Path.Combine(_battle.StartInfo.WorkingDirectory, @"legacy_server.exe");
#else
#if UNITY_STANDALONE_LINUX
				_battle.StartInfo.FileName = Path.Combine(_battle.StartInfo.WorkingDirectory, @"ringrage_battleserver.x86_64");
#endif
#endif
                _battle.EnableRaisingEvents = true;
                _battle.Start();
                GameDebug.Log($"Procces started.");
                GameDebug.Log($"Procces info:");
                GameDebug.Log($"{_battle}");
            }
            catch (Exception error)
            {
                GameDebug.LogError("Battle Process Create Error: " + error.Message);
            }
        }    

        public void Dispose()
        {
            if (_driver.IsCreated)
            {
                _driver.Dispose();
                World.DefaultGameObjectInjectionWorld.EntityManager.DestroyEntity(driverEntity);
            }
        }
    }
}


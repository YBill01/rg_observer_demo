using Legacy.Database;
using Unity.Entities;
using UnityEngine;
using UnityEngine.LowLevel;

public sealed class Bootstrap : MonoBehaviour
{

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	public static void Initialize()
	{
		Application.targetFrameRate = 60;
	}

	public class ServerBootstrap : ICustomBootstrap
	{
		public bool Initialize(string defaultWorldName)
		{
            ConfigVar.Load();
            TypeManager.Initialize();
			var world = new World(defaultWorldName);
			World.DefaultGameObjectInjectionWorld = world;
			
			ObserverCommandLine.CheckCommandLine();
			
			/*if (ObserverCommandLine.TryGetCommandLineArgValue("db", out string value))
			{
				DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, new List<Type>());

				Debug.Log("db=" + value);
				try
				{
					var _instance = new BinaryDatabaseWriter();
					_instance.Write(string.Format("mongodb://{0}:27017", value));
					//mongodb://88.99.198.202:27017
				}
				catch (Exception error)
				{
					UnityEngine.Debug.Log("error=" + error.Message);
				}
#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPlaying = false;
#else
				Application.Quit();
#endif
			} else*/
			{
				var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
				DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
			}

			ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);

			return true;
		}
	}
}
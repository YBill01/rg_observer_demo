using System;
using System.ComponentModel;
using UnityEngine;

namespace Legacy.Database
{
    public class ObserverCommandLine
    {
        public static bool TryGetCommandLineArgValue<T>(string argName, out T value)
        {
            value = default;
            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (!converter.CanConvertFrom(typeof(string)))
                    return false;

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Compare(args[i], argName, StringComparison.InvariantCultureIgnoreCase) != 0 ||
                        args.Length <= i + 1)
                        continue;

                    value = (T) converter.ConvertFromString(args[i + 1]);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void CheckCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();

            bool isUpdate = false;
            string dbIp = "88.99.198.202";
            string dbPort = "27017";
            bool shutdown = false;

            string key = null;
            string value = null;

            foreach (string arg in args)
            {
                string[] parts = arg.Split('=');

                if (parts.Length == 2)
                {
                    key = parts[0];
                    value = parts[1];
                }
                else
                {
                    key = arg;
                    value = null;
                }

                switch (key)
                {
                    case "-updateDB":
                        isUpdate = true;
                        break;

                    case "-shutdown":
                        shutdown = true;
                        break;

                    case "-dbIp":
                        if (value != null)
                        {
                            dbIp = value;
                        }

                        break;
                    case "-dbPort":
                        if (value != null)
                        {
                            dbPort = value;
                        }

                        break;
                }
            }

            if (isUpdate)
            {
                Debug.Log("----------------> Update:");
                Debug.Log($"----------------> {Environment.CurrentDirectory}");
                Debug.Log($"----------------> (mongodb://{dbIp}:{dbPort})");

                BinaryDatabase.Instance.Dispose();
                var instance = new BinaryDatabaseWriter();
                instance.Write($"mongodb://{dbIp}:{dbPort}", WriteType.Base);

                if (shutdown)
                {
                    Application.Quit();
                }
            }
        }
    }
}
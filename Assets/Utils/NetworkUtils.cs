using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Legacy.Network
{

	public class NetworkUtils
	{
		public static uint Counter = 0;

		static public ushort NextOpenPort(ushort from)
		{
			Process _netstat = new Process();
			_netstat.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			_netstat.StartInfo.CreateNoWindow = true;
			_netstat.StartInfo.RedirectStandardOutput = true;
			_netstat.StartInfo.UseShellExecute = false;
			_netstat.StartInfo.FileName = "netstat";
#if UNITY_STANDALONE_WIN
			_netstat.StartInfo.Arguments = "-pq UDP";
#elif UNITY_STANDALONE_LINUX
			//TODO: Arguments ?????
			_netstat.StartInfo.Arguments = "-lu";
#endif
			_netstat.EnableRaisingEvents = true;
			_netstat.Start();
			_netstat.WaitForExit();

			var _used_ports = new List<uint>();
			string _result = _netstat.StandardOutput.ReadToEnd();
#if UNITY_STANDALONE_WIN
			var rgx = new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:(\d{4,5})");
#elif UNITY_STANDALONE_LINUX
            var rgx = new Regex(@"\*:(\d{4})");
#elif UNITY_STANDALONE_OSX
            var rgx = new Regex(@"\*:(\d{4})");
#endif
            foreach (Match match in rgx.Matches(_result))
			{
				_used_ports.Add( uint.Parse(match.Groups[1].Value) );
			}

			while (_used_ports.Contains(from++))
			{

			}

			return (ushort)(from - 1);
		}


		unsafe static public void ConvertObject(object value, Type type, void* point)
		{

            switch (Type.GetTypeCode(type))
			{
				case TypeCode.Boolean:
				case TypeCode.Byte:
					var _byte = Convert.ToByte(value);
					UnsafeUtility.MemCpy(point, &_byte, 1);
					break;
				case TypeCode.UInt16:
					var _uint16 = Convert.ToUInt16(value);
					UnsafeUtility.MemCpy(point, &_uint16, 2);
					break;
				case TypeCode.Int16:
					var _int16 = Convert.ToInt16(value);
					UnsafeUtility.MemCpy(point, &_int16, 2);
					break;
				case TypeCode.UInt32:
					var _uint32 = Convert.ToUInt32(value);
					UnsafeUtility.MemCpy(point, &_uint32, 4);
					break;
                case TypeCode.UInt64:
                    var _uint64 = Convert.ToUInt64(value);
                    UnsafeUtility.MemCpy(point, &_uint64, 8);
                    break;
                case TypeCode.Int32:
					var _int32 = Convert.ToInt32(value);
					UnsafeUtility.MemCpy(point, &_int32, 4);
					break;
                case TypeCode.Int64:
                    var _int64 = Convert.ToInt64(value);
                    UnsafeUtility.MemCpy(point, &_int64, 8);
                    break;
                case TypeCode.Single:
					var _single = Convert.ToSingle(value);
					UnsafeUtility.MemCpy(point, &_single, 4);
					break;
            }
		}

	}
}
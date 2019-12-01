using System;
using System.Net;

namespace NetworkCore
{
	public static class Tools
	{
		public static IPEndPoint BuildIpEndPoint(string ip, ushort port)
		{
			IPAddress ipAddr;

			switch(ip)
			{
				case "any":
					ipAddr = IPAddress.Any;
					break;
				case "localhost":
					ipAddr = IPAddress.Loopback;
					break;
				default:
				{
					if(!IPAddress.TryParse(ip, out ipAddr))
					{
						throw new FormatException($"Listening IP address ({ip}) is defined in wrong format.");
					}

					break;
				}
			}

			return new IPEndPoint(ipAddr, port);
		}
	}
}
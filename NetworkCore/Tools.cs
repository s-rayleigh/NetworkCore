using System;
using System.Net;

namespace NetworkCore;

public static class Tools
{
	public static IPEndPoint BuildIpEndPoint(string ip, ushort port)
	{
		IPAddress ipAddress;

		switch(ip)
		{
			case "any":
				ipAddress = IPAddress.Any;
				break;
			case "localhost":
				ipAddress = IPAddress.Loopback;
				break;
			default:
			{
				if(!IPAddress.TryParse(ip, out ipAddress))
				{
					throw new FormatException($"Listening IP address ({ip}) is defined in wrong format.");
				}

				break;
			}
		}

		return new(ipAddress, port);
	}
}
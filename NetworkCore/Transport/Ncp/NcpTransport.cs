using System;
using System.Net.Sockets;

namespace NetworkCore.Transport.Ncp;

public abstract class NcpTransport
{
	internal const int MinPacketSize = 4;
	
	protected readonly Socket socket;

	public required short ProtocolId { get; init; }

	public TimeSpan SendInterval { get; init; } = TimeSpan.FromMilliseconds(42);
	
	public ushort Mtu { get; init; } = 1500;
	
	protected NcpTransport()
	{
		this.socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
	}
}
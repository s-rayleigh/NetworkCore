using System.Net.Sockets;

namespace NetworkCore.Transport.Ncp;

public abstract class NcpTransport
{
	protected readonly Socket socket;

	public ushort Mtu { get; set; } = 1500;
	
	protected NcpTransport()
	{
		this.socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
	}
	
	
}
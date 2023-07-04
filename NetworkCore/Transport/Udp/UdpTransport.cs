using System.Net.Sockets;

namespace NetworkCore.Transport.Udp;

public abstract class UdpTransport
{
	protected readonly Socket socket;

	public ushort Mtu { get; set; } = 1500;
	
	protected UdpTransport()
	{
		this.socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
	}
	
	
}
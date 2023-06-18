using System.Net.Sockets;

namespace NetworkCore.Transport.Tcp;

public abstract class TcpTransport
{
	protected readonly Socket socket;
	
	public ushort ReceiveBufferSize { get; set; } = 1024;
	
	/// <summary>
	/// Set true to disable the use of the Nagle algorithm, which will increase packet flooding but reduce send latency.
	/// The default is false.
	/// </summary>
	public bool NoDelay
	{
		get => this.socket.NoDelay;
		set => this.socket.NoDelay = value;
	}

	protected TcpTransport()
	{
		this.socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
		{
			ExclusiveAddressUse = true
		};
	}
}
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkCore.Transport.Udp;

public class UdpServerTransport : UdpTransport, IServerTransport
{
	public event Action<int, IPEndPoint> ClientConnected;
	
	public event Action<Exception> ConnectionAcceptError;
	
	public event Action<int, DisconnectType> ClientDisconnected;
	
	public event Action<int, byte[]> RawMsgReceived;
	
	public event Action<int, Exception> MsgReceiveError;
	
	public Task Listen(IPEndPoint ipEndPoint, CancellationToken cancellationToken)
	{
		this.socket.Bind(ipEndPoint);
		throw new NotImplementedException();
	}

	public Task SendRawMsg(int clientId, byte[] msg)
	{
		throw new NotImplementedException();
	}

	public Task Disconnect(int clientId)
	{
		throw new NotImplementedException();
	}
}
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkCore.Transport;

public interface IServerTransport
{
	event Action<int, IPEndPoint> ClientConnected;

	event Action<Exception> ConnectionAcceptError;
	
	event Action<int, DisconnectType> ClientDisconnected;

	event Action<int, byte[]> RawMsgReceived;

	event Action<int, Exception> MsgReceiveError;
	
	Task Listen(IPEndPoint ipEndPoint, CancellationToken cancellationToken);

	Task SendRawMsg(int clientId, byte[] msg);

	Task Disconnect(int clientId);
}
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkCore.Transport;

public interface IClientTransport
{
	event Action<byte[]> RawMsgReceived;
	
	event Action<Exception> MsgReceiveError;

	event Action<DisconnectType> Disconnected;
	
	Task Connect(IPEndPoint ipEndPoint, CancellationToken cancellationToken);

	Task Disconnect();

	Task SendRawMsg(byte[] msg);
}
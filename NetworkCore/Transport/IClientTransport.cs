using System;
using System.Net;
using System.Threading.Tasks;

namespace NetworkCore.Transport;

public interface IClientTransport
{
	Task Connect(IPEndPoint ipEndPoint);

	Task Disconnect();

	Task SendRawMsg(byte[] msg);

	event Action<byte[]> RawMsgReceived;
	
	event Action<Exception> MsgReceiveError;

	event Action<DisconnectType> Disconnected;
}
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkCore.Transport.Udp;

public class UdpClientTransport : UdpTransport, IClientTransport
{
	public event Action<byte[]> RawMsgReceived;
	
	public event Action<Exception> MsgReceiveError;
	
	public event Action<DisconnectType> Disconnected;
	
	public Task Connect(IPEndPoint ipEndPoint)
	{
		throw new NotImplementedException();
	}

	public Task Disconnect()
	{
		throw new NotImplementedException();
	}

	public Task SendRawMsg(byte[] msg)
	{
		// TODO: if(reliable) queue.Add(msg) else socket.SendToAsync(msg)
		// this.socket.SendToAsync()
		throw new NotImplementedException();
	}
}
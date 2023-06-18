using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkCore.Transport.Tcp;

public class TcpClientTransport : TcpTransport, IClientTransport
{
	private TcpRemoteHost tcpRemoteHost;

	public event Action<byte[]> RawMsgReceived;
	
	public event Action<Exception> MsgReceiveError;

	public event Action<DisconnectType> Disconnected;
	
	public async Task Connect(IPEndPoint ipEndPoint)
	{
		await this.socket.ConnectAsync(ipEndPoint).ConfigureAwait(false);
		this.tcpRemoteHost = new(this.socket);
		
		this.tcpRemoteHost.RawMsgReceived += msg => this.RawMsgReceived?.Invoke(msg);
		this.tcpRemoteHost.MsgReceiveError += e => this.MsgReceiveError?.Invoke(e);
		this.tcpRemoteHost.Disconnected += type => this.Disconnected?.Invoke(type);
		
		this.tcpRemoteHost.RunReceiveTask(this.ReceiveBufferSize);
	}
	
	public Task SendRawMsg(byte[] msg) => this.tcpRemoteHost.SendRawMsg(msg);

	public Task Disconnect() => this.tcpRemoteHost.Disconnect();
}
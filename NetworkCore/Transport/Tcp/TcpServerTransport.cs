using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkCore.Transport.Tcp;

public class TcpServerTransport : TcpTransport, IServerTransport
{
	private int lastClientId;

	private readonly ConcurrentDictionary<int, TcpRemoteHost> clients;

	/// <summary>
	/// The maximum length of the pending connections queue.
	/// </summary>
	public int ConnectionQueueLength { get; set; } = 128;

	public event Action<int, IPEndPoint> ClientConnected;
	
	public event Action<Exception> ConnectionAcceptError;

	public event Action<int, DisconnectType> ClientDisconnected;
	
	public event Action<int, byte[]> RawMsgReceived;
	
	public event Action<int, Exception> MsgReceiveError;

	public TcpServerTransport()
	{
		this.clients = new();
	}
	
	public Task Listen(IPEndPoint ipEndPoint, CancellationToken cancellationToken = default)
	{
		this.socket.Bind(ipEndPoint);
		this.socket.Listen(this.ConnectionQueueLength); 
		
		var acceptTask = Task.Run(async () =>
		{
			while(!cancellationToken.IsCancellationRequested)
			{
				Socket clientSocket = null;

				try
				{
					clientSocket = await this.socket.AcceptAsync().ConfigureAwait(false);
				}
				catch(SocketException e) when(e.SocketErrorCode is SocketError.OperationAborted)
				{
					// Stop accepting new connections because the socket is closed during
					// the connection accept operation.
					return;
				}
				catch(ObjectDisposedException)
				{
					// Stop accepting new connections because the socket was closed.
					return;
				}
				catch(TimeoutException)
				{
					continue;
				}
				catch(Exception e)
				{
					this.ConnectionAcceptError?.Invoke(e);
				}

				if(clientSocket is not null)
				{
					var clientId = Interlocked.Increment(ref this.lastClientId);
					var remoteHost = new TcpRemoteHost(clientSocket);
					
					remoteHost.RawMsgReceived += msg => this.RawMsgReceived?.Invoke(clientId, msg);
					remoteHost.MsgReceiveError += e => this.MsgReceiveError?.Invoke(clientId, e);
					remoteHost.Disconnected += type =>
					{
						this.clients.TryRemove(clientId, out _);
						this.ClientDisconnected?.Invoke(clientId, type);
					};
					
					this.clients[clientId] = remoteHost;
					this.ClientConnected?.Invoke(clientId, (IPEndPoint)clientSocket.RemoteEndPoint);
					
					remoteHost.RunReceiveTask(this.ReceiveBufferSize);
				}
			}
		}, cancellationToken);

		cancellationToken.Register(() =>
		{
			try
			{
				this.socket.Shutdown(SocketShutdown.Both);
			}
			catch(SocketException)
			{
				// ignored.
			}

			this.socket.Close();
		});
		
		return acceptTask;
	}

	public Task SendRawMsg(int clientId, byte[] msg)
	{
		if(!this.clients.TryGetValue(clientId, out var remoteHost))
		{
			throw new InvalidOperationException("Client not connected.");
		}

		return remoteHost.SendRawMsg(msg);
	}

	public Task Disconnect(int clientId)
	{
		if(!this.clients.TryGetValue(clientId, out var remoteHost))
		{
			throw new InvalidOperationException("Client not connected.");
		}

		return remoteHost.Disconnect();
	}
}
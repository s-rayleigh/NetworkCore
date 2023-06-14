using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;
using NetworkCore.Handling;

namespace NetworkCore.Server;

/// <summary>
/// Connection listener on the server.
/// </summary>
[PublicAPI]
public class Listener
{
	private readonly Socket socket;
		
	/// <summary>
	/// Message dispatchers used to route incoming messages.
	/// </summary>
	private IMsgDispatcher<Peer>[] msgDispatchers;
		
	/// <summary>
	/// Data model for this listener.
	/// </summary>
	public DataModel Model { get; set; }

	public ushort ReceiveBufferSize { get; set; } = 1024;
		
	/// <summary>
	/// Set to true to disable the use of Nagle algorithm, which will increase packet flooding but reduce send latency.
	/// The default is false.
	/// </summary>
	public bool NoDelay
	{
		get => this.socket.NoDelay;
		set => this.socket.NoDelay = value;
	}

	public EndPoint EndPoint => this.socket.LocalEndPoint;

	#region Events

	/// <summary>
	/// Fired when a peer connected.
	/// </summary>
	public event Action<Peer> PeerConnected;

	/// <summary>
	/// Fired when a peer disconnected.
	/// </summary>
	public event Action<Peer, DisconnectType> PeerDisconnected;

	/// <summary>
	/// Fired when a message is received from the peer.
	/// </summary>
	public event Action<Message, Peer> MessageReceived;
 
	/// <summary>
	/// Fired when an error occured while trying to receive a message.
	/// </summary>
	public event Action<Exception, Peer> MessageReceiveError;
	
	/// <summary>
	/// Fired when error occurs while accepting connections.
	/// </summary>
	public event Action<Exception> ConnectionAcceptError;

	/// <summary>
	/// Fired when an exception is occured in one of the message dispatchers.
	/// </summary>
	public event Action<Exception> DispatcherException;
		
	#endregion

	public Listener(IEnumerable<IMsgDispatcher<Peer>> dispatchers = null)
	{
		this.socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
		{
			ExclusiveAddressUse = true
		};
		
		this.msgDispatchers = dispatchers?.ToArray() ?? Array.Empty<IMsgDispatcher<Peer>>();
	}

	public Listener(IPEndPoint endPoint, IEnumerable<IMsgDispatcher<Peer>> dispatchers = null) : this(dispatchers) =>
		this.Bind(endPoint);

	public Listener(string ip, ushort port, IEnumerable<IMsgDispatcher<Peer>> dispatchers = null) 
		: this(Tools.BuildIpEndPoint(ip, port), dispatchers) { }

	public Listener Bind(IPEndPoint endPoint)
	{
		this.socket.Bind(endPoint);
		return this;
	}
		
	public Listener Bind(string ip, ushort port) => this.Bind(Tools.BuildIpEndPoint(ip, port));

	public Task Listen(ushort queueLength, CancellationToken cancellationToken = default)
	{
		// TODO: prevent multiple calls to this method (bool variable check in lock).
		
		// Create empty data model in case if no model is set.
		this.Model ??= new();
		
		this.socket.Listen(queueLength);

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
					var peer = new Peer(clientSocket, this.Model);
					peer.Disconnected += type => this.PeerDisconnected?.Invoke(peer, type);
					peer.RawMessageReceived += msgBytes =>
					{
						var message = this.Model.Deserialize(msgBytes); // TODO: catch deserialization exception.

						this.MessageReceived?.Invoke(message, peer);

						for(var i = 0; i < this.msgDispatchers.Length; i++)
						{
							try
							{
								this.msgDispatchers[i]?.DispatchMessage(message, peer);
							}
							catch(Exception e)
							{
								this.DispatcherException?.Invoke(e);
							}
						}
					};
					peer.MessageReceiveError += e => this.MessageReceiveError?.Invoke(e, peer);
					this.PeerConnected?.Invoke(peer);
					peer.RunReceiveTask(this.ReceiveBufferSize);
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
}
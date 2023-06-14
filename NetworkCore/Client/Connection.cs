using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;
using NetworkCore.Handling;

namespace NetworkCore.Client;

/// <summary>
/// Connection to the remote host.
/// </summary>
[PublicAPI]
public class Connection
{
	private IPEndPoint endPoint;
	
	private readonly Socket socket;

	/// <summary>
	/// Message dispatchers used to route incoming messages.
	/// </summary>
	private IMsgDispatcher<Peer>[] msgDispatchers;

	public DataModel Model { get; set; }

	/// <summary>
	/// A peer that represents the server on the client side.
	/// </summary>
	public Peer Peer { get; private set; }

	public ushort ReceiveBufferSize { get; set; } = 1024;
		
	/// <summary>
	/// Set to true to disable the use of Nagle algorithm, which will increase packet flooding but reduce send
	/// latency. The default is false.
	/// </summary>
	public bool NoDelay
	{
		get => this.socket.NoDelay;
		set => this.socket.NoDelay = value;
	}

	#region Events

	/// <summary>
	/// Fired when the client connected to the server.
	/// </summary>
	public event Action<Peer> Connected;

	/// <summary>
	/// Fired when the client disconnects from the server.
	/// </summary>
	public event Action<DisconnectType> Disconnected;

	public event Action<Exception> ConnectionError;

	public event Action<Exception> MessageReceiveError;

	/// <summary>
	/// Fired when a message is received from the server.
	/// </summary>
	public event Action<Message> MessageReceived;

	/// <summary>
	/// Fired when an exception is occured in one of the message dispatchers.
	/// </summary>
	public event Action<Exception> DispatcherException;
	
	#endregion
	
	public Connection(IEnumerable<IMsgDispatcher<Peer>> dispatchers = null)
	{
		this.socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
		{
			ExclusiveAddressUse = true
		};

		this.msgDispatchers = dispatchers?.ToArray() ?? Array.Empty<IMsgDispatcher<Peer>>();
	}

	public Connection(string ip, ushort port, IEnumerable<IMsgDispatcher<Peer>> dispatchers = null) 
		: this(Tools.BuildIpEndPoint(ip, port), dispatchers) { }

	public Connection(IPEndPoint endPoint, IEnumerable<IMsgDispatcher<Peer>> dispatchers = null)
		: this(dispatchers) => this.endPoint = endPoint;
	
	public Connection Bind(IPEndPoint ep)
	{
		if(this.socket.Connected)
		{
			throw new InvalidOperationException("Cannot bind connection after the socket is connected.");
		}
			
		this.endPoint = ep;
		return this;
	}
		
	public Connection Bind(string ip, ushort port)
	{
		this.Bind(Tools.BuildIpEndPoint(ip, port));
		return this;
	}
	
	/// <summary>
	/// Connects to the remote host.
	/// </summary>
	/// <returns>Peer that represents remote host.</returns>
	/// <exception cref="InvalidOperationException">If client already connected.</exception>
	public async Task<Peer> Connect()
	{
		if(this.socket.Connected) throw new InvalidOperationException("Already connected.");

		if(this.endPoint is null)
		{
			throw new InvalidOperationException("Binding to ip address and port is required to connect.");
		}
		
		this.Model ??= new();

		try
		{
			await this.socket.ConnectAsync(this.endPoint).ConfigureAwait(false);
		}
		catch(Exception e) when (e is SocketException or InvalidOperationException)
		{
			this.ConnectionError?.Invoke(e);
			throw;
		}
		
		this.Peer = new(this.socket, this.Model);
		this.Peer.Disconnected += type => this.Disconnected?.Invoke(type);
		this.Peer.MessageReceiveError += e => this.MessageReceiveError?.Invoke(e);
		this.Peer.RawMessageReceived += msgBytes =>
		{
			var message = this.Model.Deserialize(msgBytes); // TODO: handle 'failed to deserialize'
			this.MessageReceived?.Invoke(message);

			for(var i = 0; i < this.msgDispatchers.Length; i++)
			{
				try
				{
					this.msgDispatchers[i]?.DispatchMessage(message, this.Peer);
				}
				catch(Exception e)
				{
					this.DispatcherException?.Invoke(e);
				}
			}
		};

		this.Peer.RunReceiveTask(this.ReceiveBufferSize);
		
		this.Connected?.Invoke(this.Peer);

		return this.Peer;
	}

	/// <summary>
	/// Disconnects client from the server.
	/// </summary>
	public Task Disconnect() => this.Peer.Disconnect();
}
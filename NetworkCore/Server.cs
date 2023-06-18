using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;
using NetworkCore.Handling;
using NetworkCore.Transport;

namespace NetworkCore;

/// <summary>
/// Connection listener on the server side.
/// </summary>
[PublicAPI]
public class Server
{
	private readonly IServerTransport transport;

	private ConcurrentDictionary<int, Peer> peers;

	/// <summary>
	/// Message dispatchers used to route incoming messages.
	/// </summary>
	private IMsgDispatcher[] msgDispatchers;
		
	/// <summary>
	/// Data model for this listener.
	/// </summary>
	public DataModel Model { get; set; }

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

	public Server(IServerTransport transport, IEnumerable<IMsgDispatcher> dispatchers = null)
	{
		this.peers = new();
		this.transport = transport;
		this.msgDispatchers = dispatchers?.ToArray() ?? Array.Empty<IMsgDispatcher>();

		this.transport.ClientConnected += (clientId, ipEndPoint) =>
		{
			var peer = new Peer(ipEndPoint);

			peer.WantsSendMessage += msg =>
			{
				peer.LastSend = DateTime.UtcNow;
				// TODO: catch possible serialization exception.
				var bytes = this.Model.Serialize(msg);
				return transport.SendRawMsg(clientId, bytes);
			};

			peer.WantsDisconnect += () => this.transport.Disconnect(clientId);
			
			this.peers[clientId] = peer;
			this.PeerConnected?.Invoke(peer);
		};

		this.transport.ClientDisconnected += (clientId, type) =>
		{
			this.peers.TryRemove(clientId, out var peer);
			this.PeerDisconnected?.Invoke(peer, type);
		};

		this.transport.RawMsgReceived += (clientId, msgBytes) =>
		{
			if(!this.peers.TryGetValue(clientId, out var peer)) return;

			peer.LastReceive = DateTime.UtcNow;
			
			// TODO: catch deserialization exception.
			var msg = this.Model.Deserialize(msgBytes);

			this.MessageReceived?.Invoke(msg, peer);

			for(var i = 0; i < this.msgDispatchers.Length; i++)
			{
				try
				{
					this.msgDispatchers[i]?.DispatchMessage(msg, peer);
				}
				catch(Exception e)
				{
					this.DispatcherException?.Invoke(e);
				}
			}
		};

		this.transport.MsgReceiveError += (clientId, e) =>
		{
			if(!this.peers.TryGetValue(clientId, out var peer)) return;
			this.MessageReceiveError?.Invoke(e, peer);
		};
	}

	public Task Listen(IPEndPoint ipEndPoint, CancellationToken cancellationToken = default) =>
		this.transport.Listen(ipEndPoint, cancellationToken);
}
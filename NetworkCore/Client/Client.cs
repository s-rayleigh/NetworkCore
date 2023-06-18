using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;
using NetworkCore.Handling;
using NetworkCore.Transport;

namespace NetworkCore.Client;

/// <summary>
/// Connection to the remote host.
/// </summary>
[PublicAPI]
public class Client
{
	private readonly IClientTransport transport;

	/// <summary>
	/// Message dispatchers used to route incoming messages.
	/// </summary>
	private IMsgDispatcher[] msgDispatchers;

	public DataModel Model { get; set; }

	/// <summary>
	/// A peer that represents the server on the client side.
	/// </summary>
	[CanBeNull]
	public Peer Peer { get; private set; }

	#region Events

	/// <summary>
	/// Fired when the client disconnects from the server.
	/// </summary>
	public event Action<DisconnectType> Disconnected;
	
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
	
	public Client(IClientTransport transport, IEnumerable<IMsgDispatcher> dispatchers = null)
	{
		this.transport = transport;
		this.transport.Disconnected += type => this.Disconnected?.Invoke(type);
		this.transport.MsgReceiveError += e => this.MessageReceiveError?.Invoke(e);
		this.transport.RawMsgReceived += msgBytes =>
		{
			this.Peer!.LastReceive = DateTime.UtcNow;
			
			// TODO: handle 'failed to deserialize'
			var message = this.Model.Deserialize(msgBytes);
			
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
		this.msgDispatchers = dispatchers?.ToArray() ?? Array.Empty<IMsgDispatcher>();
	}

	/// <summary>
	/// Connects to the server.
	/// </summary>
	/// <returns>Peer that represents the server.</returns>
	/// <exception cref="InvalidOperationException">If client already connected.</exception>
	public async Task<Peer> Connect(IPEndPoint ipEndPoint)
	{
		if(ipEndPoint is null) throw new ArgumentNullException(nameof(ipEndPoint));
		this.Model ??= new();

		await this.transport.Connect(ipEndPoint);

		this.Peer = new(ipEndPoint);
		this.Peer.WantsDisconnect += () => this.transport.Disconnect();
		this.Peer.WantsSendMessage += msg =>
		{
			this.Peer.LastSend = DateTime.UtcNow;
			// TODO: catch possible serialization exception.
			var bytes = this.Model.Serialize(msg);
			return this.transport.SendRawMsg(bytes);
		};

		return this.Peer;
	}

	/// <summary>
	/// Disconnects client from the server.
	/// </summary>
	public Task Disconnect() => this.Peer?.Disconnect();
}
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
	private IMsgDispatcher<Client>[] msgDispatchers;
		
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

	#region Delegates

	public delegate void MessageHandler(Message message, Client client);

	public delegate void ErrorHandler(ListenerError errorType, Exception exception);

	#endregion

	#region Events

	/// <summary>
	/// Client connected event.
	/// </summary>
	public event ClientHandler ClientConnected;

	/// <summary>
	/// Client disconnected event.
	/// </summary>
	public event Action<Client, DisconnectType> ClientDisconnected;

	/// <summary>
	/// Fired when a message is received from the client.
	/// </summary>
	public event MessageHandler MessageReceived;
 
	/// <summary>
	/// Fired when internal error occurs.
	/// </summary>
	public event ErrorHandler ErrorOccurred;

	/// <summary>
	/// Fired when an exception is occured in one of the message dispatchers.
	/// </summary>
	public event Action<Exception> DispatcherException;
		
	#endregion

	public Listener(IEnumerable<IMsgDispatcher<Client>> dispatchers = null)
	{
		this.socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
		{
			ExclusiveAddressUse = true
		};
			
		this.msgDispatchers = dispatchers?.ToArray() ?? Array.Empty<IMsgDispatcher<Client>>();
	}

	public Listener(IPEndPoint endPoint, IEnumerable<IMsgDispatcher<Client>> dispatchers = null) : this(dispatchers) =>
		this.Bind(endPoint);

	public Listener(string ip, ushort port, IEnumerable<IMsgDispatcher<Client>> dispatchers = null) 
		: this(Tools.BuildIpEndPoint(ip, port), dispatchers) { }

	public Listener Bind(IPEndPoint endPoint)
	{
		this.socket.Bind(endPoint);
		return this;
	}
		
	public Listener Bind(string ip, ushort port) => this.Bind(Tools.BuildIpEndPoint(ip, port));

	public Task BeginListening(ushort queueLength, CancellationToken stopToken = default)
	{
		// Create empty data model in case if no model is set.
		this.Model ??= new();
			
		this.socket.Listen(queueLength);

		var acceptTask = Task.Run(async () =>
		{
			while(true)
			{
				Socket clientSocket = null;

				try
				{
					clientSocket = await this.socket.AcceptAsync().ConfigureAwait(false);
				}
				catch(ObjectDisposedException)
				{
					throw;
				}
				catch(TimeoutException) { }
				catch
				{
					// ignored.
				}

				if(clientSocket is not null) this.RunReceiveTask(clientSocket, stopToken);
				if(stopToken.IsCancellationRequested) break;
			}
		}, stopToken);

		// We cannot cancel AcceptTask so end task immediately after cancel received.
		return Task.WhenAny(acceptTask, Task.Run(async () =>
		{
			await Task.Delay(-1, stopToken).ConfigureAwait(false);
			this.socket.Shutdown(SocketShutdown.Both);
			this.socket.Close(1000);
		}));
	}

	private void RunReceiveTask(Socket clientSocket, CancellationToken stopToken) => Task.Run(async () =>
	{
		var client = new Client(clientSocket, this.Model, this.ReceiveBufferSize);

		this.ClientConnected?.Invoke(client);

		// Handle client disconnect while sending a message.
		client.DisconnectedInternal += delegate(Client c)
		{
			c.CloseSocket();
			this.ClientDisconnected?.Invoke(c, DisconnectType.SendError);
		};

		var manualDisconnectToken = client.DisconnectToken;

		while(!(stopToken.IsCancellationRequested || manualDisconnectToken.IsCancellationRequested))
		{
			int bytesRead;

			try
			{
				bytesRead = await client.Socket
					.ReceiveAsync(new ArraySegment<byte>(client.Buffer.Data), SocketFlags.None)
					.ConfigureAwait(false);
			}
			catch(ObjectDisposedException e)
			{
				this.ErrorOccurred?.Invoke(ListenerError.SocketClosed, e);
				this.ClientDisconnected?.Invoke(client, DisconnectType.ReceiveError);
				return;
			}
			catch(SocketException e)
			{
				client.CloseSocket();
				this.ErrorOccurred(ListenerError.SocketError, e);
				this.ClientDisconnected?.Invoke(client, DisconnectType.ReceiveError);
				return;
			}
			catch(Exception e)
			{
				client.CloseSocket();
				this.ErrorOccurred?.Invoke(ListenerError.Unknown, e);
				this.ClientDisconnected?.Invoke(client, DisconnectType.ReceiveError);
				return;
			}

			// Detect client disconnect.
			if(bytesRead <= 0)
			{
				client.CloseSocket();
				this.ClientDisconnected?.Invoke(client, DisconnectType.Normal);
				return;
			}

			try
			{
				client.Buffer.TryReceive(bytesRead);
			}
			catch(ProtocolViolationException e)
			{
				// Buffer corrupted. Disconnecting client.
				client.CloseSocket();
				this.ClientDisconnected?.Invoke(client, DisconnectType.BufferError);
				this.ErrorOccurred?.Invoke(ListenerError.BufferCorrupted, e);
				return;
			}

			// Handle messages in the queue.
			while(client.Buffer.TryGetMsgBytes(out var messageBytes))
			{
				var message = this.Model.Deserialize(messageBytes); // TODO: catch deserialization exception.

				// Update last data receive time.
				client.LastDataReceive = DateTime.UtcNow;

				this.MessageReceived?.Invoke(message, client);

				for(var i = 0; i < this.msgDispatchers.Length; i++)
				{
					try
					{
						this.msgDispatchers[i]?.DispatchMessage(message, client);
					}
					catch(Exception e)
					{
						this.DispatcherException?.Invoke(e);
					}
				}
			}
		}
	}, stopToken);
}
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;
using NetworkCore.Extensions;

namespace NetworkCore.Server
{
	/// <summary>
	/// Connection listener on the server.
	/// </summary>
	[PublicAPI]
	public class Listener
	{
		/// <summary>
		/// Message dispatcher used to direct incoming messages to corresponding handler.
		/// </summary>
		public MessageDispatcher Dispatcher { get; set; }

		public bool DispatchAsync { get; set; } = true;
		
		/// <summary>
		/// Data model for this listener.
		/// </summary>
		public DataModel Model { get; set; }

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

		public EndPoint EndPoint => this.socket.LocalEndPoint;
		
		private readonly Socket socket;

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

		public event Action<Exception> DispatcherException;
		
		#endregion

		public Listener()
		{
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				ExclusiveAddressUse = true
			};
		}

		public Listener(IPEndPoint endPoint) : this() => this.Bind(endPoint);

		public Listener(string ip, ushort port) : this(Tools.BuildIpEndPoint(ip, port)) { }

		public Listener Bind(IPEndPoint endPoint)
		{
			this.socket.Bind(endPoint);
			return this;
		}
		
		public Listener Bind(string ip, ushort port) => this.Bind(Tools.BuildIpEndPoint(ip, port));

		// TODO: detailed description
		public Task BeginListening(ushort queueLength, CancellationToken stopToken = default)
		{
			// Create empty data model in case if no model is set.
			if(this.Model is null) this.Model = new DataModel();
			
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
						// ignored
					}

					if(clientSocket != null)
					{
						this.RunReceiveTask(clientSocket, stopToken);
					}

					if(stopToken.IsCancellationRequested)
					{
						break;
					}
				}
			}, stopToken);

			// We cannot cancel AcceptTask so end task immediately after cancel received
			return Task.WhenAny(acceptTask, Task.Run(async () =>
			{
				await Task.Delay(-1, stopToken).ConfigureAwait(false);
				this.socket.Shutdown(SocketShutdown.Both);
				this.socket.Close(1000);
			}));
		}

		private void RunReceiveTask(Socket clientSocket, CancellationToken stopToken)
		{
			Task.Run(async () =>
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
				
				// Used for counting messages by type within one batch.
				var messagesBatchCount = new Dictionary<Type, ushort>();
				
				// Used for counting messages within one batch regardless type.
				ushort numInBatch = 0;
				
				while(!(stopToken.IsCancellationRequested || manualDisconnectToken.IsCancellationRequested))
				{
					int bytesRead;

					try
					{
						// bytesRead = await client.Socket.ReceiveTask(client.Buffer.Data).ConfigureAwait(false);
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

					// Detect client disconnect
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
						// Buffer corrupted. Disconnecting client
						client.CloseSocket();
						this.ClientDisconnected?.Invoke(client, DisconnectType.BufferError);
						this.ErrorOccurred?.Invoke(ListenerError.BufferCorrupted, e);
						return;
					}

					// Handle messages in the queue.
					while(client.Buffer.TryGetMsgBytes(out var messageBytes))
					{
						var message = this.Model.Deserialize(messageBytes); // TODO: catch deserialization exception

						// Update last data receive time
						client.LastDataReceive = DateTime.UtcNow;
						
						this.MessageReceived?.Invoke(message, client);

						if(this.Dispatcher is null) continue;
						
						var messageType = message.GetType();

						ushort messageBatchNum;
						
						if(messagesBatchCount.ContainsKey(messageType))
						{
							messageBatchNum = ++messagesBatchCount[message.GetType()];
						}
						else
						{
							messagesBatchCount.Add(messageType, 0);
							messageBatchNum = 0;
						}

						try
						{
							if(this.DispatchAsync)
							{
								this.Dispatcher.DispatchAsync(message, client, numInBatch, messageBatchNum)
									.FireAndForget();
							}
							else
							{
								this.Dispatcher.Dispatch(message, client, numInBatch, messageBatchNum);
							}
						}
						catch(Exception e)
						{
							this.DispatcherException?.Invoke(e);
						}
						
						numInBatch++;
					}
					
					messagesBatchCount.Clear();
					numInBatch = 0;
				}
			}, stopToken);
		}
	}
}
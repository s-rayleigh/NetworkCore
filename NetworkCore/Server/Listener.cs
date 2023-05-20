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
	[PublicAPI]
	public class Listener
	{
		/// <summary>
		/// Packet dispatcher to dispatch incoming packets.
		/// </summary>
		public PacketDispatcher Dispatcher { get; set; }

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

		public delegate void PacketHandler(Packet packet, Client client);

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
		/// Packet received from client.
		/// </summary>
		public event PacketHandler PacketReceived;

		/// <summary>
		/// Some internal error occured.
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
		public Task BeginListening(ushort queueLength, CancellationToken stopToken = default(CancellationToken))
		{
			// No model is defined, using default model
			if(this.Model is null)
			{
				this.Model = new DataModel();
			}

			this.socket.Listen(queueLength);

			var acceptTask = Task.Run(async () =>
			{
				while(true)
				{
					Socket clientSocket = null;

					try
					{
						// clientSocket = await this.socket.AcceptTask().ConfigureAwait(false);
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

				// Client disconnected while sending the packet
				client.DisconnectedInternal += delegate(Client c)
				{
					c.CloseSocket();
					this.ClientDisconnected?.Invoke(c, DisconnectType.SendError);
				};

				var manualDisconnectToken = client.DisconnectToken;
				
				// Used for counting packets by type within one batch
				var packetsBatchCount = new Dictionary<Type, ushort>();
				
				// Used for counting packets within one batch regardless type
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

					// Handle all packets in the buffer queue
					while(client.Buffer.TryGetPacketBytes(out var packetBytes))
					{
						var packet = this.Model.Deserialize(packetBytes); // TODO: catch deserialization exception

						// Update last data receive time
						client.LastDataReceive = DateTime.UtcNow;
						
						this.PacketReceived?.Invoke(packet, client);

						if(this.Dispatcher is null) { continue; }
						
						var packetType = packet.GetType();

						ushort packetBatchNum;
						
						if(packetsBatchCount.ContainsKey(packetType))
						{
							packetBatchNum = ++packetsBatchCount[packet.GetType()];
						}
						else
						{
							packetsBatchCount.Add(packetType, 0);
							packetBatchNum = 0;
						}

						try
						{
							if(this.DispatchAsync)
							{
								this.Dispatcher.DispatchAsync(packet, client, numInBatch, packetBatchNum).FireAndForget();
							}
							else
							{
								this.Dispatcher.Dispatch(packet, client, numInBatch, packetBatchNum);
							}
						}
						catch(Exception e)
						{
							this.DispatcherException?.Invoke(e);
						}
						
						numInBatch++;
					}
					
					packetsBatchCount.Clear();
					numInBatch = 0;
				}
			}, stopToken);
		}
	}
}
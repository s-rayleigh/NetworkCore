using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCore.Data;
using NetworkCore.Extensions;

namespace NetworkCore.Server
{
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
		/// Disable using of Nagle algorithm;
		/// </summary>
		public bool NoDelay { get; set; } = false;
		
		private IPEndPoint listeningEndPoint;

		private Socket socket;

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
		public event ClientHandler ClientDisconnected;

		/// <summary>
		/// Packet received from client.
		/// </summary>
		public event PacketHandler PacketReceived;

		/// <summary>
		/// Some internal error occured.
		/// </summary>
		public event ErrorHandler ErrorOccurred;

		#endregion

		public Listener()
		{
			// TODO: implement
		}

		public Listener(IPEndPoint endPoint) : this()
		{
			this.listeningEndPoint = endPoint;
		}
		
		public Listener(string ip, ushort port) : this(Tools.BuildIpEndPoint(ip, port)) { }

		public Listener Bind(IPEndPoint endPoint)
		{
			this.listeningEndPoint = endPoint;
			return this;
		}
		
		public Listener Bind(string ip, ushort port)
		{
			this.Bind(Tools.BuildIpEndPoint(ip, port));
			return this;
		}

		// TODO: detailed description
		public Task BeginListening(ushort queueLength, CancellationToken stopToken = default(CancellationToken))
		{
			if(!(this.socket is null))
			{
				throw new InvalidOperationException("Listening has already begun.");
			}
			
			if(this.listeningEndPoint is null)
			{
				throw new InvalidOperationException("Binding to ip address and port is required to begin listening.");
			}

			// No model is defined, using default model
			if(this.Model is null)
			{
				this.Model = new DataModel();
			}

			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				ExclusiveAddressUse = true,
				NoDelay = this.NoDelay
			};

			this.socket.Bind(this.listeningEndPoint);
			this.socket.Listen(queueLength);

			var acceptTask = Task.Run(async () =>
			{
				while(true)
				{
					Socket clientSocket = null;

					try
					{
						clientSocket = await this.socket.AcceptTask();
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
						this.socket.Shutdown(SocketShutdown.Both);
						this.socket.Close(1000);
						break;
					}
				}
			}, stopToken);

			// We cannot cancel AcceptTask so end task immediately after cancel received
			return Task.WhenAny(acceptTask, Task.Run(() => { stopToken.WaitHandle.WaitOne(); }));
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
					this.ClientDisconnected?.Invoke(c);
				};

				var manualDisconnectToken = client.DisconnectToken;

				while(!(stopToken.IsCancellationRequested || manualDisconnectToken.IsCancellationRequested))
				{
					int bytesRead;

					try
					{
						bytesRead = await client.Socket.ReceiveTask(client.Buffer.Data);
					}
					catch(ObjectDisposedException e)
					{
						this.ErrorOccurred?.Invoke(ListenerError.SocketClosed, e);
						this.ClientDisconnected?.Invoke(client);
						return;
					}
					catch(SocketException e)
					{
						this.ErrorOccurred(ListenerError.SocketError, e);
						this.ClientDisconnected?.Invoke(client);
						client.CloseSocket();
						return;
					}
					catch(Exception e)
					{
						this.ErrorOccurred?.Invoke(ListenerError.Unknown, e);
						this.ClientDisconnected?.Invoke(client);
						client.CloseSocket();
						return;
					}

					// Detect client disconnect
					if(bytesRead <= 0)
					{
						this.ClientDisconnected?.Invoke(client);
						client.CloseSocket();
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
						this.ClientDisconnected?.Invoke(client);
						this.ErrorOccurred?.Invoke(ListenerError.BufferCorrupted, e);
						return;
					}

					// Handle all packets in the buffer queue
					while(client.Buffer.TryGetPacketBytes(out var packetBytes))
					{
						var packet = this.Model.Deserialize(packetBytes);
						this.PacketReceived?.Invoke(packet, client);

						// Update last data receive time
						client.LastDataReceive = DateTime.UtcNow;
						
						if(this.Dispatcher is null)
						{
							continue;
						}

						if(this.DispatchAsync)
						{
							_ = this.Dispatcher.DispatchAsync(packet, client);
						}
						else
						{
							this.Dispatcher.Dispatch(packet, client);
						}
					}
				}
			}, stopToken);
		}
	}
}
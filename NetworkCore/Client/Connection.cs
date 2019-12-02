using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCore.Data;
using NetworkCore.Extensions;

namespace NetworkCore.Client
{
	// TODO: implement IDisposable
	/// <summary>
	/// Connection to the server.
	/// </summary>
	public class Connection : IDisposable
	{
		private IPEndPoint endPoint;
		
		private readonly Socket socket;

		private ReceiveBuffer buffer;

		private bool endReceive;
		
		public PacketDispatcher Dispatcher { get; set; }

		public bool DispatchAsync { get; set; } = true;
		
		public DataModel Model { get; set; }

		public PacketSender Sender { get; private set; }

		public ushort ReceiveBufferSize { get; set; } = 1024;
		
		/// <summary>
		/// Disable using of Nagle algorithm;
		/// </summary>
		public bool NoDelay { get; set; } = false;

		#region Delegates
		
		public delegate void ErrorHandler(Exception e);
		
		public delegate void PacketHandler(Packet packet);

		#endregion

		#region Events

		/// <summary>
		/// Connected event.
		/// </summary>
		public event Action Connected;

		/// <summary>
		/// Disconnected event.
		/// </summary>
		public event Action Disconnected;

		public event ErrorHandler ConnectionError;

		public event ErrorHandler DataReceiveError;

		/// <summary>
		/// Packet received event.
		/// </summary>
		public event PacketHandler PacketReceived;

		#endregion
		
		public Connection()
		{
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				ExclusiveAddressUse = true,
				NoDelay = this.NoDelay
			};
		}

		public Connection(string ip, ushort port) : this(Tools.BuildIpEndPoint(ip, port)) { }
		
		public Connection(IPEndPoint endPoint) : this()
		{
			this.endPoint = endPoint;
		}
		
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
		
		public async Task Connect()
		{
			if(this.socket.Connected)
			{
				throw new InvalidOperationException("Already connected.");
			}
			
			if(this.Model is null)
			{
				this.Model = new DataModel();
			}

			if(this.endPoint is null)
			{
				throw new InvalidOperationException("Binding to ip address and port is required to connect.");
			}

			try
			{
				await this.socket.ConnectTask(this.endPoint).ConfigureAwait(false);
			}
			catch(Exception e) when (e is SocketException || e is InvalidOperationException)
			{
				this.ConnectionError?.Invoke(e);
				return;
			}
			
			this.Sender = new PacketSender(this.socket, this.Model);
			this.Connected?.Invoke();
		}

		public void BeginReceive(CancellationToken stopToken = default(CancellationToken))
		{
			if(!this.socket.Connected)
			{
				throw new InvalidOperationException("Cannot begin receiving packets because socket is not connected to the server.");
			}

			this.buffer = new ReceiveBuffer(this.ReceiveBufferSize);

			Task.Run(async () =>
			{
				while(true)
				{
					int bytesRead;

					try
					{
						bytesRead = await this.socket.ReceiveTask(this.buffer.Data).ConfigureAwait(false);
					}
					catch(ObjectDisposedException)
					{
						// Socket disposed, stop listening
						return;
					}
					catch(SocketException e)
					{
						this.DataReceiveError?.Invoke(e);
						await this.Disconnect().ConfigureAwait(false);
						this.Disconnected?.Invoke();
						return;
					}

					if(bytesRead <= 0)
					{
						await this.Disconnect().ConfigureAwait(false);
						this.Disconnected?.Invoke();
						break;
					}
					
					try
					{
						this.buffer.TryReceive(bytesRead);
					}
					catch(ProtocolViolationException e)
					{
						this.DataReceiveError?.Invoke(e);
						await this.Disconnect().ConfigureAwait(false);
						this.Disconnected?.Invoke();
						return;
					}

					while(this.buffer.TryGetPacketBytes(out var packetBytes))
					{
						var packet = this.Model.Deserialize(packetBytes);
						this.PacketReceived?.Invoke(packet);

						if(this.Dispatcher is null) { continue; }
					
						if(this.DispatchAsync)
						{
							_ = this.Dispatcher.DispatchAsync(packet);
						}
						else
						{
							this.Dispatcher.Dispatch(packet);
						}
					}

					// TODO: change endReceive to internal cancellation token
					if(stopToken.IsCancellationRequested || this.endReceive)
					{
						break;
					}
				}
			}, stopToken);
		}

		public async Task Disconnect()
		{
			if(!this.socket.Connected) { return; }

			this.endReceive = true; // TODO: replace by using of cancellation token
			
			try
			{
				this.socket.Shutdown(SocketShutdown.Both);
				await this.socket.DisconnectTask(false).ConfigureAwait(false); // TODO: reuse
			}
			catch(ObjectDisposedException) { }

		}

		public void Dispose()
		{
			if(this.socket is null) { return; }

			try
			{
				this.socket.Shutdown(SocketShutdown.Both);
			}
			catch(ObjectDisposedException) { }

			this.socket.Close();
		}
	}
}
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;
using NetworkCore.Extensions;

namespace NetworkCore.Client
{
	// TODO: implement IDisposable
	/// <summary>
	/// Connection to the server.
	/// </summary>
	[PublicAPI]
	public class Connection : IDisposable
	{
		private IPEndPoint endPoint;
		
		private readonly Socket socket;

		private ReceiveBuffer buffer;

		private bool endReceive;

		public MessageDispatcher Dispatcher { get; set; }

		public bool DispatchAsync { get; set; } = true;
		
		public DataModel Model { get; set; }

		public MessageSender Sender { get; private set; }

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

		#region Delegates
		
		public delegate void ErrorHandler(Exception e);
		
		public delegate void MessageHandler(Message message);

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
		/// Fired when a message is received from the server.
		/// </summary>
		public event MessageHandler MessageReceived;

		/// <summary>
		/// Fired when an exception is occured in one of the message handlers.
		/// </summary>
		public event Action<Exception> HandlerException;
		
		#endregion
		
		public Connection()
		{
			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				ExclusiveAddressUse = true
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
				// await this.socket.ConnectTask(this.endPoint).ConfigureAwait(false);
				await this.socket.ConnectAsync(this.endPoint).ConfigureAwait(false);
			}
			catch(Exception e) when (e is SocketException || e is InvalidOperationException)
			{
				this.ConnectionError?.Invoke(e);
				return;
			}
			
			this.Sender = new MessageSender(this.socket, this.Model);
			this.Connected?.Invoke();
		}

		public void BeginReceive(CancellationToken stopToken = default)
		{
			if(!this.socket.Connected)
			{
				throw new InvalidOperationException(
					"Cannot begin receiving messages because the socket is not connected to the server.");
			}

			this.buffer = new ReceiveBuffer(this.ReceiveBufferSize);

			Task.Run(async () =>
			{
				while(true)
				{
					int bytesRead;

					try
					{
						// bytesRead = await this.socket.ReceiveTask(this.buffer.Data).ConfigureAwait(false);
						bytesRead = await this.socket
							.ReceiveAsync(new ArraySegment<byte>(this.buffer.Data), SocketFlags.None)
							.ConfigureAwait(false);
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

					while(this.buffer.TryGetMsgBytes(out var messageBytes))
					{
						var message = this.Model.Deserialize(messageBytes); // TODO: handle 'failed to deserialize'
						this.MessageReceived?.Invoke(message);

						if(this.Dispatcher is null) { continue; }
					
						if(this.DispatchAsync)
						{
							_ = this.Dispatcher.DispatchAsync(message);
						}
						else
						{
							this.Dispatcher.Dispatch(message);
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
		
		public Task ReceiveTask(CancellationToken stopToken = default)
		{
			if(!this.socket.Connected)
			{
				throw new InvalidOperationException("Cannot begin receiving message because socket is not connected to the server.");
			}

			this.buffer = new ReceiveBuffer(this.ReceiveBufferSize);

			return Task.Run(async () =>
			{
				while(true)
				{
					int bytesRead;

					try
					{
						// bytesRead = await this.socket.ReceiveTask(this.buffer.Data).ConfigureAwait(false);
						bytesRead = await this.socket
							.ReceiveAsync(new ArraySegment<byte>(this.buffer.Data), SocketFlags.None)
							.ConfigureAwait(false);
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

					while(this.buffer.TryGetMsgBytes(out var messageBytes))
					{
						var message = this.Model.Deserialize(messageBytes); // TODO: handle 'failed to deserialize'
						this.MessageReceived?.Invoke(message);

						if(this.Dispatcher is null) { continue; }

						try
						{
							if(this.DispatchAsync)
							{
								_ = this.Dispatcher.DispatchAsync(message);
							}
							else
							{
								this.Dispatcher.Dispatch(message);
							}
						}
						catch(Exception e)
						{
							this.HandlerException?.Invoke(e);
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
			if(!this.socket.Connected) return;

			this.endReceive = true; // TODO: use cancellation token instead.
			
			try
			{
				this.socket.Shutdown(SocketShutdown.Both);
				await this.socket.DisconnectTask(false).ConfigureAwait(false); // TODO: reuse
			}
			catch(ObjectDisposedException) { }
		}

		public void Dispose()
		{
			if(this.socket is null) return;

			try
			{
				this.socket.Shutdown(SocketShutdown.Both);
			}
			catch(ObjectDisposedException) { }

			this.socket.Close();
		}
	}
}
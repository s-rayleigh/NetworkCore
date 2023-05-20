using System;
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
	/// Client representation on the server.
	/// </summary>
	public class Client
	{
		/// <summary>
		/// Receive buffer for the data obtained from the client.
		/// </summary>
		internal ReceiveBuffer Buffer { get; }
		
		/// <summary>
		/// Socket of the client.
		/// </summary>
		internal Socket Socket { get; }

		/// <summary>
		/// IP end point of the client socket.
		/// </summary>
		public IPEndPoint EndPoint { get; }
		
		/// <summary>
		/// IP address.
		/// </summary>
		public IPAddress Ip => this.EndPoint.Address;

		/// <summary>
		/// Port.
		/// </summary>
		public int Port => this.EndPoint.Port;
		
		/// <summary>
		/// Packet sender.
		/// </summary>
		public PacketSender PacketSender { get; }

		/// <summary>
		/// <para>Last data receive time (UTC).</para>
		/// <para>Initially set to object creation time.</para>
		/// </summary>
		public DateTime LastDataReceive { get; internal set; }
		
		internal event ClientHandler DisconnectedInternal;

		/// <summary>
		/// Disconnect token source.
		/// </summary>
		private readonly CancellationTokenSource disconnectTokenSource;

		/// <summary>
		/// Cancellation token that cancels on manual disconnect.
		/// </summary>
		internal CancellationToken DisconnectToken => this.disconnectTokenSource.Token;

		private bool disconnected;
		
		internal Client(Socket socket, DataModel dataModel, ushort bufferSize)
		{
			this.Socket = socket;
			this.Buffer = new ReceiveBuffer(bufferSize);
			this.PacketSender = new PacketSender(socket, dataModel);
			this.EndPoint = (IPEndPoint)this.Socket.RemoteEndPoint;
			this.LastDataReceive = DateTime.UtcNow;
			this.disconnectTokenSource = new CancellationTokenSource();
			this.disconnected = false;

			// Detect that client is disconnected while sending the packet
			this.PacketSender.SendError += delegate
			{
				if(this.Socket.Connected || this.disconnected) return;
				
				this.DisconnectedInternal?.Invoke(this);
				this.NotifyDisconnected();
			};
		}

		~Client()
		{
			this.disconnectTokenSource.Dispose();
		}

		[PublicAPI]
		public async Task Disconnect()
		{
			this.disconnectTokenSource.Cancel();
			
			try
			{
				this.Socket.Shutdown(SocketShutdown.Both);
				await this.Socket.DisconnectTask(false).ConfigureAwait(false);
			}
			catch(ObjectDisposedException) { }
			
			this.NotifyDisconnected();
		}

		/// <summary>
		/// Shutdown and close the socket of the client.
		/// </summary>
		internal void CloseSocket()
		{
			try
			{
				this.Socket.Shutdown(SocketShutdown.Both);
			}
			catch(ObjectDisposedException) { }

			this.Socket.Close();
			this.NotifyDisconnected();
		}
		
		/// <summary>
		/// Notify the client and packet sender that socket is disconnected.
		/// </summary>
		private void NotifyDisconnected()
		{
			this.disconnected = true;
			this.PacketSender.NotifyDisconnected();
		}
	}
}
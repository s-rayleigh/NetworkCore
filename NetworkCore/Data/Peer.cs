using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Extensions;

namespace NetworkCore.Data;

/// <summary>
/// Local representation of the remote host.
/// </summary>
public sealed class Peer
{
	private readonly Socket socket;

	private readonly DataModel dataModel;

	/// <summary>
	/// Task used to receive data from server.
	/// </summary>
	private Task receiveTask;
	
	private bool disconnected;

	private bool receive;

	public IPEndPoint IpEndPoint => (IPEndPoint)this.socket.RemoteEndPoint;

	/// <summary>
	/// <para>Last message receive time in UTC.</para>
	/// <para>Initially set to object creation time.</para>
	/// </summary>
	public DateTime LastReceive { get; private set; }

	/// <summary>
	/// <para>Last message send time in UTC.</para>
	/// <para>Initially set to object creation time.</para>
	/// </summary>
	public DateTime LastSend { get; private set; }
	
	public event Action<Message, Exception> MessageSendError;

	/// <summary>
	/// Fired when a message sent.
	/// </summary>
	public event Action<Message> MessageSent;

	internal event Action<byte[]> RawMessageReceived;
	
	internal event Action<Exception> MessageReceiveError;
	
	/// <summary>
	/// Fired when the peer disconnected.
	/// </summary>
	public event Action<DisconnectType> Disconnected;

	/// <summary>
	/// Creates new peer.
	/// </summary>
	/// <param name="socket">A socket that must be already connected to the remote host.</param>
	/// <param name="dataModel">The data model used to serialize and deserialize packets.</param>
	internal Peer(Socket socket, DataModel dataModel)
	{
		if(!socket.Connected) throw new InvalidOperationException("Socket must be connected to the remote peer.");
		
		this.socket = socket;
		this.dataModel = dataModel;
		this.disconnected = false;
		this.receive = true;
		this.LastReceive = this.LastSend = DateTime.UtcNow;
		
		// Checking if disconnected when the message send error occurred.
		this.MessageSendError += (_, _) =>
		{
			if(this.socket.Connected || this.disconnected) return;
			this.CloseSocket();
			this.Disconnected?.Invoke(DisconnectType.SendError);
		};
	}

	/// <summary>
	/// Sends message to the remote host.
	/// </summary>
	/// <param name="message">A message to send.</param>
	/// <param name="sendErrorCallback">A callback to call if failed to send the message.</param>
	/// <exception cref="ArgumentNullException">If message is null.</exception>
	[PublicAPI]
	public void SendMessage(Message message, Action<Message, Exception> sendErrorCallback = null)
	{
		if(this.disconnected) return;

		if(message is null) throw new ArgumentNullException(nameof(message));

		var bytes = this.dataModel.Serialize(message);
		bytes = BitConverter.GetBytes(bytes.Length).Concat(bytes).ToArray(); // TODO: optimize.

		// TODO: use Task version of the send method.
		
		try
		{
			this.socket.BeginSend(bytes, 0, bytes.Length, 0, delegate(IAsyncResult ar)
			{
				// TODO: implement sending not sent bytes.
				try
				{
					var bytesNum = this.socket.EndSend(ar);

					if(bytesNum == bytes.Length)
					{
						this.MessageSent?.Invoke(message);
						this.LastSend = DateTime.UtcNow;
					}
					else
					{
						var ex = new Exception("Number of the bytes sent do not equal to the buffer length.");
						this.MessageSendError?.Invoke(message, ex);
						sendErrorCallback?.Invoke(message, ex);
					}
				}
				catch(Exception e)
				{
					this.MessageSendError?.Invoke(message, e);
					sendErrorCallback?.Invoke(message, e);
				}
			}, null);
		}
		catch(Exception e)
		{
			this.MessageSendError?.Invoke(message, e);
			sendErrorCallback?.Invoke(message, e);
		}
	}

	internal void RunReceiveTask(ushort receiveBufferSize) => this.receiveTask = Task.Run(async () =>
	{
		var buffer = new ReceiveBuffer(receiveBufferSize);

		while(this.receive)
		{
			int bytesReceived;

			try
			{
				bytesReceived = await this.socket
					.ReceiveAsync(new ArraySegment<byte>(buffer.Data), SocketFlags.None)
					.ConfigureAwait(false);
			}
			catch(ObjectDisposedException)
			{
				// Stop listening if the socket is disposed.
				return;
			}
			catch(SocketException e)
			{
				// Cannot receive data anymore. Disconnecting.
				this.CloseSocket();
				this.MessageReceiveError?.Invoke(e);
				this.Disconnected?.Invoke(DisconnectType.ProtocolViolation);
				return;
			}

			// Early return if stop receiving is requested.
			if(!this.receive) return;

			// Detect TCP disconnect request.
			if(bytesReceived <= 0)
			{
				this.CloseSocket();
				this.Disconnected(DisconnectType.RemoteRequest);
				return;
			}

			try
			{
				buffer.TryReceive(bytesReceived);
			}
			catch(ProtocolViolationException e)
			{
				// Buffer corrupted. Disconnecting.
				this.CloseSocket();
				this.MessageReceiveError?.Invoke(e);
				this.Disconnected?.Invoke(DisconnectType.ProtocolViolation);
				return;
			}

			// Handle messages in the queue.
			while(buffer.TryGetMsgBytes(out var messageBytes))
			{
				this.LastReceive = DateTime.UtcNow;
				this.RawMessageReceived?.Invoke(messageBytes);
			}
		}
	});
	
	/// <summary>
	/// Disconnects from the remote host.
	/// </summary>
	public async Task Disconnect()
	{
		if(this.disconnected) return;
		
		// Prevent message sending.
		this.disconnected = true;
		
		// Tell receive task to stop.
		this.receive = false;

		var rt = this.receiveTask;

		try
		{
			// Shutdown send and receive.
			this.socket.Shutdown(SocketShutdown.Both);
			
			// Close connection and socket.
			await this.socket.DisconnectTask(false).ConfigureAwait(false);
		}
		catch(ObjectDisposedException) { }
		
		if(rt is not null)
		{
			// Wait for receive task to stop.
			await rt;
			this.receiveTask = null;
		}
		
		this.Disconnected?.Invoke(DisconnectType.LocalRequest);
	}

	/// <summary>
	/// Shutdown and close socket.
	/// </summary>
	private void CloseSocket()
	{
		this.disconnected = true;
		
		try
		{
			this.socket.Shutdown(SocketShutdown.Both);
		}
		catch(ObjectDisposedException) { }

		this.socket.Close();
	}
}
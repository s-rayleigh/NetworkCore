using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkCore.Extensions;

namespace NetworkCore.Transport.Tcp;

internal sealed class TcpRemoteHost
{
	private readonly Socket socket;

	private readonly object lockObj;
	
	/// <summary>
	/// Task used to receive data from remote host.
	/// </summary>
	private Task receiveTask;
	
	private bool disconnected;

	private bool receive;

	public IPEndPoint IpEndPoint => (IPEndPoint)this.socket.RemoteEndPoint;

	public event Action<byte[]> RawMsgReceived;
	
	public event Action<Exception> MsgReceiveError;
	
	/// <summary>
	/// Fired when the remote host disconnected.
	/// </summary>
	public event Action<DisconnectType> Disconnected;
	
	public TcpRemoteHost(Socket socket)
	{
		if(!socket.Connected) throw new InvalidOperationException("Socket must be connected to the remote peer.");

		this.receive = true;
		this.socket = socket;
		this.lockObj = new();
	}

	public async Task SendRawMsg(byte[] msg)
	{
		var bytes = BitConverter.GetBytes(msg.Length).Concat(msg).ToArray(); // TODO: optimize.
		
		try
		{
			var sentBytesNum = await this.socket.SendAsync(new ArraySegment<byte>(bytes), SocketFlags.None);
			if(sentBytesNum != bytes.Length) throw new("Number of the bytes sent do not equal to the buffer length.");
		}
		catch(SocketException) when(!this.socket.Connected)
		{
			if(this.TryCloseSocket()) this.Disconnected?.Invoke(DisconnectType.SendError);
		}
	}
	
	public void RunReceiveTask(ushort receiveBufferSize) => this.receiveTask = Task.Run(async () =>
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
				if(this.TryCloseSocket()) this.Disconnected?.Invoke(DisconnectType.ProtocolViolation);
				this.MsgReceiveError?.Invoke(e);
				return;
			}

			// Early return if stop receiving is requested.
			if(!this.receive) return;

			// Detect TCP disconnect request.
			if(bytesReceived <= 0)
			{
				if(this.TryCloseSocket()) this.Disconnected(DisconnectType.RemoteRequest);
				return;
			}

			try
			{
				buffer.TryReceive(bytesReceived);
			}
			catch(ProtocolViolationException e)
			{
				// Buffer corrupted. Disconnecting.
				if(this.TryCloseSocket()) this.Disconnected?.Invoke(DisconnectType.ProtocolViolation);
				this.MsgReceiveError?.Invoke(e);
				return;
			}

			// Handle messages in the queue.
			while(buffer.TryGetMsgBytes(out var messageBytes)) this.RawMsgReceived?.Invoke(messageBytes);
		}
	});
	
	/// <summary>
	/// Disconnects from the remote host.
	/// </summary>
	public async Task Disconnect()
	{
		lock(this.lockObj)
		{
			if(this.disconnected) return;

			// Prevent message sending and socket closing while we disconnecting.
			this.disconnected = true;
		}

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
	private bool TryCloseSocket()
	{
		lock(this.lockObj)
		{
			if(this.disconnected) return false;
			this.disconnected = true;
		}

		try
		{
			this.socket.Shutdown(SocketShutdown.Both);
		}
		catch(ObjectDisposedException) { }

		this.socket.Close();

		return true;
	}
}
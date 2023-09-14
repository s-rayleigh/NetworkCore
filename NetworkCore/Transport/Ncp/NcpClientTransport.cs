using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NetworkCore.Transport.Ncp.Model;
using NetworkCore.Transport.Ncp.Model.Connection;

namespace NetworkCore.Transport.Ncp;

public class NcpClientTransport : NcpTransport, IClientTransport
{
	private readonly byte[] receiveBuffer;

	private Task receiveTask;

	private readonly PacketReceiver packetReceiver;

	private readonly Channel<byte[]> connectionPacketsChannel;

	private readonly Channel<AckPacket> ackPacketsChannel;
	
	public event Action<byte[]> RawMsgReceived;
	
	public event Action<Exception> MsgReceiveError;
	
	public event Action<DisconnectType> Disconnected;
    
	public NcpClientTransport()
	{
		this.receiveBuffer = new byte[1500];
		this.connectionPacketsChannel = Channel.CreateUnbounded<byte[]>();
		this.ackPacketsChannel = Channel.CreateUnbounded<AckPacket>();
		this.packetReceiver = new()
		{
			MinPacketSize = MinPacketSize,
			ConnectionPacketsChannelWriter = this.connectionPacketsChannel.Writer,
			AckPacketsChannelWriter = this.ackPacketsChannel.Writer,
			OnDataPacketReceived = bytes => { } // TODO: use. 
		};
	}
	
	public async Task Connect(IPEndPoint ipEndPoint, CancellationToken cancellationToken)
	{
		// TODO: connection state.
		// TODO: connection timeout.
		
		await this.socket.ConnectAsync(ipEndPoint).ConfigureAwait(false);

		short clientSequenceNum, serverSequenceNum;
		int clientId;

		// Connection request stage.
		{
			using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var connectionRequest = this.BuildConnectionRequestPacket(out clientSequenceNum);
			var responseTask = this.WaitForOneOfConnectionPackets(
				new IConnectionPacket[] { new ClientVerificationRequestPacket(), new ConnectionErrorPacket() },
				cancelSource.Token);
			var connectionRequestTask = this.IntervalSend(connectionRequest.AsMemory(), this.SendInterval,
				cancelSource.Token);

			// NOTE: awaiting the 'connectionRequestTask' is required to catch its underneath exceptions.
			var resultingTask = await Task.WhenAny(responseTask, connectionRequestTask);
			
			// Cancel the other task.
			cancelSource.Cancel();

			if(!resultingTask.IsCompletedSuccessfully)
			{
				if(resultingTask.IsCanceled) throw new TaskCanceledException();

				if(resultingTask.IsFaulted)
				{
					const string msg = "Failed to send the connection request.";
					if(resultingTask.Exception is not null) throw new(msg, resultingTask.Exception.InnerException);
					throw new(msg);
				}
			}

			var packet = responseTask.Result;

			if(packet is ConnectionErrorPacket errorPacket)
			{
				// TODO: error codes
				throw new Exception("");
			}
			
			var verificationRequestPacket = (ClientVerificationRequestPacket)packet;
			serverSequenceNum = verificationRequestPacket.serverSequenceNumber;
			clientId = verificationRequestPacket.clientId;
		}
		
		// TODO: implement the search for the PoW task solution.

		// Client verification stage.
		{
			var verificationResponsePacket = new ClientVerificationResponsePacket
			{
				clientId = clientId,
				clientSequenceNumber = unchecked(++clientSequenceNum),
				nonce = 0 // TODO
			};
			
			
		}
		

		while(true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			
			await this.socket.SendAsync(connectionRequest.AsMemory(), SocketFlags.None,
				cancellationToken: cancellationToken);

		}

		throw new NotImplementedException();
	}

	public Task Disconnect()
	{
		throw new NotImplementedException();
	}

	public Task SendRawMsg(byte[] msg)
	{
		// TODO: if(reliable) queue.Add(msg) else socket.SendToAsync(msg)
		// this.socket.SendToAsync()
		throw new NotImplementedException();
	}
	
	private void RunReceiveTask(CancellationToken cancellationToken)
	{
		this.receiveTask = Task.Run(async () =>
		{
			while(true)
			{
				// TODO: use IMemoryOwner for the Memory<byte>.
				var receivedBytes = await this.socket.ReceiveAsync(this.receiveBuffer.AsMemory(), SocketFlags.None,
					cancellationToken: cancellationToken);



			}
		}, cancellationToken);
	}

	private void RunSendTask(CancellationToken cancellationToken = default)
	{
		Task.Run(async () =>
		{
			while(true)
			{
				// TODO: delay to config.
				await Task.Delay(40, cancellationToken);
			}
		}, cancellationToken);
	}

	private async Task IntervalSend(Memory<byte> packet, TimeSpan interval, CancellationToken ct = default)
	{
		while(true)
		{
			ct.ThrowIfCancellationRequested();
			await this.socket.SendAsync(packet, SocketFlags.None, ct);
			await Task.Delay(interval, ct);
		}
	}

	private async Task<T> WaitForConnectionPacket<T>(CancellationToken ct) where T : struct, IConnectionPacket
	{
		var channelReader = this.connectionPacketsChannel.Reader;
		var packet = new T();
		
		while(true)
		{
			ct.ThrowIfCancellationRequested();
			
			var packetBytes = await channelReader.ReadAsync(ct);
			
			// Filter received packets by type.
			if((ConnectionPacketType)packetBytes[0] != packet.Type) continue;

			// Skip connection packet type and try to parse the packet.
			if(!packet.TryParse(packetBytes[1..])) continue;
			
			return packet;
		}
	}

	private async Task<IConnectionPacket> WaitForOneOfConnectionPackets(IReadOnlyList<IConnectionPacket> packets,
		CancellationToken cancellationToken)
	{
		var channelReader = this.connectionPacketsChannel.Reader;
		
		while(true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			
			var packetBytes = await channelReader.ReadAsync(cancellationToken);

			foreach(var packet in packets)
			{
				// Filter received packets by type.
				if((ConnectionPacketType)packetBytes[0] != packet.Type) continue;

				// Skip connection packet type and try to parse the packet.
				if(!packet.TryParse(packetBytes[1..])) continue;

				return packet;
			}
		}
	}
	
	private byte[] BuildConnectionRequestPacket(out short sequenceNum)
	{
		// TODO: remove allocation if possible.
		var data = new byte[1206];
		
		// TODO: packet builder. Something like PacketBuilder.CreateConnection().SetType()...
		// TODO: packet pool (use standard pool in the .NET or ArrayPool<T> for byte[] or MemoryPool).
		
		// Set the protocol ID.
		BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0, 2), this.ProtocolId);

		// Set the packet type to "connection".
		data[2] = (byte)PacketType.Connection;
		
		// Set the connection packet type to "connection request".
		data[3] = (byte)ConnectionPacketType.ConnectionRequest;
        
		// There is a padding of 1200 bytes.
		
		// Generate a primary sequence number in the packet data and store it in an out variable.
		var sequenceNumberSpan = data.AsSpan(1204, 2);
		RandomNumberGenerator.Fill(sequenceNumberSpan);
		sequenceNum = BinaryPrimitives.ReadInt16LittleEndian(sequenceNumberSpan);
		
		return data;
	}
}
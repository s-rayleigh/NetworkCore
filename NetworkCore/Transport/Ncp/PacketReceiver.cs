using System;
using System.Buffers.Binary;
using System.Threading.Channels;
using NetworkCore.Transport.Ncp.Model;

namespace NetworkCore.Transport.Ncp;

internal sealed class PacketReceiver
{
	public required int MinPacketSize { get; init; }
	
	public required Action<byte[]> OnDataPacketReceived { private get; init; }
	
	public required ChannelWriter<byte[]> ConnectionPacketsChannelWriter { private get; init; }
	
	public required ChannelWriter<AckPacket> AckPacketsChannelWriter { private get; init; }
	
	public PacketReceiver()
	{
		
	}

	public void ReceivePacket(ReadOnlyMemory<byte> rawPacketBytes)
	{
		// NOTE: after this method called, the origin buffer should not be accessed by any other code. Otherwise
		// it can brake packet parsing. This is required to prevent copying the bytes array, which should prevent
		// unnecessary allocations.
		// TODO: use IMemoryOwner<T> to prevent simultaneous access to the Memory

		// Drop packets that are smaller than defined minimum.
		if(rawPacketBytes.Length < this.MinPacketSize) return;
		
		// NOTE: no need to check protocol id on the client, so we can just skip 2 bytes.
		var span = rawPacketBytes.Span[2..];
		var type = (PacketType)span[0];
		
		// Skip packet type byte.
		span = span[1..];
		
		switch(type)
		{
			case PacketType.Data:
				// TODO
				break;
			case PacketType.Acknowledge:
				unsafe
				{
					// Drop ACK packets of the wrong size.
					if(span.Length != sizeof(AckPacket)) return;
				}

				var ackPacket = new AckPacket
				{
					sequenceNumber = BinaryPrimitives.ReadInt16LittleEndian(span[..2]),
					ackBitmask = BinaryPrimitives.ReadInt64LittleEndian(span[2..10])
				};
				
				// TODO: drop any ack packets that are too old by the sequence number.

				this.AckPacketsChannelWriter.TryWrite(ackPacket);
				break;
			case PacketType.Connection:
				this.ConnectionPacketsChannelWriter.TryWrite(span.ToArray()); // TODO: remove allocation.
				break;
			default:
				// TODO: debug logging
				break;
		}
	}
}
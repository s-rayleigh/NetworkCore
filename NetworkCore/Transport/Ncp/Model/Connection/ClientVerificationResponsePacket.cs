using System;
using System.Buffers.Binary;

namespace NetworkCore.Transport.Ncp.Model.Connection;

internal struct ClientVerificationResponsePacket : IConnectionPacket
{
	public short clientSequenceNumber;

	public int clientId;

	public int nonce;
	
	public ConnectionPacketType Type => ConnectionPacketType.ClientVerificationResponse;
	
	public bool TryParse(Span<byte> packetBytes)
	{
		throw new NotImplementedException();
	}

	public void CopyToBuffer(Span<byte> buffer)
	{
		const int size = sizeof(short) + sizeof(int) * 2;

		if(buffer.Length < size) throw new ArgumentException("The buffer is too small.", nameof(buffer));

		BinaryPrimitives.WriteInt16LittleEndian(buffer[..2], this.clientSequenceNumber);
		BinaryPrimitives.WriteInt32LittleEndian(buffer[2..6], this.clientId);
		BinaryPrimitives.WriteInt32LittleEndian(buffer[6..10], this.nonce);
	}
}
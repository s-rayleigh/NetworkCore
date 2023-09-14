using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace NetworkCore.Transport.Ncp.Model.Connection;

internal struct ClientVerificationRequestPacket : IConnectionPacket
{
	public int clientId;

	public short serverSequenceNumber;

	public byte powChallenge;
	
	public ReadOnlyMemory<byte> powSalt;
	
	public ConnectionPacketType Type => ConnectionPacketType.ClientVerificationRequest;
	
	public bool TryParse(Span<byte> packetBytes)
	{
		// sizeof(clientId) + sizeOf(seqNum) + sizeof(powChallenge) + sizeof(powSaltLen).
		const int partSize = sizeof(int) + sizeof(short) + sizeof(byte) * 2;

		if(packetBytes.Length < partSize) return false;

		var saltSize = packetBytes[7];
		if(packetBytes.Length < partSize + saltSize) return false;

		this.clientId = BinaryPrimitives.ReadInt32LittleEndian(packetBytes[..4]);
		this.serverSequenceNumber = BinaryPrimitives.ReadInt16LittleEndian(packetBytes[4..6]);
		this.powChallenge = packetBytes[6];
		this.powSalt = packetBytes[8..saltSize].ToArray();

		return true;
	}
}
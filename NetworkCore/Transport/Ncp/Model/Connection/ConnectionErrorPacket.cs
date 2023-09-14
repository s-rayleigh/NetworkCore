using System;

namespace NetworkCore.Transport.Ncp.Model.Connection;

internal struct ConnectionErrorPacket : IConnectionPacket
{
	public ConnectionErrorType errorType;
	
	public ConnectionPacketType Type => ConnectionPacketType.ConnectionError;
	
	public bool TryParse(Span<byte> packetBytes)
	{
		throw new NotImplementedException();
	}
}
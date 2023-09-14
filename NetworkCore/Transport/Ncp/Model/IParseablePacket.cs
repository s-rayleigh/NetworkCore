using System;

namespace NetworkCore.Transport.Ncp.Model;

internal interface IParseablePacket
{
	bool TryParse(Span<byte> packetBytes);
}
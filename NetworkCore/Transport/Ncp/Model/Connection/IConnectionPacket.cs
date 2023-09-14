using System;

namespace NetworkCore.Transport.Ncp.Model.Connection;

internal interface IConnectionPacket : IParseablePacket
{
	ConnectionPacketType Type { get; }
}
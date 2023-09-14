namespace NetworkCore.Transport.Ncp.Model;

internal enum PacketType : byte
{
	Data = 0,
	Acknowledge = 1,
	Connection = 2
}
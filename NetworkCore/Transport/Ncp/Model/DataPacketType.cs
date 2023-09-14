namespace NetworkCore.Transport.Ncp.Model;

internal enum DataPacketType : byte
{
	Single = 1,
	Fragment = 2,
	Merged = 3
}
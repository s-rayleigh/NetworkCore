namespace NetworkCore.Transport.Ncp.Model;

internal struct AckPacket
{
	public short sequenceNumber;

	public long ackBitmask;
}
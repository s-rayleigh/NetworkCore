namespace NetworkCore.Transport.Ncp.Model.Connection;

internal enum ConnectionPacketType : byte
{
	ConnectionRequest = 1,
	ClientVerificationRequest = 2,
	ClientVerificationResponse = 3,
	ConnectionEstablished = 4
}
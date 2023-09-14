namespace NetworkCore.Transport.Ncp.Model.Connection;

internal enum ConnectionPacketType : byte
{
	ConnectionRequest = 1,
	ConnectionError = 2,
	ClientVerificationRequest = 3,
	ClientVerificationResponse = 4,
	ConnectionEstablished = 5
}
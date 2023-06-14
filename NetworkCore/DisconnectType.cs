namespace NetworkCore;

/// <summary>
/// Type of peer disconnect.
/// </summary>
public enum DisconnectType
{
	/// <summary>
	/// Disconnected by request from the peer.
	/// </summary>
	RemoteRequest,
	
	/// <summary>
	/// Disconnected by local request.
	/// </summary>
	LocalRequest,
	
	/// <summary>
	/// Detected connection loss on trying to send message to the remote device.
	/// </summary>
	SendError,
	
	/// <summary>
	/// Connection broken due to protocol violation by remote device.
	/// </summary>
	ProtocolViolation
}
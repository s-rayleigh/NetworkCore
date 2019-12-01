namespace NetworkCore.Server
{
	public enum ListenerError
	{
		/// <summary>
		/// Unknown error.
		/// </summary>
		Unknown,
		
		/// <summary>
		/// Socket was closed.
		/// </summary>
		SocketClosed,
		
		/// <summary>
		/// Some error has occurred in socket.
		/// </summary>
		SocketError,
		
		/// <summary>
		/// Buffer has corrupted data.
		/// </summary>
		BufferCorrupted
	}
}
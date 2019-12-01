namespace NetworkCore.Data
{
	public enum PacketType : byte
	{
		/// <summary>
		/// Request.
		/// Sent from client to the server.
		/// </summary>
		Request = 0,
		
		/// <summary>
		/// Response.
		/// Sent from server to the client as response to the request.
		/// </summary>
		Response = 1,
		
		/// <summary>
		/// Event.
		/// Sent from server to the client.
		/// </summary>
		Event = 2,
		
		/// <summary>
		/// Error.
		/// </summary>
		Error = 3
	}
}
namespace NetworkCore.Logging
{
	/// <summary>
	/// Logging level.
	/// </summary>
	public enum LogLevel : sbyte
	{
		/// <summary>
		/// Event.
		/// Logged regardless to current logging level.
		/// </summary>
		Event = -1,

		/// <summary>
		/// Fatal error.
		/// </summary>
		Fatal = 0,

		/// <summary>
		/// Error.
		/// </summary>
		Error = 1,

		/// <summary>
		/// Warning.
		/// </summary>
		Warning = 2,

		/// <summary>
		/// Info message.
		/// </summary>
		Info = 3,

		/// <summary>
		/// Debug message.
		/// </summary>
		Debug = 4
	}
}

using System;

namespace NetworkCore.Logging
{
	public interface ILoggerTarget
	{
		void Init();

		void Write(string message, DateTime timestamp);
	}
}
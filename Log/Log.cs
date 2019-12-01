using System;

namespace NetworkCore.Logging
{
	/// <summary>
	/// Класс логирования.
	/// </summary>
	public static class Log
	{
		/// <summary>
		/// Default logging level.
		/// </summary>
		public const LogLevel DefaultLogLevel = LogLevel.Error;

		/// <summary>
		/// Current logging level.
		/// </summary>
		public static LogLevel LogLevel { get; set; } = DefaultLogLevel;

		/// <summary>
		/// Log date and time in the logging messages.
		/// </summary>
		public static bool LogDateTime { get; set; } = false;
		
		public static void Event(object obj)
		{
			Console.ForegroundColor = ConsoleColor.Green;
			LogObj(obj, LogLevel.Event);
			Console.ResetColor();
		}

		public static void Fatal(object obj)
		{
			Console.ForegroundColor = ConsoleColor.Magenta;
			LogObj(obj, LogLevel.Fatal);
			Console.ResetColor();
		}

		public static void Error(object obj)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			LogObj(obj, LogLevel.Error);
			Console.ResetColor();
		}

		public static void Warning(object obj)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			LogObj(obj, LogLevel.Warning);
			Console.ResetColor();
		}

		public static void Info(object obj)
		{
			Console.ForegroundColor = ConsoleColor.Black;
			LogObj(obj, LogLevel.Info);
			Console.ResetColor();
		}

		public static void Debug(object obj)
		{
			Console.ForegroundColor = ConsoleColor.DarkGray;
			LogObj(obj, LogLevel.Debug);
			Console.ResetColor();
		}

		private static void LogObj(object obj, LogLevel logLevel)
		{
			if(logLevel > LogLevel)
			{
				return;
			}

			Console.WriteLine((LogDateTime ? $"[{DateTime.Now}]" : "") + $"[{logLevel.ToString()}] {obj ?? "null"}");
		}

		public static LogLevel LogLevelFromName(string name)
		{
			switch(name)
			{
				case "fatal":
					return LogLevel.Fatal;
				case "error":
					return LogLevel.Error;
				case "warning":
					return LogLevel.Warning;
				case "info":
					return LogLevel.Info;
				case "debug":
					return LogLevel.Debug;
				default:
					return DefaultLogLevel;
			}
		}
	}
}

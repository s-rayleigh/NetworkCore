using System;
using McMaster.Extensions.CommandLineUtils;

namespace Benchmark
{
	[Command(Name = "benchmark"), VersionOption("-v|--version", "1.0.0"),
	 Subcommand(typeof(LocalCommand), typeof(ClientCommand), typeof(ServerCommand))]
	public class Program
	{
		public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);
	}
}
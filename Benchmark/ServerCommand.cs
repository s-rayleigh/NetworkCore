using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Benchmark.DataModel;
using McMaster.Extensions.CommandLineUtils;
using NetworkCore.Server;

namespace Benchmark
{
	[Command(Name = "server", Description = "")]
	public class ServerCommand
	{
		private async Task OnExecuteAsync(CancellationToken token)
		{
			var listener = new Listener("127.0.0.1", 0)
			{
				Model = new BenchmarkDataModel()
			};

			var endPoint = (IPEndPoint)listener.EndPoint;
			
			Console.WriteLine($"Listening on {endPoint.Address}:{endPoint.Port}.");

			await listener.BeginListening(100, token);
		}
	}
}
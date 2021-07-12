using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Benchmark
{
	[Command(Name = "client", Description = "")]
	public class ClientCommand
	{
		private async Task OnExecuteAsync()
		{
			throw new NotImplementedException();
		}
	}
}
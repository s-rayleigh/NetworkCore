using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Benchmark
{
	[Command(Name = "local", Description = "")]
	public class LocalCommand
	{
		private async Task OnExecuteAsync()
		{
			throw new NotImplementedException();
		}
	}
}
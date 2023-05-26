using System.Threading;
using System.Threading.Tasks;

namespace NetworkCore.Extensions;

internal static class CancellationTokenExtensions
{
	public static Task WhenCanceled(this CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<bool>();
		cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
		return tcs.Task;
	}
}
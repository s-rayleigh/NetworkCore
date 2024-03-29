using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkCore.Extensions;

/// <summary>
/// Extension methods for the <see cref="Task"/> class.
/// </summary>
public static class TaskExtensions
{
	public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout,
		CancellationToken cancellationToken = default)
	{
		using var cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var delayTask = Task.Delay(timeout, cancelTokenSource.Token);
				
		if(await Task.WhenAny(task, delayTask) != task)
		{
			// Throw exception is canceled.
			await delayTask;
					
			throw new TimeoutException();
		}
				
		// Cancel the delay task.
		cancelTokenSource.Cancel();
				
		// Very important in order to propagate exceptions.
		return await task;
	}
}
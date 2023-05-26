using System;
using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore.Handling;

public class ThreadPoolMsgDispatcher<TSender> : MsgAsyncHandlersDispatcher<TSender>
{
	public event Action<Exception> UnhandledException;

	protected override void HandleMsg(Action<Message, TSender> callback, Message message, TSender sender)
	{
		Task.Run(() =>
		{
			try
			{
				callback(message, sender);
			}
			catch(Exception e)
			{
				this.UnhandledException?.Invoke(e);
			}
		});
	}

	protected override void HandleMsgAsync(Func<Message, TSender, Task> callback, Message message, TSender sender)
	{
		Task.Run(() =>
		{
			try
			{
				callback(message, sender);
			}
			catch(Exception e)
			{
				this.UnhandledException?.Invoke(e);
			}
		});
	}
}
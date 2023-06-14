using System;
using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore.Handling;

public class ThreadPoolMsgDispatcher : MsgAsyncHandlersDispatcher
{
	public event Action<Exception> UnhandledException;

	protected override void HandleMsg(Action<Message, Peer> callback, Message message, Peer peer)
	{
		Task.Run(() =>
		{
			try
			{
				callback(message, peer);
			}
			catch(Exception e)
			{
				this.UnhandledException?.Invoke(e);
			}
		});
	}

	protected override void HandleMsgAsync(Func<Message, Peer, Task> callback, Message message, Peer peer)
	{
		Task.Run(() =>
		{
			try
			{
				callback(message, peer);
			}
			catch(Exception e)
			{
				this.UnhandledException?.Invoke(e);
			}
		});
	}
}
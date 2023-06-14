using System;
using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore.Handling;

public abstract class MsgAsyncHandlersDispatcher : MsgHandlersDispatcher
{
	public void RegisterAsyncHandler<T>(IMsgAsyncHandler handler) where T : Message =>
		this.RegisterHandler(typeof(T), handler);

	protected override void HandleMessageInternal(object handler, Message message, Peer peer)
	{
		if(handler is IMsgAsyncHandler asyncHandler)
		{
			this.HandleMsgAsync(asyncHandler.HandleAsync, message, peer);
			return;
		}
			
		base.HandleMessageInternal(handler, message, peer);
	}

	protected abstract void HandleMsgAsync(Func<Message, Peer, Task> callback, Message message, Peer peer);
}
using System;
using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore.Handling
{
	public abstract class MsgAsyncHandlersDispatcher<TSender> : MsgHandlersDispatcher<TSender>
	{
		public void RegisterAsyncHandler<T>(IMsgAsyncHandler<TSender> handler) where T : Message =>
			this.RegisterHandler(typeof(T), handler);

		protected override void HandleMessageInternal(object handler, Message message, TSender sender)
		{
			if(handler is IMsgAsyncHandler<TSender> asyncHandler)
			{
				this.HandleMsgAsync(asyncHandler.HandleAsync, message, sender);
				return;
			}
			
			base.HandleMessageInternal(handler, message, sender);
		}

		protected abstract void HandleMsgAsync(Func<Message, TSender, Task> callback, Message message, TSender sender);
	}
}
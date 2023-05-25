using System;
using NetworkCore.Data;

namespace NetworkCore.Handling
{
	public sealed class SimpleMsgDispatcher<TSender> : MsgHandlersDispatcher<TSender>
	{
		protected override void HandleMsg(Action<Message, TSender> callback, Message message, TSender sender) =>
			callback(message, sender);
	}
}
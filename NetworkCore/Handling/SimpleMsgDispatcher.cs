using System;
using NetworkCore.Data;

namespace NetworkCore.Handling;

public sealed class SimpleMsgDispatcher : MsgHandlersDispatcher
{
	protected override void HandleMsg(Action<Message, Peer> callback, Message message, Peer peer) =>
		callback(message, peer);
}
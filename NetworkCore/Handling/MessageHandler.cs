using System;
using JetBrains.Annotations;
using NetworkCore.Data;

namespace NetworkCore.Handling
{
	[PublicAPI]
	public abstract class MessageHandler<TMessage, TSender> : IMsgHandler<TSender> where TMessage : Message
	{
		public event Action<TMessage, TSender> MessageReceived;
		
		public void Handle(Message message, TSender sender)
		{
			var cast = (TMessage)message;
			this.HandleMessage(cast, sender);
			this.MessageReceived?.Invoke(cast, sender);
		}

		protected virtual void HandleMessage(TMessage message, TSender sender) { }
	}
}
using JetBrains.Annotations;
using NetworkCore.Data;

namespace NetworkCore
{
	[PublicAPI]
	public abstract class MessageHandler<T> : IHandler where T : Message
	{
		public delegate void ReceiveEventHandler(T message, object state);

		public event ReceiveEventHandler MessageReceived;
		
		public void Handle(Message message, object state)
		{
			var cast = (T)message;
			this.HandleMessage(cast, state);
			this.MessageReceived?.Invoke(cast, state);
		}

		protected virtual void HandleMessage(T message, object state) { }
	}
}
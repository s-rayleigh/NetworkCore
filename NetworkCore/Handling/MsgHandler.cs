using System;
using JetBrains.Annotations;
using NetworkCore.Data;

namespace NetworkCore.Handling
{
	/// <summary>
	/// Standard abstract implementation of the <see cref="IMsgHandler{TSender}"/> interface which provides
	/// possibility to handle message in the overriden <see cref="HandleMessage"/> method or by subscribing
	/// to the <see cref="MessageReceived"/> event.
	/// </summary>
	/// <typeparam name="TMessage">The type of the message handled by this message handler.</typeparam>
	/// <typeparam name="TSender">The type of the object that represents the sender of the message.</typeparam>
	[PublicAPI]
	public abstract class MsgHandler<TMessage, TSender> : IMsgHandler<TSender> where TMessage : Message
	{
		/// <summary>
		/// Fired when a message is passed for handling.
		/// </summary>
		public event Action<TMessage, TSender> MessageReceived;
		
		/// <summary>
		/// Handles message.
		/// </summary>
		/// <param name="message">The message to handle.</param>
		/// <param name="sender">An object that represents the sender of the message.</param>
		public void Handle(Message message, TSender sender)
		{
			var cast = (TMessage)message;
			this.HandleMessage(cast, sender);
			this.MessageReceived?.Invoke(cast, sender);
		}

		/// <summary>
		/// Override this method to implement handling logic.
		/// </summary>
		/// <param name="message">The message to handle.</param>
		/// <param name="sender">An object that represents the sender of the message.</param>
		protected virtual void HandleMessage(TMessage message, TSender sender) { }
	}
}
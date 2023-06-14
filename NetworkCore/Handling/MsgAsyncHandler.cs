using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;

namespace NetworkCore.Handling;

/// <summary>
/// Standard abstract implementation of the <see cref="IMsgAsyncHandler"/> interface which provides
/// possibility to <b>asynchronously</b> handle message in the overriden <see cref="HandleMessageAsync"/> method or
/// by subscribing to the <see cref="MessageReceived"/> event.
/// </summary>
/// <typeparam name="TMessage">The type of the message handled by this message handler.</typeparam>
[PublicAPI]
public class MsgAsyncHandler<TMessage> : IMsgAsyncHandler where TMessage : Message
{
	/// <summary>
	/// Fired when a message is passed for handling.
	/// </summary>
	/// <remarks>
	/// All subscribed handlers are called in parallel.
	/// </remarks>
	public event Func<TMessage, Peer, Task> MessageReceived;
		
	/// <summary>
	/// Handles message.
	/// </summary>
	/// <param name="message">The message to handle.</param>
	/// <param name="peer">A peer that represents the sender of the message.</param>
	public async Task HandleAsync(Message message, Peer peer)
	{
		var cast = (TMessage)message;
		await this.HandleMessageAsync(cast, peer);

		var eventHandlers = this.MessageReceived;
		if(eventHandlers is null) return;

		await Task.WhenAll(eventHandlers.GetInvocationList().Cast<Func<TMessage, Peer, Task>>()
			.Select(h => h(cast, peer)));
	}

	/// <summary>
	/// Override this method to implement handling logic.
	/// </summary>
	/// <param name="message">The message to handle.</param>
	/// <param name="sender">A peer that represents the sender of the message.</param>
	/// <returns>A task that represents the completion of message handling.</returns>
	protected virtual Task HandleMessageAsync(TMessage message, Peer sender) => Task.CompletedTask;
}
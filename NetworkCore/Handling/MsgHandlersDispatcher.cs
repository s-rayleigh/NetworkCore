using System;
using System.Collections.Concurrent;
using NetworkCore.Data;

namespace NetworkCore.Handling;

/// <summary>
/// Dispatches passed messages to the corresponding registered handlers.
/// </summary>
public abstract class MsgHandlersDispatcher : IMsgDispatcher
{
	private readonly ConcurrentDictionary<Type, object> handlers;

	protected MsgHandlersDispatcher() => this.handlers = new();

	/// <summary>
	/// Registers message handler.
	/// </summary>
	/// <param name="handler">Message handler.</param>
	/// <typeparam name="T">Type of the message which will be handled.</typeparam>
	/// <exception cref="ArgumentException">
	/// If handler is already registered for the specified message type.
	/// </exception>
	public void RegisterHandler<T>(IMsgHandler handler) where T : Message =>
		this.RegisterHandler(typeof(T), handler);
	
	protected void RegisterHandler(Type messageType, object handler)
	{
		if(this.handlers.TryAdd(messageType, handler)) return;
		throw new ArgumentException($"Handler for message type {messageType.Name} is already registered.");
	}
	
	/// <summary>
	/// Removes previously registered message handler.
	/// </summary>
	/// <typeparam name="T">Type of the message.</typeparam>
	/// <exception cref="ArgumentException">If the handler is not registered.</exception>
	public void RemoveHandler<T>() where T : Message
	{
		var type = typeof(T);
		if(this.handlers.TryRemove(type, out _)) return;
		throw new ArgumentException($"Handler for message type {type} is not registered.", nameof(T));
	}

	/// <summary>
	/// Dispatches a message to the handler if there is suitable handler registered.
	/// </summary>
	/// <param name="message">Message to dispatch.</param>
	/// <param name="peer">A peer that represents the sender of the message.</param>
	public void DispatchMessage(Message message, Peer peer)
	{
		if(!this.handlers.TryGetValue(message.GetType(), out var handler)) return;
		this.HandleMessageInternal(handler, message, peer);
	}

	protected virtual void HandleMessageInternal(object handler, Message message, Peer peer) =>
		this.HandleMsg(((IMsgHandler)handler).Handle, message, peer);

	protected abstract void HandleMsg(Action<Message, Peer> callback, Message message, Peer peer);
}
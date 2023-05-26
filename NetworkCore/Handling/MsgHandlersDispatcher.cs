using System;
using System.Collections.Concurrent;
using NetworkCore.Data;

namespace NetworkCore.Handling;

/// <summary>
/// Dispatches passed messages to the corresponding registered handlers.
/// </summary>
public abstract class MsgHandlersDispatcher<TSender> : IMsgDispatcher<TSender>
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
	public void RegisterHandler<T>(IMsgHandler<TSender> handler) where T : Message =>
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
	/// <param name="sender">Object that represents the sender of the message.</param>
	public void DispatchMessage(Message message, TSender sender)
	{
		if(!this.handlers.TryGetValue(message.GetType(), out var handler)) return;
		this.HandleMessageInternal(handler, message, sender);
	}

	protected virtual void HandleMessageInternal(object handler, Message message, TSender sender) =>
		this.HandleMsg(((IMsgHandler<TSender>)handler).Handle, message, sender);

	protected abstract void HandleMsg(Action<Message, TSender> callback, Message message, TSender sender);
}
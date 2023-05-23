using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;

namespace NetworkCore
{
	/// <summary>
	/// Dispatches incoming messages to the corresponding handlers.
	/// </summary>
	public class MessageDispatcher
	{
		private readonly ConcurrentDictionary<Type, IHandler> handlers;

		public MessageDispatcher()
		{
			this.handlers = new ConcurrentDictionary<Type, IHandler>();
		}
		
		/// <summary>
		/// Registers message handler.
		/// </summary>
		/// <param name="handler">Message handler.</param>
		/// <typeparam name="T">Type of the message which will be handled.</typeparam>
		/// <exception cref="ArgumentException">
		/// If handler for the specified message type is already registered.
		/// </exception>
		[PublicAPI]
		public void RegisterHandler<T>(MessageHandler<T> handler) where T : Message
		{
			var type = typeof(T);

			if(this.handlers.ContainsKey(type))
			{
				throw new ArgumentException($"Handler for message type {type} is already registered.",
					nameof(T));
			}
			
			this.handlers[type] = handler;
		}

		/// <summary>
		/// Dispatches a message to the handler if there is suitable handler registered.
		/// </summary>
		/// <param name="message">Message to dispatch.</param>
		/// <param name="state">State object with custom data that used in the handler.</param>
		/// <param name="batchNum">Batch number of the message.</param>
		/// <param name="batchNumPerType">Number of the message in its batch.</param>
		internal void Dispatch(Message message, object state = null, ushort batchNum = 0, ushort batchNumPerType = 0)
		{
			// TODO: remove batchNum and batchNumPerType or define some "event args" struct with this parameters.
			if(this.handlers.TryGetValue(message.GetType(), out var handler))
			{
				handler.Handle(message, state, batchNum, batchNumPerType);
			}
		}

		/// <summary>
		/// Dispatches a message asynchronously.
		/// </summary>
		/// <param name="message">Message to dispatch.</param>
		/// <param name="state">State object with custom data that used in the handler.</param>
		/// <param name="batchNum">Batch number of the message.</param>
		/// <param name="batchNumPerType">Number of the message in its batch.</param>
		/// <returns>Task.</returns>
		internal async Task DispatchAsync(Message message, object state = null, ushort batchNum = 0,
			ushort batchNumPerType = 0)
		{
			await Task.Run(() => this.Dispatch(message, state, batchNum, batchNumPerType)).ConfigureAwait(false);
		}
	}
}
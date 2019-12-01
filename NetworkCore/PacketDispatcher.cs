using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore
{
	/// <summary>
	/// Dispatcher of the packets.
	/// </summary>
	public class PacketDispatcher
	{
		private readonly ConcurrentDictionary<Type, IHandler> handlers;

		public PacketDispatcher()
		{
			this.handlers = new ConcurrentDictionary<Type, IHandler>();
		}
		
		/// <summary>
		/// Registers the handler that handles the packet.
		/// </summary>
		/// <param name="handler">Packet handler.</param>
		/// <typeparam name="T">Type of the packet.</typeparam>
		/// <exception cref="ArgumentException">If handler for the specified type of the packet is already registered.</exception>
		public void RegisterHandler<T>(PacketHandler<T> handler) where T : Packet
		{
			var type = typeof(T);

			if(this.handlers.ContainsKey(type))
			{
				throw new ArgumentException($"Handler for specified packet type '{type}' is already registered.", nameof(T));
			}
			
			this.handlers[type] = handler;
		}

		/// <summary>
		/// Dispatches a packet.
		/// </summary>
		/// <param name="packet">Packet to dispatch.</param>
		/// <param name="state">State object with custom data that used in the handler.</param>
		public void Dispatch(Packet packet, object state = null)
		{
			if(this.handlers.TryGetValue(packet.GetType(), out var handler))
			{
				handler.Handle(packet, state);
			}
		}

		/// <summary>
		/// Dispatches a packet asynchronously.
		/// </summary>
		/// <param name="packet">Packet to dispatch.</param>
		/// <param name="state">State object with custom data that used in the handler.</param>
		/// <returns>Task.</returns>
		public async Task DispatchAsync(Packet packet, object state = null)
		{
			await Task.Run(() => this.Dispatch(packet, state)).ConfigureAwait(false);
		}
	}
}
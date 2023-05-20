using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetworkCore.Data;

namespace NetworkCore
{
	/// <summary>
	/// Dispatches incoming packets to the corresponding handlers.
	/// </summary>
	public class PacketDispatcher
	{
		private readonly ConcurrentDictionary<Type, IHandler> handlers;

		public PacketDispatcher()
		{
			this.handlers = new ConcurrentDictionary<Type, IHandler>();
		}
		
		/// <summary>
		/// Registers packet handler.
		/// </summary>
		/// <param name="handler">Packet handler.</param>
		/// <typeparam name="T">Type of the packet which will be handled.</typeparam>
		/// <exception cref="ArgumentException">
		/// If handler for the specified type of the packet is already registered.
		/// </exception>
		[PublicAPI]
		public void RegisterHandler<T>(PacketHandler<T> handler) where T : Packet
		{
			var type = typeof(T);

			if(this.handlers.ContainsKey(type))
			{
				throw new ArgumentException($"Handler for specified packet type {type} is already registered.",
					nameof(T));
			}
			
			this.handlers[type] = handler;
		}

		/// <summary>
		/// Dispatches a packet to the handler if there is suitable handler registered.
		/// </summary>
		/// <param name="packet">Packet to dispatch.</param>
		/// <param name="state">State object with custom data that used in the handler.</param>
		/// <param name="batchNum">Batch number of the packet.</param>
		/// <param name="batchNumPerType">Number of the packet in its batch.</param>
		internal void Dispatch(Packet packet, object state = null, ushort batchNum = 0, ushort batchNumPerType = 0)
		{
			// TODO: remove batchNum and batchNumPerType or define some "event args" struct with this parameters.
			if(this.handlers.TryGetValue(packet.GetType(), out var handler))
			{
				handler.Handle(packet, state, batchNum, batchNumPerType);
			}
		}

		/// <summary>
		/// Dispatches a packet asynchronously.
		/// </summary>
		/// <param name="packet">Packet to dispatch.</param>
		/// <param name="state">State object with custom data that used in the handler.</param>
		/// <param name="batchNum">Batch number of the packet.</param>
		/// <param name="batchNumPerType">Number of the packet in its batch.</param>
		/// <returns>Task.</returns>
		internal async Task DispatchAsync(Packet packet, object state = null, ushort batchNum = 0,
			ushort batchNumPerType = 0)
		{
			await Task.Run(() => this.Dispatch(packet, state, batchNum, batchNumPerType)).ConfigureAwait(false);
		}
	}
}
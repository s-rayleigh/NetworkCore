using NetworkCore.Data;

namespace NetworkCore
{
	public abstract class PacketHandler<T> : IHandler where T : Packet
	{
		public delegate void ReceiveEventHandler(T packet, object state);

		public event ReceiveEventHandler PacketReceived;
		
		public void Handle(Packet packet, object state, ushort batchNum, ushort batchNumPerType)
		{
			var cast = packet as T;
			this.HandlePacket(cast, state, batchNum, batchNumPerType);
			this.PacketReceived?.Invoke(cast, state);
		}

		protected virtual void HandlePacket(T packet, object state, ushort batchNum, ushort batchNumPerType) { }
	}
}
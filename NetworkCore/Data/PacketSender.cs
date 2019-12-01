using System;
using System.Linq;
using System.Net.Sockets;

namespace NetworkCore.Data
{
	public sealed class PacketSender
	{
		private readonly Socket socket;

		private readonly DataModel dataModel;
		
		public delegate void SendErrorHandler(Packet packet, Exception e);

		public delegate void SendHandler(Packet packet);

		public event SendErrorHandler SendError;

		public event SendHandler PacketSent;
		
		public PacketSender(Socket socket, DataModel dataModel)
		{
			this.socket = socket;
			this.dataModel = dataModel;
		}

		public void Send(Packet packet, SendErrorHandler errorHandler = null)
		{
			if(!this.socket.Connected)
			{
				throw new InvalidOperationException("Socket must be connected, but it is not.");
			}

			if(packet is null)
			{
				throw new ArgumentException("Packet should not be null.", nameof(packet));
			}

			var bytes = this.dataModel.Serialize(packet);
			bytes = BitConverter.GetBytes(bytes.Length).Concat(bytes).ToArray();

			try
			{
				this.socket.BeginSend(bytes, 0, bytes.Length, 0, delegate(IAsyncResult ar)
				{
					// TODO: implement sending not sended bytes
					try
					{
						var bytesNum = this.socket.EndSend(ar);

						if(bytesNum == bytes.Length)
						{
							this.PacketSent?.Invoke(packet);
						}
						else
						{
							var ex = new Exception("Number of the bytes sent do not equal to the buffer length.");
							this.SendError?.Invoke(packet, ex);
							errorHandler?.Invoke(packet, ex);
						}
					}
					catch(Exception e)
					{
						this.SendError?.Invoke(packet, e);
						errorHandler?.Invoke(packet, e);
					}
				}, null);
			}
			catch(Exception e)
			{
				this.SendError?.Invoke(packet, e);
				errorHandler?.Invoke(packet, e);
			}
		}
	}
}
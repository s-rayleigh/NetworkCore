using System;
using System.Linq;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace NetworkCore.Data
{
	public sealed class MessageSender
	{
		private readonly Socket socket;

		private readonly DataModel dataModel;

		private bool disconnected;
		
		public delegate void SendErrorHandler(Message message, Exception e);

		public delegate void SendHandler(Message message);

		public event SendErrorHandler SendError;

		public event SendHandler MessageSent;
		
		public MessageSender(Socket socket, DataModel dataModel)
		{
			this.socket = socket;
			this.dataModel = dataModel;
			this.disconnected = false;
		}

		[PublicAPI]
		public void Send(Message message, SendErrorHandler errorHandler = null)
		{
			if(this.disconnected) return;

			if(message is null) throw new ArgumentNullException(nameof(message));

			var bytes = this.dataModel.Serialize(message);
			bytes = BitConverter.GetBytes(bytes.Length).Concat(bytes).ToArray(); // TODO: optimize

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
							this.MessageSent?.Invoke(message);
						}
						else
						{
							var ex = new Exception("Number of the bytes sent do not equal to the buffer length.");
							this.SendError?.Invoke(message, ex);
							errorHandler?.Invoke(message, ex);
						}
					}
					catch(Exception e)
					{
						this.SendError?.Invoke(message, e);
						errorHandler?.Invoke(message, e);
					}
				}, null);
			}
			catch(Exception e)
			{
				this.SendError?.Invoke(message, e);
				errorHandler?.Invoke(message, e);
			}
		}

		/// <summary>
		/// Notify that socket is disconnected.
		/// </summary>
		internal void NotifyDisconnected()
		{
			this.disconnected = true;
		}
	}
}
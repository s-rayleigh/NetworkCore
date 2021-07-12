using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NetworkCore.Data
{
	/// <summary>
	/// Buffer for receiving packets.
	/// </summary>
	internal class ReceiveBuffer
	{
		/// <summary>
		/// Receiving buffer.
		/// </summary>
		public byte[] Data { get; }

		/// <summary>
		/// Size of the buffer.
		/// </summary>
		public int Size => this.Data.Length;
		
		/// <summary>
		/// Received bytes.
		/// </summary>
		private readonly List<byte> bytes;

		/// <summary>
		/// Known size of the packet.
		/// </summary>
		private int packetSize;

		/// <summary>
		/// Is the packet size known.
		/// </summary>
		private bool packetSizeKnown;

		/// <summary>
		/// Packet bytes queue.
		/// </summary>
		private readonly Queue<byte[]> queue;
		
		/// <summary>
		/// Creates new receive buffer with specified size.
		/// </summary>
		/// <param name="size">Size of the buffer in bytes.</param>
		public ReceiveBuffer(ushort size)
		{
			this.Data = new byte[size];
			this.bytes = new List<byte>();
			this.queue = new Queue<byte[]>();
		}
		
		/// <summary>
		/// Try to receive packet and add it to the internal queue.
		/// </summary>
		/// <param name="count">Received bytes count.</param>
		/// <exception cref="ProtocolViolationException">If buffer is corrupted.</exception>
		public void TryReceive(int count)
		{
			const int intSize = sizeof(int);

			if(count > 0)
			{
				this.bytes.AddRange(this.Data.Take(count));
			}

			if(!this.packetSizeKnown)
			{
				if(this.bytes.Count >= intSize)
				{
					// TODO: ToArray -> separate array for length prefix
					this.packetSize = BitConverter.ToInt32(this.bytes.Take(intSize).ToArray(), 0);
				
					// Do not process zero length packets
					if(this.packetSize == 0)
					{
						this.bytes.RemoveRange(0, intSize);
						return;
					}

					// We need to disconnect socket because data buffer is corrupted
					if(this.packetSize < 0)
					{
						throw new ProtocolViolationException($"Obtained packet size is lower than zero and is {this.packetSize}.");
					}
					
					this.packetSizeKnown = true;
				}
				else
				{
					// Wait for the next portion of data
					return;
				}
			}

			var totalLen = this.packetSize + intSize;

			if(this.bytes.Count >= totalLen)
			{
				var packetBytes = new byte[this.packetSize];

				this.bytes.CopyTo(intSize, packetBytes, 0, this.packetSize);
				this.bytes.RemoveRange(0, totalLen);
				this.packetSizeKnown = false;
				
				this.queue.Enqueue(packetBytes);
				
				if(this.bytes.Count > 0)
				{
					this.TryReceive(0);
				}
			}
		}

		/// <summary>
		/// Try to obtain packet bytes from the queue.
		/// </summary>
		/// <param name="packetBytes">Packet bytes if there any.</param>
		/// <returns>True if there any packet.</returns>
		public bool TryGetPacketBytes(out byte[] packetBytes)
		{
			if(this.queue.Count > 0)
			{
				packetBytes = this.queue.Dequeue();
				return true;
			}

			packetBytes = null;
			return false;
		}
	}
}
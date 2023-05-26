using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NetworkCore.Data;

/// <summary>
/// Buffer for receiving messages.
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
	/// Known size of the message.
	/// </summary>
	private int messageSize;

	/// <summary>
	/// Is the message size known.
	/// </summary>
	private bool messageSizeKnown;

	/// <summary>
	/// Queue of the serialized messages.
	/// </summary>
	private readonly Queue<byte[]> queue;
		
	/// <summary>
	/// Creates new receive buffer with specified size.
	/// </summary>
	/// <param name="size">Size of the buffer in bytes.</param>
	public ReceiveBuffer(ushort size)
	{
		this.Data = new byte[size];
		this.bytes = new();
		this.queue = new();
	}
		
	/// <summary>
	/// Try to receive message and add it to the internal queue.
	/// </summary>
	/// <param name="count">Received bytes count.</param>
	/// <exception cref="ProtocolViolationException">If buffer is corrupted.</exception>
	public void TryReceive(int count)
	{
		const int intSize = sizeof(int);

		if(count > 0) this.bytes.AddRange(this.Data.Take(count));

		if(!this.messageSizeKnown)
		{
			if(this.bytes.Count >= intSize)
			{
				// TODO: ToArray -> separate array for length prefix.
				this.messageSize = BitConverter.ToInt32(this.bytes.Take(intSize).ToArray(), 0);
				
				// Do not process zero length messages.
				if(this.messageSize is 0)
				{
					this.bytes.RemoveRange(0, intSize);
					return;
				}

				// We need to disconnect socket because data buffer is corrupted.
				if(this.messageSize < 0)
				{
					throw new ProtocolViolationException("Obtained message size is less than zero.");
				}
					
				this.messageSizeKnown = true;
			}
			else
			{
				// Wait for the next portion of data.
				return;
			}
		}

		var totalLen = this.messageSize + intSize;

		if(this.bytes.Count >= totalLen)
		{
			var messageBytes = new byte[this.messageSize];

			this.bytes.CopyTo(intSize, messageBytes, 0, this.messageSize);
			this.bytes.RemoveRange(0, totalLen);
			this.messageSizeKnown = false;
				
			this.queue.Enqueue(messageBytes);
				
			if(this.bytes.Count > 0) this.TryReceive(0);
		}
	}

	/// <summary>
	/// Try to obtain a message bytes from the queue.
	/// </summary>
	/// <param name="messageBytes">Message bytes if there any.</param>
	/// <returns>True if there any message.</returns>
	public bool TryGetMsgBytes(out byte[] messageBytes)
	{
		if(this.queue.Count > 0)
		{
			messageBytes = this.queue.Dequeue();
			return true;
		}

		messageBytes = null;
		return false;
	}
}
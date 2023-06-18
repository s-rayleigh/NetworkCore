using System;
using System.Net;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace NetworkCore.Data;

/// <summary>
/// Local representation of the remote host.
/// </summary>
public sealed class Peer
{
	/// <summary>
	/// IP endpoint of the remote host.
	/// </summary>
	public IPEndPoint IpEndPoint { get; }

	/// <summary>
	/// <para>Last message receive time in UTC.</para>
	/// <para>Initially set to object creation time.</para>
	/// </summary>
	public DateTime LastReceive { get; internal set; }

	/// <summary>
	/// <para>Last message send time in UTC.</para>
	/// <para>Initially set to object creation time.</para>
	/// </summary>
	public DateTime LastSend { get; internal set; }

	internal event Func<Message, Task> WantsSendMessage;

	internal event Func<Task> WantsDisconnect;

	/// <summary>
	/// Creates new peer.
	/// </summary>
	internal Peer(IPEndPoint ipEndPoint)
	{
		this.IpEndPoint = ipEndPoint;
		this.LastReceive = this.LastSend = DateTime.UtcNow;
	}

	/// <summary>
	/// Sends message to the remote host.
	/// </summary>
	/// <param name="message">A message to send.</param>
	/// <exception cref="ArgumentNullException">If message is null.</exception>
	[PublicAPI]
	public Task SendMessage(Message message)
	{
		if(message is null) throw new ArgumentNullException(nameof(message));
		return this.WantsSendMessage?.Invoke(message);
	}

	/// <summary>
	/// Disconnects from the remote host.
	/// </summary>
	public Task Disconnect() => this.WantsDisconnect?.Invoke();
}
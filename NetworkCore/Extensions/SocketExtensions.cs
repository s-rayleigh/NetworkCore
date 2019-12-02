using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkCore.Extensions
{
	public static class SocketExtensions
	{
		public static Task ConnectTask(this Socket socket, IPEndPoint endPoint) =>
			Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endPoint, null);

		public static Task DisconnectTask(this Socket socket, bool reuseSocket) =>
			Task.Factory.FromAsync(socket.BeginDisconnect, socket.EndDisconnect, reuseSocket, null);

		public static Task<int> ReceiveTask(this Socket socket, byte[] buffer, SocketFlags flags = SocketFlags.None) =>
			Task.Factory.FromAsync(socket.BeginReceive(buffer, 0, buffer.Length, flags, null, null), socket.EndReceive);

		public static Task<Socket> AcceptTask(this Socket socket) =>
			Task.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
	}
}
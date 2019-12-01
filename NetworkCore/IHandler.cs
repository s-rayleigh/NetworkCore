using NetworkCore.Data;

namespace NetworkCore
{
	public interface IHandler
	{
		void Handle(Packet packet, object state);
	}
}
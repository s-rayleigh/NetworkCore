using NetworkCore.Data;

namespace NetworkCore
{
	public interface IHandler
	{
		void Handle(Message message, object state, ushort batchNum, ushort batchNumPerType);
	}
}
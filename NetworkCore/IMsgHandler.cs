using NetworkCore.Data;

namespace NetworkCore
{
	public interface IMsgHandler<TSender>
	{
		void Handle(Message message, TSender sender);
	}
}
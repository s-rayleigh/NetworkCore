using NetworkCore.Data;

namespace NetworkCore
{
	public interface IMsgDispatcher<TSender>
	{
		void DispatchMessage(Message message, TSender sender);
	}
}
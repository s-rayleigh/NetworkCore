using NetworkCore.Data;

namespace NetworkCore.Handling
{
	public interface IMsgDispatcher<TSender>
	{
		void DispatchMessage(Message message, TSender sender);
	}
}
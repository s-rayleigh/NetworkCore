using NetworkCore.Data;

namespace NetworkCore.Handling;

public interface IMsgDispatcher
{
	void DispatchMessage(Message message, Peer peer);
}
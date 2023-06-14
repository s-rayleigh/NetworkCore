using NetworkCore.Data;

namespace NetworkCore.Handling;

public interface IMsgHandler
{
	void Handle(Message message, Peer peer);
}
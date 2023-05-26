using NetworkCore.Data;

namespace NetworkCore.Handling;

public interface IMsgHandler<TSender>
{
	void Handle(Message message, TSender sender);
}
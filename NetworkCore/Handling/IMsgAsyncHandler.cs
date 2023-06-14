using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore.Handling;

public interface IMsgAsyncHandler
{
	Task HandleAsync(Message message, Peer peer);
}
using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore.Handling
{
	public interface IMsgAsyncHandler<TSender>
	{
		Task HandleAsync(Message message, TSender sender);
	}
}
using System.Threading.Tasks;
using NetworkCore.Data;

namespace NetworkCore
{
	public interface IMsgAsyncHandler<TSender>
	{
		Task HandleAsync(Message message, TSender sender);
	}
}
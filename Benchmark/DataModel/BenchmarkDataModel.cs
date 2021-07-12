
namespace Benchmark.DataModel
{
	public class BenchmarkDataModel : NetworkCore.Data.DataModel
	{
		public BenchmarkDataModel()
		{
			this.AddPacket<DataPacket>();
		}
	}
}
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public interface ISorterEngine
    {
        Task SortChunks(Dictionary<long, int> chunkData, CancellationToken cancellationToken);
    }
}

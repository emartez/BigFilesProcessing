using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public interface ISorterEngine
    {
        Task MergeTheChunks(string destinationDirectory, string destinationFile, CancellationToken cancellationToken);
        Task SplitToChunksParallely(Dictionary<long, int> chunkData, CancellationToken cancellationToken);
    }
}

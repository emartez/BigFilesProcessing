using System.Collections.Generic;

namespace BigFilesSorter.BackgroundJobs
{
    public interface IBackgroundFileSorterQueue
    {
        void Enqueue(Dictionary<long, int> chunkData);
        int GetQueueLength();
        bool TryDequeue(out Dictionary<long, int> chunkData);
        bool IsEmpty();
    }
}

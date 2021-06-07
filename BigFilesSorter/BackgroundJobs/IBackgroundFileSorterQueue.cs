using System.Collections.Generic;

namespace BigFilesSorter.BackgroundJobs
{
    public interface IBackgroundFileSorterQueue
    {
        void Enqueue(KeyValuePair<long, int> chunkInfo);
        int GetQueueLength();
        bool TryDequeue(out KeyValuePair<long, int> result);
        bool IsEmpty();
    }
}

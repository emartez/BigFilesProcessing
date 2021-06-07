using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BigFilesSorter.BackgroundJobs
{
    public class BackgroundFileSorterQueue : IBackgroundFileSorterQueue
    {
        private readonly ConcurrentQueue<Dictionary<long, int>> _items = new();

        public void Enqueue(Dictionary<long, int> chunkInfo)
        {
            _items.Enqueue(chunkInfo);
        }

        public bool TryDequeue(out Dictionary<long, int> result)
        {
            return _items.TryDequeue(out result);
        }

        public int GetQueueLength()
        {
            return _items.Count();
        }

        public bool IsEmpty()
        {
            return _items.IsEmpty;
        }
    }
}

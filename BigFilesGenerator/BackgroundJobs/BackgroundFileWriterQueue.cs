using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace BigFilesGenerator.BackgroundJobs
{
    public class BackgroundFileWriterQueue : IBackgroundFileWriterQueue
    {
        private bool _isProcessing;
        private readonly ConcurrentQueue<StringBuilder> _items = new();

        private long _totalSize = 0;

        public void EnqueueText(StringBuilder text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _totalSize += text.Length;
            _items.Enqueue(text);
        }

        public bool TryDequeue(out StringBuilder result)
        {
            return _items.TryDequeue(out result);
        }

        public bool IsEmpty()
        {
            return _items.IsEmpty;
        }

        public float GetTotalSizeInGb()
        {
            return (float)_totalSize / 1000 / 1000 / 1000;
        }

        public int GetQueueLength()
        {
            return _items.Count();
        }
    }
}

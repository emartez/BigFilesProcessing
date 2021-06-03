﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.BackgroundJobs
{
    public class BackgroundFileWriterQueue : IBackgroundFileWriterQueue
    {
        private readonly ConcurrentQueue<StringBuilder> _items = new();
        private readonly ConcurrentQueue<Guid> _requests = new();

        // Holds the current count of texts in the queue.
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private long _totalSize = 0;
        internal float CurrentWriteQueueLength => _items.Count;

        public void EnqueueText(StringBuilder text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _totalSize += text.Length;
            _items.Enqueue(text);
            _requests.Enqueue(Guid.NewGuid());
            _signal.Release();
        }

        public async Task<StringBuilder> DequeueAsync(CancellationToken cancellationToken)
        {
            // Wait for task to become available
            await _signal.WaitAsync(cancellationToken);

            _items.TryDequeue(out var text);
            return text;
        }

        public float GetTotalSizeInGb()
        {
            return (float)_totalSize / 1000 / 1000 / 1000;
        }

        public int GetQueueLength()
        {
            return _items.Count();
        }

        public int GetNotFinishedRequest()
        {
            return _requests.Count();
        }
    }
}

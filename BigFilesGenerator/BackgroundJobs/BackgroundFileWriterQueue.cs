using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.BackgroundJobs
{
    public class BackgroundFileWriterQueue : IBackgroundFileWriterQueue
    {
        private readonly ConcurrentQueue<StringBuilder> _items = new();

        // Holds the current count of tasks in the queue.
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public void EnqueueText(StringBuilder text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _items.Enqueue(text);
            _signal.Release();
        }

        public async Task<StringBuilder> DequeueAsync(CancellationToken cancellationToken)
        {
            // Wait for task to become available
            await _signal.WaitAsync(cancellationToken);

            _items.TryDequeue(out var text);
            return text;
        }
    }
}

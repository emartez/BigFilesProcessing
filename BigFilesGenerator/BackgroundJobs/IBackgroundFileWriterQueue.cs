using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.BackgroundJobs
{
    public interface IBackgroundFileWriterQueue
    {
        // Enqueues the given text.
        void EnqueueText(StringBuilder text);

        // Dequeues and returns one text. This method blocks until a text becomes available.
        Task<StringBuilder> DequeueAsync(CancellationToken cancellationToken);
    }
}

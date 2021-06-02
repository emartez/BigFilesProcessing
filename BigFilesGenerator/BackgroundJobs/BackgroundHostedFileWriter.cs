using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.BackgroundJobs
{
    public class BackgroundHostedFileWriter : BackgroundService
    {
        private readonly IBackgroundFileWriterQueue _writerQueue;
        private readonly ILogger<BackgroundHostedFileWriter> _logger;
        private readonly GeneratorOptions _options;

        public BackgroundHostedFileWriter(IBackgroundFileWriterQueue writerQueue, ILogger<BackgroundHostedFileWriter> logger, IOptions<GeneratorOptions> options)
        {
            _writerQueue = writerQueue ?? throw new ArgumentNullException(nameof(writerQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Dequeue and execute texts until the application is stopped
            while (!cancellationToken.IsCancellationRequested)
            {
                // Get next queue position
                // This blocks until a queue position becomes available
                var text = await _writerQueue.DequeueAsync(cancellationToken);

                try
                {
                    var resultFile = Path.Combine(_options.ResultDirectory, _options.ResultFileName);
                    using (StreamWriter w = new StreamWriter(resultFile))
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await w.WriteAsync(text, cancellationToken);
                        }

                        w.Flush();
                        w.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured during execution of a background task");
                }
            }
        }
    }
}

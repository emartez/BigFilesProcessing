using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.BackgroundJobs
{
    public class BackgroundHostedFileWriter : BackgroundService
    {
        private readonly IBackgroundFileWriterQueue _writerQueue;
        private readonly ILogger<BackgroundHostedFileWriter> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly SorterOptions _options;

        public BackgroundHostedFileWriter(
            IBackgroundFileWriterQueue writerQueue, 
            ILogger<BackgroundHostedFileWriter> logger, 
            IOptions<SorterOptions> options,
            IHostApplicationLifetime appLifetime)
        {
            _writerQueue = writerQueue ?? throw new ArgumentNullException(nameof(writerQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appLifetime = appLifetime;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                  {
                      await DoWork(cancellationToken);
                  }, cancellationToken);
            });

            return Task.CompletedTask;
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            var outputFileName = Path.Combine(_options.ResultDirectory, _options.ResultFileName);

            // Dequeue and execute texts until the application is stopped
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_writerQueue.IsEmpty())
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                try
                {

                    if (_options.GenerateChunksThenMerge)
                        outputFileName = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");

                    using (StreamWriter writer = new StreamWriter(outputFileName, true))
                    {
                        for (int i = 0; i < _options.WriterGenerationLoopLimit && _writerQueue.TryDequeue(out StringBuilder text); i++)
                        {
                            await writer.WriteAsync(text, cancellationToken);
                        }

                        await writer.FlushAsync();
                        writer.Close();
                    }

                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogInformation("Background file writer is shutting down...");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured during execution of a background task");
                }
            }
        }
    }
}

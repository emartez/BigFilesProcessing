using BigFilesSorter.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BigFilesSorter.Services;

namespace BigFilesSorter.BackgroundJobs
{
    public class BackgroundHostedFileSorter : BackgroundService
    {
        private readonly IBackgroundFileSorterQueue _writerQueue;
        private readonly ILogger<BackgroundHostedFileSorter> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ISorterEngine _sorterEngine;
        private readonly SorterOptions _options;

        public BackgroundHostedFileSorter(
            IBackgroundFileSorterQueue writerQueue,
            ILogger<BackgroundHostedFileSorter> logger,
            IOptions<SorterOptions> options,
            IHostApplicationLifetime appLifetime,
            ISorterEngine sorterEngine)
        {
            _writerQueue = writerQueue ?? throw new ArgumentNullException(nameof(writerQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appLifetime = appLifetime;
            _sorterEngine = sorterEngine;
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
            var destinationFilePath = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");
            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var fileSize = new FileInfo(sourceFilePath).Length;

            // Dequeue and execute texts until the application is stopped
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_writerQueue.IsEmpty())
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                try
                {
                    if (!_writerQueue.TryDequeue(out Dictionary<long, int> chunkData))
                        continue;

                    Console.WriteLine("Scheduled from 1111111111111");
                    //await _sorterEngine.SplitToChunks(chunkData, cancellationToken);

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

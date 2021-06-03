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
                _writerQueue.SetProcessing(true);
                try
                {
                    var outputFileName = _options.GenerateChunksThenMerge
                        ? Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt")
                        : Path.Combine(_options.ResultDirectory, _options.ResultFileName);

                    var iterations = 0;
                    using (StreamWriter writer = new StreamWriter(outputFileName,true))
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            if (iterations >= _options.WriterGenerationLoopLimit) 
                                break;

                            var text = await _writerQueue.DequeueAsync(cancellationToken);
                            await writer.WriteAsync(text, cancellationToken);
                            iterations++;
                        }

                        await writer.FlushAsync();
                        writer.Close();
                    }

                    _writerQueue.SetProcessing(false);
                }
                catch(OperationCanceledException ex)
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

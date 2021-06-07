using BigFilesSorter.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BigFilesSorter.BackgroundJobs
{
    public class BackgroundHostedFileSorter : BackgroundService
    {
        private readonly IBackgroundFileSorterQueue _writerQueue;
        private readonly ILogger<BackgroundHostedFileSorter> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly SorterOptions _options;

        public BackgroundHostedFileSorter(
            IBackgroundFileSorterQueue writerQueue,
            ILogger<BackgroundHostedFileSorter> logger,
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

                Task.Run(async () =>
                {
                    await DoWork(cancellationToken);
                }, cancellationToken);

                Task.Run(async () =>
                {
                    await DoWork(cancellationToken);
                }, cancellationToken);
            });

            return Task.CompletedTask;
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            var outputFileName = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");
            var sourceFileName = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var fileSize = new FileInfo(sourceFileName).Length;

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
                    if (!_writerQueue.TryDequeue(out KeyValuePair<long, int> chunk))
                        continue;

                    using var rs = new FileStream(sourceFileName, FileMode.Open);
                    using var reader = new BinaryReader(rs);
                    using var ws = new FileStream(outputFileName, FileMode.CreateNew);
                    using var writer = new BinaryWriter(ws);
                    {
                        reader.BaseStream.Position = chunk.Key;
                        //sourcAccessor.ReadArray(0, sentence, 0, length);

                        //var lineBreaks = Enumerable.Range(0, sentence.Length).Where(idx => sentence[idx] == newLineByte).ToArray();
                        //if (!lineBreaks.Any())
                        //    _logger.LogError($"File structure is not correct for offset {offset}");

                        //var sortedSentences = new byte[lineBreaks.Length][];

                        //for (int j = 0, startIndex = 0; j < lineBreaks.Length; j++)
                        //{
                        //    sortedSentences[j] = new byte[lineBreaks[j] - startIndex];
                        //    Array.Copy(sentence, startIndex + 1, sortedSentences[j], 0, lineBreaks[j] - startIndex);
                        //    startIndex = lineBreaks[j];
                        //}

                        //Array.Sort(sortedSentences, _comparer);

                        //sentence = Encoding.Default.GetBytes("0000. testowa linijka linijk linijk linijk");
                        //var sortedSentencesArray = sortedSentences.SelectMany(s => s).ToArray();
                        //destAccessor.WriteArray(0, sortedSentencesArray, 0, sortedSentencesArray.Length);

                        writer.Write(reader.ReadBytes(chunk.Value));
                        Console.WriteLine($"Finished sorting chunk: from {chunk.Key} to {chunk.Value}");
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

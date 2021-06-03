using BigFilesGenerator.BackgroundJobs;
using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileGenerator : IFileGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;
        private readonly IBackgroundFileWriterQueue _writerQueue;
        private readonly ILogger<FileGenerator> _logger;
        private readonly SorterOptions _options;

        public FileGenerator(ISentencesGenerator sentencesGenerator,
            IBackgroundFileWriterQueue writerQueue,
            ILogger<FileGenerator> logger,
            IOptions<SorterOptions> options)
        {
            _sentencesGenerator = sentencesGenerator ?? throw new ArgumentNullException(nameof(sentencesGenerator));
            _writerQueue = writerQueue ?? throw new ArgumentNullException(nameof(writerQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task GenerateAsync(byte maxFileSizeInGb, CancellationToken cancellationToken)
        {
            float totalSizeInGb = 0;
            int iteration = 0;

            if (cancellationToken.IsCancellationRequested)
                return;

            float lastTotalSizeInGb = 0;
            while (totalSizeInGb < maxFileSizeInGb && iteration < _options.SchedullerIterationLimit && !cancellationToken.IsCancellationRequested)
            {
                List<Task> tasks = CreateNewTasks(cancellationToken);

                await Task.Run(() => Task.WhenAll(tasks), cancellationToken);

                totalSizeInGb = _writerQueue.GetTotalSizeInGb();

                if (lastTotalSizeInGb != totalSizeInGb)
                {
                    var totalSizePercentage = totalSizeInGb / maxFileSizeInGb * 100; //%
                    Console.Write($"\nTotal size of result file is {totalSizeInGb:0.000}[GB] ({totalSizePercentage:0.00}%)");
                } else
                {
                    Console.Write('.');
                }

                while (_writerQueue.GetQueueLength() >= _options.AllowedQueuedLength)
                {
                    await Task.Delay(100, cancellationToken);
                }

                lastTotalSizeInGb = totalSizeInGb;
                iteration++;
            };

            await FinalizeGeneration(cancellationToken);
        }

        public async Task SimpleGenerateAsync(byte maxFileSizeInGb, CancellationToken cancellationToken)
        {
            var fileName = "sentences.txt";

            var size = 0;
            var oldSize = 0;
            string text = "";

            using (StreamReader sr = new StreamReader(fileName))
            {
                text = await sr.ReadToEndAsync();
                sr.Close();
            }

            using (StreamWriter writer = new StreamWriter(Path.Combine(_options.ResultDirectory, "dat.txt"), true))
            {
                while (size < maxFileSizeInGb * 1000 * 1000 * 1000 && !cancellationToken.IsCancellationRequested)
                {
                    await writer.WriteAsync(text);
                    size += text.Length;

                    if (size - oldSize > 100 * 1000 * 1000)
                    {
                        oldSize = size;
                        Console.WriteLine($"\nTotal size of result file is {size / 1000f / 1000f / 1000f:0.000}[GB]");
                    }
                }

                await Task.Delay(100, cancellationToken);
                await writer.FlushAsync();
                writer.Close();
            }
        }

        private async Task FinalizeGeneration(CancellationToken cancellationToken)
        {
            while (!_writerQueue.IsEmpty())
            {
                Console.WriteLine("\nFile is still generated....");
                await Task.Delay(500, cancellationToken);
            }

            Console.WriteLine("\nGeneration completed");
            if (_options.GenerateChunksThenMerge)
            {
                Console.WriteLine("Merging chunks...");
                await Merge(cancellationToken);
            }
        }

        private async Task Merge(CancellationToken cancellationToken)
        {
            string[] txtFiles;
            int mergedFilesNumber = 0;
            txtFiles = Directory.GetFiles(_options.DestinationDirectory, "*.txt");
            var resultFile = Path.Combine(_options.ResultDirectory, _options.ResultFileName);

            while (!cancellationToken.IsCancellationRequested && txtFiles.Length > 0 && mergedFilesNumber < txtFiles.Length)
            {
                using (StreamWriter writer = new StreamWriter(resultFile))
                {
                    for (int i = 0; i < _options.FilesMergedAtOnce && mergedFilesNumber < txtFiles.Length; i++)
                    {
                        mergedFilesNumber++;
                        using (StreamReader reader = File.OpenText(txtFiles[i]))
                        {
                            await writer.WriteAsync(await reader.ReadToEndAsync());
                            reader.Close();
                        }

                        if (mergedFilesNumber % 10 == 0)
                            Console.WriteLine($"Merged {mergedFilesNumber} files");
                    }

                    await writer.FlushAsync();
                    writer.Close();
                }

                await Task.Delay(200, cancellationToken);
            }
        }

        private List<Task> CreateNewTasks(CancellationToken cancellationToken)
        {
            List<Task> tasks = new();
            for (int i = 0; i < _options.ParralelTaskSchedulingLimit; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                tasks.Add(GenerateFileContent(cancellationToken));
            }

            return tasks;
        }

        private async Task GenerateFileContent(CancellationToken cancellationToken)
        {
            var sentences = await _sentencesGenerator.GenerateData(_options.SentencesPerBatch, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            _writerQueue.EnqueueText(sentences);
        }
    }
}
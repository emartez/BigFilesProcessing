using BigFilesGenerator.BackgroundJobs;
using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileGenerator : IFileGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;
        private readonly IBackgroundFileWriterQueue _writerQueue;
        private readonly ILogger<FileGenerator> _logger;
        private readonly GeneratorOptions _options;

        public FileGenerator(ISentencesGenerator sentencesGenerator,
            IBackgroundFileWriterQueue writerQueue,
            ILogger<FileGenerator> logger,
            IOptions<GeneratorOptions> options)
        {
            _sentencesGenerator = sentencesGenerator ?? throw new ArgumentNullException(nameof(sentencesGenerator));
            _writerQueue = writerQueue ?? throw new ArgumentNullException(nameof(writerQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task Generate(byte maxFileSizeInGb, CancellationToken cancellationToken)
        {
            float totalSizeInGb = 0;
            int iteration = 0;

            if (cancellationToken.IsCancellationRequested)
                return;

            while (totalSizeInGb < maxFileSizeInGb && iteration < _options.SchedullerIterationLimit && !cancellationToken.IsCancellationRequested)
            {
                List<Task> tasks = CreateNewTasks(cancellationToken);

                await Task.Run(() => Task.WhenAll(tasks), cancellationToken);

                totalSizeInGb = _options.GenerateChunksThenMerge
                    ? new DirectoryInfo(_options.DestinationDirectory).EnumerateFiles().Sum(file => file.Length) / 1000f / 1000f / 1000f
                    : _writerQueue.GetTotalSizeInGb();

                var totalSizePercentage = totalSizeInGb / maxFileSizeInGb * 100; //%
                Console.WriteLine($"Total size of result file is {totalSizeInGb:0.00}[GB] ({totalSizePercentage:0.00}%)");

                while (_writerQueue.GetQueueLength() >= _options.AllowedQueuedLength)
                {
                    await Task.Delay(1000, cancellationToken);
                }

                await Task.Delay(100, cancellationToken);
                iteration++;
            };

            await FinalizeGeneration(cancellationToken);
        }

        private async Task FinalizeGeneration(CancellationToken cancellationToken)
        {
            while (_writerQueue.GetNotFinishedRequest() > 0)
            {
                Console.WriteLine("File is still generated....");
                await Task.Delay(500, cancellationToken);
            }

            if (_options.GenerateChunksThenMerge)
                await Merge(cancellationToken);
        }

        private async Task Merge(CancellationToken cancellationToken)
        {
            string[] txtFiles;
            int mergedFilesNumber = 0;
            txtFiles = Directory.GetFiles(_options.DestinationDirectory, "*.txt");
            var resultFile = Path.Combine(_options.ResultDirectory, _options.ResultFileName);

            while (!cancellationToken.IsCancellationRequested && mergedFilesNumber < txtFiles.Length)
            {
                using (StreamWriter writer = new StreamWriter(resultFile))
                {
                    for (int i = 0; i < _options.FilesMergedAtOnce; i++)
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
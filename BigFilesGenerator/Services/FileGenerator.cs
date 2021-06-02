using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileGenerator : IFileGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;
        private readonly ILogger<FileGenerator> _logger;
        private readonly GeneratorOptions _options;
        private readonly FileWriter _fileWriter;

        public FileGenerator(ISentencesGenerator sentencesGenerator, 
            ILogger<FileGenerator> logger, 
            IOptions<GeneratorOptions> options,
            FileWriter fileWriter)
        {
            _sentencesGenerator = sentencesGenerator;
            _options = options.Value;
            _logger = logger;
            _fileWriter = fileWriter;
        }

        public async Task Generate(byte maxFileSizeInGb, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            float totalSizeInGb = 0;
            int iteration = 0;

            if (cancellationToken.IsCancellationRequested) 
                return;

            stopWatch.Start();
            while (totalSizeInGb < maxFileSizeInGb && iteration < _options.SchedullerIterationLimit && !cancellationToken.IsCancellationRequested)
            {
                List<Task> tasks = CreateNewTasks(cancellationToken);

                await Task.Run(() => Task.WhenAll(tasks), cancellationToken);

                totalSizeInGb = _fileWriter.TotalSizeInGb;
                var totalSizePercentage = totalSizeInGb / maxFileSizeInGb * 100; //%
                Console.WriteLine($"Total size of result file is {totalSizeInGb:0.00}[GB] ({totalSizePercentage:0.00}%)");

                while (_fileWriter.CurrentWriteQueueLength > _options.AllowedQueuedLength)
                {
                    await Task.Delay(100, cancellationToken);
                }

                iteration++;
            };

            stopWatch.Stop();
            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
        }

        private List<Task> CreateNewTasks(CancellationToken cancellationToken)
        {
            List<Task> tasks = new();
            for (int i = 0; i < _options.ParralelTaskSchedulingLimit; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                tasks.Add(GenerateChunk(cancellationToken));
            }

            return tasks;
        }

        private async Task GenerateChunk(CancellationToken cancellationToken)
        {
            var sentences = await _sentencesGenerator.GenerateQuickData(100000, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            _fileWriter.WriteText(sentences);
        }
    }
}

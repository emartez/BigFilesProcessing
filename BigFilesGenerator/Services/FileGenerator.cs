using BigFilesGenerator.BackgroundJobs;
using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileGenerator : IFileGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;
        private readonly IBackgroundFileWriterQueue _fileWriterQueue;
        private readonly ILogger<FileGenerator> _logger;
        private readonly GeneratorOptions _options;
        private readonly FileWriter _fileWriter;
        private static ConcurrentDictionary<Guid, bool> _currentTasks = new ConcurrentDictionary<Guid, bool>();

        public FileGenerator(ISentencesGenerator sentencesGenerator,
            IBackgroundFileWriterQueue fileWriterQueue,
            ILogger<FileGenerator> logger, 
            IOptions<GeneratorOptions> options,
            FileWriter fileWriter)
        {
            _sentencesGenerator = sentencesGenerator;
            _fileWriterQueue = fileWriterQueue;
            _options = options.Value;
            _logger = logger;
            _fileWriter = fileWriter;
        }

        public async Task Merge(CancellationToken cancellationToken)
        {
            string[] txtFiles;
            int mergedFilesNumber = 0;
            txtFiles = Directory.GetFiles(_options.DestinationDirectory, "*.txt");

            var resultFile = Path.Combine(_options.ResultDirectory, _options.ResultFileName);

            while (mergedFilesNumber < txtFiles.Length)
            {
                using (StreamWriter writer = new StreamWriter(resultFile))
                {
                    for (int i = 0; i < 500; i++)
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

                    writer.Flush();
                    writer.Close();
                }

                await Task.Delay(200);
            }
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

                //var directoryInfo = new DirectoryInfo(_options.DestinationDirectory);
                //totalSizeInGb = directoryInfo.EnumerateFiles().Sum(file => file.Length) / 1000f / 1000f / 1000f;

                totalSizeInGb = _fileWriter.TotalSizeInGb;
                var totalSizePercentage = totalSizeInGb / maxFileSizeInGb * 100; //%
                Console.WriteLine($"Total size of result file is {totalSizeInGb:0.00}[GB] ({totalSizePercentage:0.00}%)");                

                while (_fileWriter.CurrentWriteQueueLength >= _options.AllowedQueuedLength)
                {
                    await Task.Delay(100, cancellationToken);
                }

                await Task.Delay(100, cancellationToken);
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

                tasks.Add(GenerateFileContent(cancellationToken));
            }

            return tasks;
        }

        private async Task GenerateFileContent(CancellationToken cancellationToken)
        {
            var sentences = await _sentencesGenerator.GenerateQuickData(_options.SentencesPerChunk, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            //var _filePath = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");

            //using (StreamWriter w = File.AppendText(_filePath))
            //{
            //    await w.WriteAsync(sentences, cancellationToken);
            //    w.Close();
            //}

            //_fileWriter.WriteText(sentences);
            _fileWriterQueue.EnqueueText(sentences);
        }

        #region Chunks

        public async Task GenerateChunks(byte maxFileSizeInGb, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            float totalSizeInGb = 0;
            int iteration = 0;

            if (cancellationToken.IsCancellationRequested)
                return;

            stopWatch.Start();
            while (totalSizeInGb < maxFileSizeInGb && iteration < _options.SchedullerIterationLimit && !cancellationToken.IsCancellationRequested)
            {
                List<Task> tasks = CreateNewChunkTasks(cancellationToken);
                await Task.Run(() => Task.WhenAll(tasks), cancellationToken);

                DirectoryInfo info = new DirectoryInfo(_options.DestinationDirectory);
                totalSizeInGb = info.EnumerateFiles().Sum(file => file.Length) / 1024f / 1024f / 1024f;
                var totalSizePercentage = totalSizeInGb / maxFileSizeInGb * 100; //%
                Console.WriteLine($"Total size of result file is {totalSizeInGb:0.00}[GB] ({totalSizePercentage:0.00}%)");

                iteration++;
            };

            stopWatch.Stop();
            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
        }

        private List<Task> CreateNewChunkTasks(CancellationToken cancellationToken)
        {
            List<Task> tasks = new();
            for (int i = 0; i < _options.ParralelTaskSchedulingLimit; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var chunkId = Guid.NewGuid();

                if (_currentTasks.TryAdd(chunkId, true))
                    tasks.Add(GenerateChunk(cancellationToken, chunkId));
            }

            return tasks;
        }

        private async Task GenerateChunk(CancellationToken cancellationToken, Guid chunkId)
        {
            var sentences = await _sentencesGenerator.GenerateQuickData(_options.SentencesPerChunk, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            var _filePath = Path.Combine(_options.DestinationDirectory, $"{chunkId}.txt");
            using (StreamWriter w = File.AppendText(_filePath))
            {
                w.Write(sentences);
                w.Flush();
            }

            var _task = _currentTasks.FirstOrDefault(c => c.Key == chunkId);
            if (_task.Value)
                _currentTasks.TryRemove(_task);
        }
    }

    #endregion Chunks end
}
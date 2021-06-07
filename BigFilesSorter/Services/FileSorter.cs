using BigFilesSorter.BackgroundJobs;
using BigFilesSorter.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public class FileSorter : IFileSorter
    {
        private readonly ILogger<FileSorter> _logger;
        private readonly IBackgroundFileSorterQueue _backgroundQueue;
        private readonly ISorterEngine _sorterEngine;
        private readonly SorterOptions _options;

        public FileSorter(
            ILogger<FileSorter> logger,
            IOptions<SorterOptions> options,
            IBackgroundFileSorterQueue backgroundQueue,
            ISorterEngine sorterEngine)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backgroundQueue = backgroundQueue;
            _sorterEngine = sorterEngine;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task SortAsync(CancellationToken cancellationToken)
        {
            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var destinationFilePath = Path.Combine(_options.DestinationDirectory, _options.DestinationFileName);
            var fileSize = new FileInfo(sourceFilePath).Length;
            var chunkData = GetChunkData(fileSize);

            var tasks = new List<Task>();
            var noOfParallelTasks = _options.MaxMemoryUsageMb / _options.ApproximateChunkFileSizeMb ;
            for (int i = 0; i < noOfParallelTasks; i++)
            {
                var chunksForTask = new Dictionary<long, int>();
                int j = 0;
                int nextChunk = i;
                foreach (var chunkInfo in chunkData)
                {
                    if (j == nextChunk)
                    {
                        chunksForTask[chunkInfo.Key] = chunkInfo.Value;
                        nextChunk = chunksForTask.Count * noOfParallelTasks + i;
                    }
                    j++;
                }

                Console.WriteLine(chunksForTask.Sum(c => c.Value));
                tasks.Add(_sorterEngine.SortChunks(chunksForTask, cancellationToken));
            }

            await Task.Run(() => Task.WhenAll(tasks), cancellationToken);
        }

        private Dictionary<long, int> GetChunkData(long fileSize)
        {
            var newLineByte = (byte)'\n';
            var filePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            long approximateChunkSize = _options.ApproximateChunkFileSizeMb * 1024 * 1024;
            var noOfChunks = (int)(fileSize / approximateChunkSize);

            long[] chunkEndIndexes = new long[noOfChunks];

            using (var sourceMmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "dataProcessing"))
            {
                long offset = approximateChunkSize;
                for (int i = 0; i < noOfChunks; i++)
                {
                    using (var sourceAccessor = sourceMmf.CreateViewAccessor(offset, _options.ApproximateLineLength * 2))
                    {
                        byte[] searchBytes = new byte[_options.ApproximateLineLength];
                        sourceAccessor.ReadArray(0, searchBytes, 0, _options.ApproximateLineLength);
                        var newLineIndex = Array.IndexOf(searchBytes, newLineByte);

                        if (newLineIndex < 0)
                            _logger.LogError("File structure is not correct");

                        chunkEndIndexes[i] = offset + newLineIndex;

                        if (offset + approximateChunkSize > fileSize)
                        {
                            chunkEndIndexes[i] = fileSize - 1;
                            break;
                        }

                        offset += approximateChunkSize;
                    }
                }
                sourceMmf.Dispose();
            };

            var chunkData = new Dictionary<long, int>();
            long chunkStartPoint = 0;

            for (int i = 0; i < chunkEndIndexes.Length; i++)
            {
                var offset = chunkEndIndexes[i];
                chunkData[chunkStartPoint] = (int)(offset - chunkStartPoint + 1);
                chunkStartPoint = offset + 1;
            }

            return chunkData;
        }
    }
}
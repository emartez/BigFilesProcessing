using BigFilesSorter.BackgroundJobs;
using BigFilesSorter.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public class FileSorter : IFileSorter
    {

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        private readonly ILogger<FileSorter> _logger;
        private readonly IBackgroundFileSorterQueue _backgroundQueue;
        private readonly SorterOptions _options;
        private readonly SemaphoreSlim _semaphore;

        public FileSorter(
            ILogger<FileSorter> logger,
            IOptions<SorterOptions> options,
            IBackgroundFileSorterQueue backgroundQueue)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backgroundQueue = backgroundQueue;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _semaphore = new SemaphoreSlim(0, _options.WriterSemaphorAccess);
        }

        static readonly IComparer<byte[]> _comparer = Comparer<byte[]>.Create(
            (byte[] firstLine, byte[] secondLine) => memcmp(firstLine, secondLine, firstLine.Length)
        );

        public async Task SortAsync(CancellationToken cancellationToken)
        {
            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var destinationFilePath = Path.Combine(_options.DestinationDirectory, _options.DestinationFileName);
            var fileSize = new FileInfo(sourceFilePath).Length;
            _semaphore.Release(_options.WriterSemaphorAccess);

            var tasks = new List<Task>();
            using (var sourceMmf = MemoryMappedFile.CreateFromFile(sourceFilePath, FileMode.Open, "dataProcessing"))
            {
                var chunkData = GetChunkData(fileSize, sourceMmf);
                var noOfParallelTasks = _options.MaxMemoryUsageMb / _options.ApproximateChunkFileSizeMb / 2;
                for (int i = 0; i < chunkData.Count / noOfParallelTasks; i++)
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

                    //var chunksForTask = chunkData.Skip(i * noOfParallelTasks).Take(noOfParallelTasks).ToDictionary(c => c.Key, c => c.Value);
                    tasks.Add(SortChunks(chunksForTask, fileSize, sourceFilePath, destinationFilePath, cancellationToken));
                }
            };

            await Task.Run(() => Task.WhenAll(tasks), cancellationToken);
        }

        private async Task SortChunks(Dictionary<long, int> chunkData, long fileSize, MemoryMappedFile sourceMmf, string destinationFilePath, CancellationToken cancellationToken)
        {
            var newLineByte = (byte)'\n';
            var dotByte = (byte)'.';
            if (cancellationToken.IsCancellationRequested)
                return;

            var stopwatch = new Stopwatch();
            var chunkCount = 1;

            foreach(var chunkInfo in chunkData)
            {
                stopwatch.Start();

                using (var sourceAccessor = sourceMmf.CreateViewAccessor(chunkInfo.Key, chunkInfo.Value))
                {
                    byte[] data = new byte[chunkInfo.Value];
                    sourceAccessor.ReadArray(0, data, 0, chunkInfo.Value);
                    sourceAccessor.Dispose();
                }

                try
                {
                    //data = GetSortedData(data, newLineByte, dotByte);
                    await _semaphore.WaitAsync();
                    //destinationFilePath = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");
                    //using (FileStream dfs = new FileStream(destinationFilePath, FileMode.OpenOrCreate, FileAccess.Write))
                    //{
                    //    await dfs.WriteAsync(data, 0, chunkInfo.Value, cancellationToken);
                    //    await dfs.FlushAsync();
                    //    dfs.Close();
                    //    await dfs.DisposeAsync();
                    //}
                }
                finally
                {
                    _semaphore.Release();
                }
                

                stopwatch.Stop();
                Console.WriteLine($"Written {chunkCount} chunk, time {stopwatch.Elapsed.TotalSeconds}, size {chunkInfo.Key / 1024f / 1024f:0.000}");
                chunkCount++;
            };
        }

        private byte[] GetSortedData(byte[] data, byte newLineByte, byte dotByte)
        {
            var dotIndex = 0;
            var breakIndex = 0;
            var breaksCount = 0;
            var lineBreaksList = new List<long>();
            while (breakIndex >= 0)
            {
                breakIndex = Array.IndexOf(data, newLineByte, breakIndex + 1);
                if (breakIndex >= 0)
                {
                    lineBreaksList.Add(breakIndex);
                    breaksCount++;
                }
            }

            var lineBreaks = lineBreaksList.ToArray();
            lineBreaksList = null;

            if (!lineBreaks.Any())
                _logger.LogError($"File structure is not correct");

            //var sortedSentences = new byte[lineBreaks.Length][][];
            var sortedSentences = new List<(byte[] sentence, byte[] text, byte[] id)>();

            for (long j = 0, startIndex = 0; j < lineBreaks.Length; j++)
            {
                var sentence = new byte[lineBreaks[j] - startIndex + 1];
                Array.Copy(data, startIndex, sentence, 0, lineBreaks[j] - startIndex + 1);

                dotIndex = Array.IndexOf(sentence, dotByte);

                var id = new byte[12];
                var text = new byte[_options.ApproximateLineLength];

                var idC = new byte[dotIndex];
                Array.Copy(sentence, idC, dotIndex);
                id = (new byte[12 - dotIndex]).Concat(idC).ToArray();
                Array.Copy(sentence, dotIndex + 1, text, 0, sentence.Length - 1 - dotIndex);

                sortedSentences.Add((sentence, text, id));
                startIndex = lineBreaks[j] + 1;
            }

            return sortedSentences
                .OrderBy(s => s.text, _comparer)
                .ThenBy(s => s.id, _comparer)
                .SelectMany(s => s.sentence)
                .ToArray();
        }

        private Dictionary<long, int> GetChunkData(long fileSize, MemoryMappedFile mmf)
        {
            var newLineByte = (byte)'\n';
            var filePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            long approximateChunkSize = _options.ApproximateChunkFileSizeMb * 1024 * 1024;
            var noOfChunks = (int)(fileSize / approximateChunkSize);

            long[] chunkEndIndexes = new long[noOfChunks];

            long offset = approximateChunkSize;
            for (int i = 0; i < noOfChunks; i++)
            {
                using (var sourceAccessor = mmf.CreateViewAccessor(offset, _options.ApproximateLineLength * 2))
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

            var chunkData = new Dictionary<long, int>();
            long chunkStartPoint = 0;

            for (int i = 0; i < chunkEndIndexes.Length; i++)
            {
                var currentOffset = chunkEndIndexes[i];
                chunkData[chunkStartPoint] = (int)(currentOffset - chunkStartPoint + 1);
                chunkStartPoint = currentOffset + 1;
            }

            return chunkData;
        }
    }
}
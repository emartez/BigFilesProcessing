using BigFilesSorter.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public class SorterEngine : ISorterEngine, IDisposable
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        static readonly IComparer<byte[]> _comparer = Comparer<byte[]>.Create(
            (byte[] firstLine, byte[] secondLine) => memcmp(firstLine, secondLine, firstLine.Length)
        );

        private readonly ILogger<FileSorter> _logger;
        private readonly SorterOptions _options;
        private readonly SemaphoreSlim _semaphore;
        private FileStream[] readers;

        public SorterEngine(
            ILogger<FileSorter> logger,
            IOptions<SorterOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _semaphore = new SemaphoreSlim(0, _options.WriterSemaphorAccess);
            _semaphore.Release(_options.WriterSemaphorAccess);
        }

        public async Task SplitToChunksParallely(Dictionary<long, int> chunkData, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            var noOfParallelTasks = _options.MaxMemoryUsageMb / _options.ApproximateChunkFileSizeMb;
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

                Console.WriteLine($"Size of chunks to generate: {chunksForTask.Sum(c => c.Value)}");
                tasks.Add(SplitToChunks(chunksForTask, cancellationToken));
            }

            await Task.Run(() => Task.WhenAll(tasks), cancellationToken);
        }

        public async Task MergeTheChunks(string destinationDirectory, string destinationFile, CancellationToken cancellationToken)
        {
            string[] paths = Directory.GetFiles(destinationDirectory, "*.txt");
            int chunks = paths.Length; // Number of chunks

            if (paths.Length <= 0)
                return;

            byte dotByte = (byte)'.';
            byte lineBreak = (byte)'\n';
            int recordsize = _options.ApproximateLineLength; // estimated record size
            int records = _options.ApproximateTotalRecords; // estimated total # records
            int maxusage = _options.MaxMemoryUsageMb * 1024 * 1024; // max memory usage
            int buffersize = maxusage / chunks; // bytes of each queue
            double recordoverhead = 7.5; // The overhead of using ConcurrentQueue<>
            int bufferlen = (int)(buffersize / recordsize /
              recordoverhead); // number of records in each queue

            // Open the files
            FileStream[] readers = new FileStream[chunks];
            for (int i = 0; i < chunks; i++)
                readers[i] = new FileStream(paths[i], FileMode.Open, FileAccess.Read);

            // Make the queues
            ConcurrentQueue<(byte[] line, byte[] text, byte[] id)>[] queues = new ConcurrentQueue<(byte[] line, byte[] text, byte[] id)>[chunks];
            for (int i = 0; i < chunks; i++)
                queues[i] = new ConcurrentQueue<(byte[] line, byte[] text, byte[] id)>();

            // Load the queues
            for (int i = 0; i < chunks; i++)
                await LoadQueue(queues[i], readers[i], bufferlen, lineBreak, dotByte, cancellationToken);

            // Merge!

            using FileStream sw = new FileStream(Path.Combine(_options.DestinationDirectory, _options.DestinationFileName), FileMode.OpenOrCreate, FileAccess.Write);
            bool done = false;
            int lowest_index, j, progress = 0;
            (byte[] line, byte[] text, byte[] id) lowest_value;
            while (!done)
            {
                // Report the progress
                if (++progress % 5000 == 0)
                    Console.Write("{0:f2}%   \r",
                      100.0 * progress / records);

                // Find the chunk with the lowest value
                lowest_index = -1;
                lowest_value = (Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>());

                Dictionary<int, (byte[] line, byte[] text, byte[] id)> valuesToCompare = 
                    new Dictionary<int, (byte[] line, byte[] text, byte[] id)>();

                for (j = 0; j < chunks; j++)
                {
                    if (queues[j] != null)
                    {
                        queues[j].TryPeek(out (byte[] line, byte[] text, byte[] id) peekedValue);
                        if (peekedValue.line.Length == 0)
                            continue;

                        valuesToCompare.Add(j, peekedValue);
                    }
                }

                if (valuesToCompare.Any())
                {
                    var lowestObj = valuesToCompare.OrderBy(v => v.Value.text, _comparer).ThenBy(v => v.Value.id, _comparer).First();
                    lowest_index = lowestObj.Key;
                    lowest_value = lowestObj.Value;
                }

                // Was nothing found in any queue? We must be done then.
                if (lowest_index == -1) { done = true; break; }

                // Output it
                await sw.WriteAsync(lowest_value.line, cancellationToken);

                // Remove from queue
                queues[lowest_index].TryDequeue(out _);
                // Have we emptied the queue? Top it up
                if (queues[lowest_index].Count == 0)
                {
                    Console.WriteLine($"Pull from queue {lowest_index}, file position: {readers[lowest_index].Position}");
                    await LoadQueue(queues[lowest_index],
                      readers[lowest_index], bufferlen, lineBreak, dotByte, cancellationToken);
                    // Was there nothing left to read?
                    if (queues[lowest_index].Count == 0)
                    {
                        queues[lowest_index] = null;
                    }
                }
            }
            sw.Close();

            // Close and delete the files
            for (int i = 0; i < chunks; i++)
            {
                readers[i].Close();
                File.Delete(paths[i]);
            }
        }

        private async Task LoadQueue(ConcurrentQueue<(byte[] line, byte[] text, byte[] id)> queue,
          FileStream file, int records, byte lineBreak, byte dotByte, CancellationToken cancellationToken)
        {
            var data = new byte[_options.ApproximateLineLength];
            long lastPosition = 0;
            for (int i = 0; i < records; i++)
            {
                lastPosition = file.Position;
                var bytes = await file.ReadAsync(data, cancellationToken);

                if (bytes <= 0)
                {
                    break;
                }

                var lineBreakIdx = Array.IndexOf(data, lineBreak);
                file.Position = lastPosition + lineBreakIdx + 1;

                var lineObject = GetLineObject(data, 0, lineBreakIdx, dotByte);
                queue.Enqueue(lineObject);
            }
        }

        private async Task SplitToChunks(Dictionary<long, int> chunkData, CancellationToken cancellationToken)
        {
            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var newLineByte = (byte)'\n';
            var dotByte = (byte)'.';

            if (cancellationToken.IsCancellationRequested)
                return;

            var stopwatch = new Stopwatch();
            var chunkCount = 1;

            using FileStream fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read);
            foreach (var chunkInfo in chunkData)
            {
                fs.Position = chunkInfo.Key;

                stopwatch.Start();
                byte[] data = new byte[chunkInfo.Value];
                await fs.ReadAsync(data, 0, chunkInfo.Value, cancellationToken);

                try
                {
                    data = GetSortedData(data, newLineByte, dotByte);
                    await _semaphore.WaitAsync();
                    var destinationFilePath = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");
                    using (FileStream dfs = new FileStream(destinationFilePath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        await dfs.WriteAsync(data, 0, chunkInfo.Value, cancellationToken);
                        await dfs.FlushAsync();
                        dfs.Close();
                        await dfs.DisposeAsync();
                        data = null;
                    }

                    if (chunkCount % 3 == 0)
                        GC.Collect();
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

            if (!lineBreaks.Any())
                _logger.LogError($"File structure is not correct");

            var linesToSort = new List<(byte[] line, byte[] text, byte[] id)>();

            for (long j = 0, startIndex = 0; j < lineBreaks.Length; j++)
            {
                var lineObj = GetLineObject(data, startIndex, lineBreaks[j], dotByte);

                linesToSort.Add(lineObj);
                startIndex = lineBreaks[j] + 1;
            }

            return GetOrderedLines(ref linesToSort);
        }

        private static (byte[] line, byte[] text, byte[] id) GetLineObject(byte[] data, long startIndex, long endIndex, byte dotByte)
        {
            var line = new byte[endIndex - startIndex + 1];
            Array.Copy(data, startIndex, line, 0, endIndex - startIndex + 1);
            int dotIndex = Array.IndexOf(line, dotByte);
            var id = (new byte[12 - dotIndex]).Concat(line.Take(dotIndex)).ToArray();
            return (line, line.TakeLast(line.Length - dotIndex - 1).ToArray(), id);
        }

        private byte[] GetOrderedLines(ref List<(byte[] line, byte[] text, byte[] id)> linesToSort)
        {
            return linesToSort
                   .OrderBy(s => s.text, _comparer)
                   .ThenBy(s => s.id, _comparer)
                   .SelectMany(s => s.line)
                   .ToArray();
        }

        public void Dispose()
        {
            if (readers != null && readers.Any())
                foreach (var reader in readers)
                    reader.Dispose();
        }

        ~SorterEngine()
        {
            Dispose();
        }
    }
}

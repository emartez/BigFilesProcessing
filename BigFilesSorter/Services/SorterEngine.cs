using BigFilesSorter.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public class SorterEngine : ISorterEngine
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        static readonly IComparer<byte[]> _comparer = Comparer<byte[]>.Create(
            (byte[] firstLine, byte[] secondLine) => memcmp(firstLine, secondLine, firstLine.Length)
        );

        private readonly ILogger<FileSorter> _logger;
        private readonly SorterOptions _options;
        private readonly SemaphoreSlim _semaphore;

        public SorterEngine(
            ILogger<FileSorter> logger,
            IOptions<SorterOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _semaphore = new SemaphoreSlim(0, _options.WriterSemaphorAccess);
            _semaphore.Release(_options.WriterSemaphorAccess);
        }

        public async Task SortChunks(Dictionary<long, int> chunkData, CancellationToken cancellationToken)
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

            var sortedSentences = new List<(byte[] sentence, byte[] text, byte[] id)>();

            for (long j = 0, startIndex = 0; j < lineBreaks.Length; j++)
            {
                var sentence = new byte[lineBreaks[j] - startIndex + 1];
                Array.Copy(data, startIndex, sentence, 0, lineBreaks[j] - startIndex + 1);                 

                dotIndex = Array.IndexOf(sentence, dotByte);
                var id = (new byte[12 - dotIndex]).Concat(sentence.Take(dotIndex)).ToArray();
                sortedSentences.Add((sentence, sentence.TakeLast(sentence.Length - dotIndex - 1).ToArray(), id));
                startIndex = lineBreaks[j] + 1;
            }

            return sortedSentences
                .OrderBy(s => s.text, _comparer)
                .ThenBy(s => s.id, _comparer)
                .SelectMany(s => s.sentence)
                .ToArray();
        }
    }
}

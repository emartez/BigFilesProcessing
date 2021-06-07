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
        private static SemaphoreSlim semaphore = new SemaphoreSlim(0, 1);

        public FileSorter(
            ILogger<FileSorter> logger,
            IOptions<SorterOptions> options,
            IBackgroundFileSorterQueue backgroundQueue)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backgroundQueue = backgroundQueue;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        static readonly IComparer<byte[]> _comparer = Comparer<byte[]>.Create(
            (byte[] firstLine, byte[] secondLine) => Comparer(firstLine, secondLine)
        );

        static readonly IComparer<(byte[], byte[], byte[])> _comparer2 = Comparer<(byte[], byte[], byte[])>.Create(
            ((byte[], byte[], byte[]) firstLine, (byte[], byte[], byte[]) secondLine) => Comparer2(firstLine, secondLine)
        );

        static int Comparer2((byte[], byte[], byte[]) firstLine, (byte[], byte[], byte[]) secondLine)
        {
            return memcmp(firstLine.Item2, secondLine.Item2, firstLine.Item2.Length);
        }

        static int Comparer(byte[] firstLine, byte[] secondLine)
        {

            return memcmp(firstLine, secondLine, firstLine.Length);
            //return memcmp(firstLine, secondLine, firstLine.Length);
            //var firstHash = firstLine.GetHashCode();
            //var secondHash = secondLine.GetHashCode();

            //if (firstHash < secondHash)
            //    return -1;
            //else if (firstHash > secondHash)
            //    return 1;
            //else
            //    return 0;

            //var testString = memcmp(testStringArray, testString2Array, testString2Array.Length);

            //var testIntArray = Encoding.UTF8.GetBytes("1.345");
            //var testInt2Array = Encoding.UTF8.GetBytes("123001");
            //var testIntEArray = BitConverter.ToInt16(testIntArray);
            //var testInt2EArray = BitConverter.ToInt16(testInt2Array);
            //var testInt = memcmp(testStringArray, testString2Array, 5);
            //return memcmp(firstLine, secondLine, firstLine.Length);
            var newLine = (byte)'.';
            var index1 = Array.IndexOf(firstLine, (byte)newLine);
            var index2 = Array.IndexOf(secondLine, (byte)newLine);

            if (index1 < 0) 
                return index2 < 0 ? 0 : 1;

            if (index2 < 0)
                return index1 < 0 ? 0 : -1;

            var firstTextId = new byte[index1];
            Array.Copy(firstLine, firstTextId, index1);

            var secondTextId = new byte[index2];
            Array.Copy(secondLine, secondTextId, index2);

            var firstText = new byte[firstLine.Length - 1 - index1];
            Array.Copy(firstLine, index1 + 1, firstText, 0, firstLine.Length - 1 - index1);

            var secondText = new byte[secondLine.Length - 1 - index2];
            Array.Copy(secondLine, index2 + 1, secondText, 0, secondLine.Length - 1 - index2);

            return memcmp(firstLine, secondLine, firstLine.Length);

            if (memcmp(firstText, secondText, firstText.Length) < 0 || firstTextId.Length < secondTextId.Length || (firstTextId.Length == secondTextId.Length && memcmp(firstTextId, secondTextId, firstText.Length) < 0))
                return -1;
            else if (memcmp(firstText, secondText, firstText.Length) > 0 || firstTextId.Length > secondTextId.Length || (firstTextId.Length == secondTextId.Length && memcmp(firstTextId, secondTextId, firstText.Length) > 0))
                return 1;
            else
                return 0;
        }

        public async Task SortAsync(CancellationToken cancellationToken)
        {
            var testStringArray = Encoding.UTF8.GetBytes("1124. chats slepa kura");
            var testString2Array = Encoding.UTF8.GetBytes("11231. chats slepa kura");
            var result = Comparer(testStringArray, testString2Array);

            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var destinationFilePath = Path.Combine(_options.DestinationDirectory, _options.DestinationFileName);
            var fileSize = new FileInfo(sourceFilePath).Length;

            long offset = 0x00000000; // 256 megabytes
            long length = 0x20000000; // 512 megabytes
            var newLineByte = (byte)'\n';

            var chunkData = GetChunkData(fileSize, newLineByte);

            //var chunksForBj = chunkData.Take(100).ToDictionary(c => c.Key, c => c.Value);
            //var chunksForMain = chunkData.Skip(100).ToDictionary(c => c.Key, c => c.Value);
            //_backgroundQueue.Enqueue(chunksForBj);

            semaphore.Release(1);
            var tasks = new List<Task>();
            
            //using var sourceMmf = MemoryMappedFile.CreateFromFile(sourceFilePath, FileMode.Open, "dataProcessing");
            for (int i = 0; i < chunkData.Count / 20; i++)
            {
                tasks.Add(SortChunks(chunkData.Skip(i * 20).Take(20).ToDictionary(c => c.Key, c => c.Value), fileSize, sourceFilePath, destinationFilePath, cancellationToken));
            }

            await Task.Run(() => Task.WhenAll(tasks), cancellationToken);
        }

        private async Task SortChunks(Dictionary<long, int> chunkData, long fileSize, string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken)
        {
            var newLineByte = (byte)'\n';
            var dotByte = (byte)'.';
            if (cancellationToken.IsCancellationRequested)
                return;

            var stopwatch = new Stopwatch();
            var chunkCount = 1;


            long size = 0;
            foreach(var chunkInfo in chunkData)
            {
                using FileStream fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read);
                fs.Position = size;

                stopwatch.Start();
                byte[] data = new byte[chunkInfo.Value];
                await fs.ReadAsync(data, 0, chunkInfo.Value, cancellationToken);
                size = chunkInfo.Key;

                fs.Close();
                await fs.DisposeAsync();

                try
                {
                    await semaphore.WaitAsync();
                    data = GetSortedData(data, newLineByte, dotByte);
                    destinationFilePath = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");
                    using (FileStream dfs = new FileStream(destinationFilePath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        await dfs.WriteAsync(data, 0, chunkInfo.Value, cancellationToken);
                        await dfs.FlushAsync();
                        dfs.Close();
                        await dfs.DisposeAsync();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
                

                stopwatch.Stop();
                Console.WriteLine($"Written {chunkCount} chunk, time {stopwatch.Elapsed.TotalSeconds}, size {size / 1024f / 1024f:0.000}");
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
                var text = new byte[100];

                var idC = new byte[dotIndex];
                Array.Copy(sentence, idC, dotIndex);
                id = (new byte[12 - dotIndex]).Concat(idC).ToArray();
                Array.Copy(sentence, dotIndex + 1, text, 0, sentence.Length - 1 - dotIndex);

                sortedSentences.Add((sentence: sentence, text: text, id: id));
                startIndex = lineBreaks[j] + 1;
            }

            return sortedSentences
                .OrderBy(s => s.text, _comparer)
                .ThenBy(s => s.id, _comparer)
                .SelectMany(s => s.sentence)
                .ToArray();
        }

        private Dictionary<long, int> GetChunkData(long fileSize, byte newLineByte)
        {
            var filePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            long approximateChunkSize = 0x500000; // 100 megabytes
            int approximateLineLength = 100;
            var noOfChunks = (int)(fileSize / approximateChunkSize);

            long[] chunkEndIndexes = new long[noOfChunks];

            using (var sourceMmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "dataProcessing"))
            {
                long offset = approximateChunkSize;
                for (int i = 0; i < noOfChunks; i++)
                {
                    using (var sourceAccessor = sourceMmf.CreateViewAccessor(offset, approximateLineLength*2))
                    {
                        byte[] searchBytes = new byte[200];
                        sourceAccessor.ReadArray(0, searchBytes, 0, 200);
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
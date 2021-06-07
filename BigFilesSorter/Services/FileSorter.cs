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

            var tasks = new List<Task>();

            for (int i = 0; i < chunkData.Count / 50; i++)
            {
                tasks.Add(Task.Run(() => SortChunks3(chunkData.Skip(i * 50).Take(50).ToDictionary(c => c.Key, c => c.Value), fileSize, sourceFilePath, destinationFilePath, cancellationToken)));
            }

            await Task.Run(() => Task.WhenAll(tasks), cancellationToken);
        }

        private void SortChunks3(Dictionary<long, int> chunkData, long fileSize, string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken)
        {
            var newLineByte = (byte)'\n';
            var dotByte = (byte)'.';
            var dotIndex = 0;
            if (cancellationToken.IsCancellationRequested)
                return;

            var stopwatch = new Stopwatch();
            var chunkCount = 1;
            using var sourceMmf = MemoryMappedFile.CreateFromFile(sourceFilePath, FileMode.Open, "dataProcessing");
            foreach (var chunk in chunkData)
            {
                //if (chunkCount == 10) break;

                stopwatch.Start();
                var sourceAccessor = sourceMmf.CreateViewAccessor(chunk.Key, chunk.Value);
                byte[] data = new byte[chunk.Value];
                sourceAccessor.ReadArray(0, data, 0, chunk.Value);
                sourceAccessor.Dispose();

                var breakIndex = 0;
                var breaksCount = 0;
                var lineBreaksList = new List<long>();
                while (breakIndex >= 0)
                {
                    breakIndex = Array.IndexOf(data, newLineByte, breakIndex + 1);
                    if (breakIndex >= 0) {
                        lineBreaksList.Add(breakIndex);
                        breaksCount++;
                    }
                }

                var lineBreaks = lineBreaksList.ToArray();
                lineBreaksList = null;

                if (!lineBreaks.Any())
                    _logger.LogError($"File structure is not correct for offset {chunk.Key}");

                //var sortedSentences = new byte[lineBreaks.Length][][];
                var sortedSentences = new List<(byte[] sentence, byte[] text, byte[] id)>();

                for (long j = 0, startIndex = 0; j < lineBreaks.Length; j++)
                {
                    var sentence = new byte[lineBreaks[j] - startIndex];
                    Array.Copy(data, startIndex + 1, sentence, 0, lineBreaks[j] - startIndex);
                    
                    dotIndex = Array.IndexOf(sentence, dotByte);

                    var id = new byte[10];
                    var text = new byte[50];

                    if (dotIndex >= 0)
                    {
                        var idC = new byte[dotIndex];
                        Array.Copy(sentence, idC, dotIndex);
                        id = (new byte[10 - dotIndex]).Concat(idC).ToArray();

                        Array.Copy(sentence, dotIndex + 1, text, 0, sentence.Length - 1 - dotIndex);
                    }

                    sortedSentences.Add((sentence: sentence, text: text, id: id));

                    //sortedSentences[j][1] = new byte[lineBreaks[j] - startIndex];
                    //Array.Copy(data, startIndex + 1, sortedSentences[j][1], 0, lineBreaks[j] - startIndex);
                    //dotIndex = Array.IndexOf(sortedSentences[j][1], dotByte);

                    //sortedSentences[j][2] = new byte[dotIndex];
                    //Array.Copy(sortedSentences[j][1], sortedSentences[j][2], dotIndex);

                    //sortedSentences[j][3] = new byte[sortedSentences[j][1].Length - 1 - dotIndex];
                    //Array.Copy(sortedSentences[j][1], dotIndex + 1, sortedSentences[j][3], 0, sortedSentences[j][1].Length - 1 - dotIndex);

                    startIndex = lineBreaks[j];
                }

                //data = null;
                //var test = sortedSentences.ToArray();
                data = sortedSentences.OrderBy(s => s.text, _comparer).ThenBy(s => s.id, _comparer).SelectMany(s => s.sentence).ToArray();
                //Array.Sort(test, _comparer2);
                //data = sortedSentences.OrderBy(s => s.text).ThenBy(s => s.id).SelectMany(s => s.sentence).ToArray();


                sortedSentences = null;

                using var destMmf = MemoryMappedFile.CreateFromFile(Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt"), FileMode.OpenOrCreate, "dataProcessed", chunk.Value);
                using var destAccessor = destMmf.CreateViewAccessor(0, data.Length);
                destAccessor.WriteArray(0, data, 0, data.Length);

                destAccessor.Flush();
                destAccessor.Dispose();
                destMmf.Dispose();

                stopwatch.Stop();

                Console.WriteLine($"Written {chunkCount} chunk, time {stopwatch.Elapsed.TotalSeconds}, position {chunk.Key}, length {chunk.Value}");
                chunkCount++;

            }
        }

        private async Task SortChunks2(Dictionary<long, int> chunkData, long fileSize, string sourceFile, string destFile, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();

            using FileStream fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read);
            using BinaryReader br = new BinaryReader(fs, new UTF8Encoding());

            var chunkCount = 1;
            long size = 0;
            var fileName = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");
            var dfs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write);
            foreach (var chunk in chunkData)
            {
                //if (size >= 3000000000) break;

                if (chunkCount % 100 == 0)
                {
                    Console.WriteLine("Flushing....");
                    await dfs.FlushAsync();
                    dfs.Close();
                    dfs.Dispose();
                    dfs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write);
                    dfs.Position = size;
                    Console.WriteLine("We are back....");
                    Console.WriteLine($"Written {chunkCount} chunk, time {stopWatch.Elapsed.TotalSeconds}, position {chunk.Key}, length {chunk.Value}");
                }

                stopWatch.Start();

                byte[] data = new byte[chunk.Value];
                size += await fs.ReadAsync(data, 0, chunk.Value, cancellationToken);
                await dfs.WriteAsync(data, 0, chunk.Value, cancellationToken);
                data = null;
                chunkCount++;
            }
            await dfs.FlushAsync();
            dfs.Close();
            dfs.Dispose();

            //using FileStream fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read);
            //using BinaryReader br = new BinaryReader(fs, new UTF8Encoding());
            //using FileStream dfs = new FileStream(destFile, FileMode.OpenOrCreate, FileAccess.Write);

            //var chunkCount = 1;
            //long size = 0;
            //while (chunkCount < 100 && !cancellationToken.IsCancellationRequested)
            //{
            //    stopWatch.Start();
            //    byte[] chunk = new byte[CHUNK_SIZE];
            //    size += await br.BaseStream.ReadAsync(chunk, 0, CHUNK_SIZE, cancellationToken);
            //    await dfs.WriteAsync(chunk, 0, CHUNK_SIZE, cancellationToken);

            //    stopWatch.Stop();
            //    Console.WriteLine($"Written {chunkCount} chunk, time {stopWatch.Elapsed.TotalSeconds}, size {size / 1024f / 1024f:0.000}");
            //    chunkCount++;
            //};

            //var chunkCount = 1;
            //using var sourceMmf = MemoryMappedFile.CreateFromFile(sourceFile, FileMode.Open, "dataProcessing");
            //foreach (var chunk in chunkData)
            //{
            //    if (chunkCount == 10) break;
            //    stopWatch.Start();
            //    using var sourceAccessor = sourceMmf.CreateViewAccessor(chunk.Key, chunk.Value);
            //    using var destMmf = MemoryMappedFile.CreateFromFile(Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt"), FileMode.OpenOrCreate, "dataProcessed", chunk.Value);
            //    using var destAccessor = destMmf.CreateViewAccessor();
            //    byte[] data = new byte[chunk.Value];
            //    sourceAccessor.ReadArray(0, data, 0, chunk.Value);
            //    destAccessor.WriteArray(0, data, 0, data.Length);
            //    Console.WriteLine($"Written {chunkCount} chunk, time {stopWatch.Elapsed.TotalSeconds}, position {chunk.Key}, length {chunk.Value}");
            //    chunkCount++;
            //}
        }

        private async Task SortChunks(Dictionary<long, int> chunkData, long fileSize, CancellationToken cancellationToken)
        {
            var parralelTasksAllowed = 2;
            
            List<Task> tasks = new();
            foreach (var chunk in chunkData)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                tasks.Add(SortChunk(chunk, cancellationToken));

                if (tasks.Count() >= parralelTasksAllowed)
                {
                    await Task.Run(() => Task.WhenAll(tasks), cancellationToken);
                }
            }
            
        }

        private async Task SortChunk(KeyValuePair<long, int> chunkInfo, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            Console.WriteLine($"Queued sorting chunk: from {chunkInfo.Key} to {chunkInfo.Value}");
            var newLineByte = (byte)'\n';

            _backgroundQueue.Enqueue(chunkInfo);

            //using var sourcAccessorStream = sourceMmf.CreateViewStream(offset, length);
            //using var reader = new StreamReader(sourcAccessorStream);
            //using var destAccessor = destMmf.CreateViewStream(offset, length);
            //using var writer = new StreamWriter(destAccessor);
            //{
            //    byte[] sentence = reader.ReadBytes(length);

            //    sourcAccessor.ReadArray(0, sentence, 0, length);

            //    var lineBreaks = Enumerable.Range(0, sentence.Length).Where(idx => sentence[idx] == newLineByte).ToArray();
            //    if (!lineBreaks.Any())
            //        _logger.LogError($"File structure is not correct for offset {offset}");

            //    var sortedSentences = new byte[lineBreaks.Length][];

            //    for (int j = 0, startIndex = 0; j < lineBreaks.Length; j++)
            //    {
            //        sortedSentences[j] = new byte[lineBreaks[j] - startIndex];
            //        Array.Copy(sentence, startIndex + 1, sortedSentences[j], 0, lineBreaks[j] - startIndex);
            //        startIndex = lineBreaks[j];
            //    }

            //    //Array.Sort(sortedSentences, _comparer);

            //    //sentence = Encoding.Default.GetBytes("0000. testowa linijka linijk linijk linijk");
            //    var sortedSentencesArray = sortedSentences.SelectMany(s => s).ToArray();
            //    destAccessor.WriteArray(0, sortedSentencesArray, 0, sortedSentencesArray.Length);
            //    await writer.WriteAsync(await reader.ReadToEndAsync());

            //    Console.WriteLine($"Finished sorting chunk: from {offset} to {length}");
            //}
        }

        private Dictionary<long, int> GetChunkData(long fileSize, byte newLineByte)
        {
            var filePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            long approximateChunkSize = 0x1000000; // 100 megabytes
            int approximateLineLength = 100;
            var noOfChunks = (int)(fileSize / approximateChunkSize);

            long[] chunkEndIndexes = new long[noOfChunks];

            using (var sourceMmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "dataProcessing"))
            {
                long offset = approximateChunkSize;
                for (int i = 0; i < noOfChunks; i++)
                {
                    using (var sourcAccessor = sourceMmf.CreateViewAccessor(offset, approximateLineLength*2))
                    {
                        byte[] searchBytes = new byte[200];
                        sourcAccessor.ReadArray(0, searchBytes, 0, 200);
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
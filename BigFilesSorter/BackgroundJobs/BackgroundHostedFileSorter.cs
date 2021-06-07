using BigFilesSorter.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace BigFilesSorter.BackgroundJobs
{
    public class BackgroundHostedFileSorter : BackgroundService
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        private readonly IBackgroundFileSorterQueue _writerQueue;
        private readonly ILogger<BackgroundHostedFileSorter> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly SorterOptions _options;

        public BackgroundHostedFileSorter(
            IBackgroundFileSorterQueue writerQueue,
            ILogger<BackgroundHostedFileSorter> logger,
            IOptions<SorterOptions> options,
            IHostApplicationLifetime appLifetime)
        {
            _writerQueue = writerQueue ?? throw new ArgumentNullException(nameof(writerQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appLifetime = appLifetime;
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

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                  {
                      await DoWork(cancellationToken);
                  }, cancellationToken);

                Task.Run(async () =>
                {
                    await DoWork(cancellationToken);
                }, cancellationToken);

                Task.Run(async () =>
                {
                    await DoWork(cancellationToken);
                }, cancellationToken);
            });

            return Task.CompletedTask;
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            var destinationFilePath = Path.Combine(_options.DestinationDirectory, $"{Guid.NewGuid()}.txt");
            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var fileSize = new FileInfo(sourceFilePath).Length;

            // Dequeue and execute texts until the application is stopped
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_writerQueue.IsEmpty())
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                try
                {
                    if (!_writerQueue.TryDequeue(out Dictionary<long, int> chunkData))
                        continue;



                    var tasks = new List<Task>();

                    using var sourceMmf = MemoryMappedFile.OpenExisting("dataProcessing");
                    for (int i = 0; i < chunkData.Count / 20; i++)
                    {
                        tasks.Add(Task.Run(() => SortChunks3(chunkData.Skip(i * 20).Take(20).ToDictionary(c => c.Key, c => c.Value), fileSize, sourceMmf, destinationFilePath, cancellationToken), cancellationToken));
                    }

                    await Task.Run(() => Task.WhenAll(tasks), cancellationToken);

                    //using var rs = new FileStream(sourceFileName, FileMode.Open);
                    //using var reader = new BinaryReader(rs);
                    //using var ws = new FileStream(outputFileName, FileMode.CreateNew);
                    //using var writer = new BinaryWriter(ws);
                    //{
                    //    reader.BaseStream.Position = chunk.Key;
                    //    //sourcAccessor.ReadArray(0, sentence, 0, length);

                    //    //var lineBreaks = Enumerable.Range(0, sentence.Length).Where(idx => sentence[idx] == newLineByte).ToArray();
                    //    //if (!lineBreaks.Any())
                    //    //    _logger.LogError($"File structure is not correct for offset {offset}");

                    //    //var sortedSentences = new byte[lineBreaks.Length][];

                    //    //for (int j = 0, startIndex = 0; j < lineBreaks.Length; j++)
                    //    //{
                    //    //    sortedSentences[j] = new byte[lineBreaks[j] - startIndex];
                    //    //    Array.Copy(sentence, startIndex + 1, sortedSentences[j], 0, lineBreaks[j] - startIndex);
                    //    //    startIndex = lineBreaks[j];
                    //    //}

                    //    //Array.Sort(sortedSentences, _comparer);

                    //    //sentence = Encoding.Default.GetBytes("0000. testowa linijka linijk linijk linijk");
                    //    //var sortedSentencesArray = sortedSentences.SelectMany(s => s).ToArray();
                    //    //destAccessor.WriteArray(0, sortedSentencesArray, 0, sortedSentencesArray.Length);

                    //    writer.Write(reader.ReadBytes(chunk.Value));
                    //    Console.WriteLine($"Finished sorting chunk: from {chunk.Key} to {chunk.Value}");

                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogInformation("Background file writer is shutting down...");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured during execution of a background task");
                }
            }
        }

        private void SortChunks3(Dictionary<long, int> chunkData, long fileSize, MemoryMappedFile sourceMmf, string destinationFilePath, CancellationToken cancellationToken)
        {
            var newLineByte = (byte)'\n';
            var dotByte = (byte)'.';
            var dotIndex = 0;
            if (cancellationToken.IsCancellationRequested)
                return;

            var stopwatch = new Stopwatch();
            var chunkCount = 1;
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
                    if (breakIndex >= 0)
                    {
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

                Console.WriteLine($"BJ: Written {chunkCount} chunk, time {stopwatch.Elapsed.TotalSeconds}, position {chunk.Key}, length {chunk.Value}");
                chunkCount++;

            }
        }
    }
}

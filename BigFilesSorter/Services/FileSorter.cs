﻿using BigFilesSorter.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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
        private readonly SorterOptions _options;

        public FileSorter(
            ILogger<FileSorter> logger,
            IOptions<SorterOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        static readonly IComparer<byte[]> _comparer = Comparer<byte[]>.Create(
            (byte[] firstLine, byte[] secondLine) => Comparer(firstLine, secondLine)
        );

        static int Comparer(byte[] firstLine, byte[] secondLine)
        {
            return memcmp(firstLine, secondLine, firstLine.Length);
            var chr = '.';
            var index1 = Array.IndexOf(firstLine, (byte)chr);
            var index2 = Array.IndexOf(secondLine, (byte)chr);

            if (index1 < 0) 
                return 1;

            if (index2 < 0) 
                return -1;

            var firstTextId = new byte[index1];
            Array.Copy(firstLine, firstTextId, index1);

            var secondTextId = new byte[index2];
            Array.Copy(secondLine, secondTextId, index2);

            var firstText = new byte[firstLine.Length - 1 - index1];
            Array.Copy(firstLine, index1 + 1, firstText, 0, firstLine.Length - 1 - index1);

            var secondText = new byte[secondLine.Length - 1 - index2];
            Array.Copy(secondLine, index2 + 1, secondText, 0, secondLine.Length - 1 - index2);

            return 
                (memcmp(firstText, secondText, firstText.Length) < 0 
                || firstTextId.Length < secondTextId.Length
                || firstTextId.Length == secondTextId.Length && memcmp(firstTextId, secondTextId, firstText.Length) < 0) 
                    ? -1 
                    : 1;
        }

        public async Task SortAsync(CancellationToken cancellationToken)
        {
            var testStringArray = Encoding.UTF8.GetBytes("11456. ahats slepa kura");
            var testString2Array = Encoding.UTF8.GetBytes("1123. chats slepa kura");
            var result = Comparer(testStringArray, testString2Array);



            //var testString = memcmp(testStringArray, testString2Array, testString2Array.Length);

            //var testIntArray = Encoding.UTF8.GetBytes("1.345");
            //var testInt2Array = Encoding.UTF8.GetBytes("123001");
            //var testIntEArray = BitConverter.ToInt16(testIntArray);
            //var testInt2EArray = BitConverter.ToInt16(testInt2Array);
            //var testInt = memcmp(testStringArray, testString2Array, 5);


            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var destinationFilePath = Path.Combine(_options.DestinationDirectory, _options.DestinationFileName);
            var fileSize = new FileInfo(sourceFilePath).Length;

            //using (FileStream fsSource = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            //{
            //    byte[] bytes = new byte[fsSource.Length];
            //    int numBytesToRead = (int)fsSource.Length;
            //    int numBytesRead = 0;
            //    while (numBytesToRead > 0)
            //    {
            //        // Read may return anything from 0 to numBytesToRead.
            //        int n = fsSource.Read

            //        // Break when the end of the file is reached.
            //        if (n == 0)
            //            break;

            //        numBytesRead += n;
            //        numBytesToRead -= n;
            //    }
            //    numBytesToRead = bytes.Length;
            //}

            await Class1.Split(sourceFilePath);

            long offset = 0x00000000; // 256 megabytes
            long length = 0x20000000; // 512 megabytes
            var newLineByte = (byte)'\n';

            //var chunkData = GetChunkData(fileSize, newLineByte);
            //await SortChunks(chunkData, fileSize, cancellationToken);
        }

        private async Task SortChunks(Dictionary<long, int> chunkData, long fileSize, CancellationToken cancellationToken)
        {
            var parralelTasksAllowed = 20;

            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var destinationFilePath = Path.Combine(_options.DestinationDirectory, _options.DestinationFileName);

            using var destMmf = MemoryMappedFile.CreateFromFile(destinationFilePath, FileMode.CreateNew, "dataProcessed", fileSize);
            using (var sourceMmf = MemoryMappedFile.CreateFromFile(sourceFilePath, FileMode.Open, "dataProcessing"))
            {
                List<Action> tasks = new();
                foreach (var chunk in chunkData)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    tasks.Add(() => SortChunk(chunk.Key, chunk.Value, fileSize, destMmf, sourceMmf, cancellationToken));

                    if (tasks.Count() >= parralelTasksAllowed)
                    {
                        Parallel.Invoke(tasks.ToArray());
                        tasks = new List<Action>();
                    }
                }
            }
        }

        private void SortChunk(long offset, int length, long fileSize, MemoryMappedFile destMmf, MemoryMappedFile sourceMmf, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            Console.WriteLine($"Started sorting chunk: from {offset} to {length}");
            var newLineByte = (byte)'\n';

            using var destAccessor = destMmf.CreateViewAccessor(offset, length);
            using var sourcAccessor = sourceMmf.CreateViewAccessor(offset, length);
            {
                byte[] sentence = new byte[length];

                sourcAccessor.ReadArray(0, sentence, 0, length);

                var lineBreaks = Enumerable.Range(0, sentence.Length).Where(idx => sentence[idx] == newLineByte).ToArray();
                if (!lineBreaks.Any())
                    _logger.LogError($"File structure is not correct for offset {offset}");

                var sortedSentences = new byte[lineBreaks.Length][];

                for (int j = 0, startIndex = 0; j < lineBreaks.Length; j++)
                {
                    sortedSentences[j] = new byte[lineBreaks[j] - startIndex];
                    Array.Copy(sentence, startIndex + 1, sortedSentences[j], 0, lineBreaks[j] - startIndex);
                    startIndex = lineBreaks[j];
                }

                //Array.Sort(sortedSentences, _comparer);

                //sentence = Encoding.Default.GetBytes("0000. testowa linijka linijk linijk linijk");
                var sortedSentencesArray = sortedSentences.SelectMany(s => s).ToArray();
                destAccessor.WriteArray(0, sortedSentencesArray, 0, sortedSentencesArray.Length);

                Console.WriteLine($"Finished sorting chunk: from {offset} to {length}");
            }
        }

        private Dictionary<long, int> GetChunkData(long fileSize, byte newLineByte)
        {
            var filePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            long approximateChunkSize = 0x2000000; // 512 megabytes
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
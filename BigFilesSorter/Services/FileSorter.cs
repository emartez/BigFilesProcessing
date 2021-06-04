using BigFilesSorter.Configurations;
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

        static readonly IComparer<byte[]> _comparer = Comparer<byte[]>.Create((byte[] firstLine, byte[] secondLine) => {
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
        });

        static int Comparer(ref byte[] firstLine, ref byte[] secondLine)
        {
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
            var result = Comparer(ref testStringArray, ref testString2Array);



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

            //await Class1.Split(filePath);
            long offset = 0x00000000; // 256 megabytes
            long length = 0x20000000; // 512 megabytes
            var chr = (byte)'\n';
            using (var destMmf = MemoryMappedFile.CreateFromFile(destinationFilePath, FileMode.CreateNew, "dataProcessed", 50000))
            {
                using (var destAccessor = destMmf.CreateViewAccessor()) 
                {
                    using (var sourceMmf = MemoryMappedFile.CreateFromFile(sourceFilePath, FileMode.Open, "dataProcessing"))
                    {
                        using (var sourcAccessor = sourceMmf.CreateViewAccessor(offset, length))
                        {
                            var sentenceToCmp = Encoding.UTF8.GetBytes("ktyroi");

                            // Make changes to the view.
                            for (long i = 0; i < 1; i += 2)
                            {
                                byte[] sentence = new byte[50000];
                                sourcAccessor.ReadArray(i, sentence, 0, 50000);

                                var endIndex = Array.LastIndexOf(sentence, chr);

                                var lineBreaks = Enumerable.Range(0, sentence.Length).Where(i => sentence[i] == chr).ToArray();
                                var sortedSentences = new byte[lineBreaks.Length][];

                                for (int j = 0, startIndex = 0; j < lineBreaks.Length; j++)
                                {
                                    sortedSentences[j] = new byte[lineBreaks[j] - startIndex];
                                    Array.Copy(sentence, j, sortedSentences[j], 0, lineBreaks[j] - startIndex);
                                    startIndex = lineBreaks[j];
                                }

                                Array.Sort(sortedSentences, )

                                //sentence = Encoding.Default.GetBytes("0000. testowa linijka linijk linijk linijk");
                                destAccessor.WriteArray(i, sentence, 0, sentence.Length);
                            }
                        }
                    } 
                }
            }

        }
    }
}
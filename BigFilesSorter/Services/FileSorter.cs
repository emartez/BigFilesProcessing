using BigFilesSorter.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public class FileSorter : IFileSorter
    {
        private readonly ILogger<FileSorter> _logger;
        private readonly ISorterEngine _sorterEngine;
        private readonly SorterOptions _options;

        public FileSorter(
            ILogger<FileSorter> logger,
            IOptions<SorterOptions> options,
            ISorterEngine sorterEngine)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sorterEngine = sorterEngine;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task SortAsync(CancellationToken cancellationToken)
        {
            var sourceFilePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            var fileSize = new FileInfo(sourceFilePath).Length;
            var chunkData = GetChunkBoundariesData(fileSize);
            await _sorterEngine.SplitToChunksParallely(chunkData, cancellationToken);
            await _sorterEngine.MergeTheChunks(_options.DestinationDirectory, _options.DestinationFileName, cancellationToken);
        }

        private Dictionary<long, int> GetChunkBoundariesData(long fileSize)
        {
            var chunkBoundaries = GetChunksBoundaries(fileSize);
            var chunkData = new Dictionary<long, int>();
            long chunkStartPoint = 0;

            for (int i = 0; i < chunkBoundaries.Length; i++)
            {
                var offset = chunkBoundaries[i];
                chunkData[chunkStartPoint] = (int)(offset - chunkStartPoint + 1);
                chunkStartPoint = offset + 1;
            }

            return chunkData;
        }

        private long[] GetChunksBoundaries(long fileSize)
        {
            var filePath = Path.Combine(_options.SourceDirectory, _options.SourceFileName);
            long approximateChunkSize = _options.ApproximateChunkFileSizeMb * 1024 * 1024;
            var newLineByte = (byte)'\n';
            var noOfChunks = (int)(fileSize / approximateChunkSize);

            long[] chunkBoundaries = new long[noOfChunks];

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

                        chunkBoundaries[i] = offset + newLineIndex;

                        if (offset + approximateChunkSize > fileSize)
                        {
                            chunkBoundaries[i] = fileSize - 1;
                            break;
                        }

                        offset += approximateChunkSize;
                    }
                }
                sourceMmf.Dispose();
            };

            return chunkBoundaries;
        }
    }
}
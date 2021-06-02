using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileWriter
    {
        internal float TotalSizeInGb => (float)TotalSize / 1000 / 1000 / 1000;
        internal float CurrentSizeInGb => (float)CurrentSize / 1000 / 1000 / 1000;
        internal float CurrentWriteQueueLength => _textToWrite.Count;

        private static long TotalSize => _textToWrite.Sum(t => (long)t.Length) + _processedSize;
        private static long CurrentSize => _textToWrite.Sum(t => (long)t.Length);
        private static long _processedSize;
        private static bool _isRunning = false;
        private static bool _chunksFilesProcessing;

        //private static readonly object WriterLock = new object();
        private static readonly SemaphoreSlim writerLock = new SemaphoreSlim(1,1);
        private static ConcurrentQueue<StringBuilder> _textToWrite = new ConcurrentQueue<StringBuilder>();
        private readonly GeneratorOptions _generateOptions;
        private CancellationToken _token;
        private string _filePath;

        public FileWriter(IOptions<GeneratorOptions> generateOptions)
        {
            _generateOptions = generateOptions.Value;
            _filePath = Path.Combine(_generateOptions.ResultDirectory, _generateOptions.ResultFileName);
        }

        public void Run(CancellationToken cancellationToken, bool chunksFilesProcessing = false)
        {
            if (!_isRunning)
            {
                writerLock.Wait(_token);
                try
                {
                    if (!_isRunning)
                    {
                        _token = cancellationToken;
                        _isRunning = true;
                        _textToWrite.Clear();
                        _processedSize = 0;
                        _chunksFilesProcessing = chunksFilesProcessing;

                        // This is the task that will run
                        // in the background and do the actual file writing
                        Task.Run(() => WriteToFile(), _token);
                    }
                } finally
                {
                    writerLock.Release();
                }
            }
        }

        public async Task Stop()
        {
            if (_isRunning)
            {
                if (_token.IsCancellationRequested)
                    return;

                await writerLock.WaitAsync(_token);
                try
                {
                    if (_textToWrite.Any())
                    {
                        var queuePositions = _textToWrite.Count;
                        await Task.Delay(100, _token);

                        while (_textToWrite.Any() && queuePositions > _textToWrite.Count)
                        {
                            queuePositions = _textToWrite.Count;
                            await Task.Delay(100, _token);
                        }
                    }

                    if (_isRunning)
                    {
                        _isRunning = false;
                        _textToWrite.Clear();
                        _processedSize = 0;
                    }
                } finally
                {
                    writerLock.Release();
                }
            }
        }

        /// The public method where a thread can ask for a text
        /// to be written.
        public void WriteText(StringBuilder text)
        {
            _textToWrite.Enqueue(text);
        }

        /// The actual file writer, running
        /// in the background.
        private async void WriteToFile()
        {
            while (_isRunning)
            {
                if (_token.IsCancellationRequested)
                {
                    return;
                }

                //while (mergedFilesNumber < txtFiles.Length)
                //{
                //    using (StreamWriter writer = new StreamWriter(resultFile))
                //    {
                //        for (int i = 0; i < 500; i++)
                //        {
                //            mergedFilesNumber++;
                //            using (StreamReader reader = File.OpenText(txtFiles[i]))
                //            {
                //                await writer.WriteAsync(await reader.ReadToEndAsync());
                //                reader.Close();
                //            }

                //            if (mergedFilesNumber % 10 == 0)
                //                Console.WriteLine($"Merged {mergedFilesNumber} files");
                //        }

                //        writer.Flush();
                //        writer.Close();
                //    }

                //    await Task.Delay(200);
                //}

                var iterations = 0;
                using (StreamWriter w = new StreamWriter(_filePath))
                {
                    while (iterations < 50 && _isRunning && !_token.IsCancellationRequested && _textToWrite.TryDequeue(out StringBuilder text))
                    {
                        _processedSize += text.Length;
                        await w.WriteAsync(text, _token);
                        
                        iterations++;
                    }

                    w.Flush();
                    w.Close();
                }

                Thread.Sleep(100);

                //if (_chunksFilesProcessing)
                //    await WriteToFileChunks();
                //else
                //    await WriteToOneFile();

                //await Task.Delay(100, _token);
            }
        }

        private async Task WriteToOneFile()
        {
            using (StreamWriter w = File.AppendText(_filePath))
            {
                int currentlyProcessedSize = 0;
                int currentlyProcessedSizeInMb = 0;
                while (_isRunning && !_token.IsCancellationRequested && _textToWrite.TryDequeue(out StringBuilder text))
                {
                    _processedSize += text.Length;
                    await w.WriteAsync(text, _token);

                    currentlyProcessedSize += text.Length;
                    currentlyProcessedSizeInMb = currentlyProcessedSize / 1000 / 1000;

                    if (currentlyProcessedSizeInMb > 1000)
                        break;
                }
                await w.FlushAsync();
            }
        }

        private async Task WriteToFileChunks()
        {
            _filePath = Path.Combine(_generateOptions.DestinationDirectory, $"{Guid.NewGuid()}.txt");
            using StreamWriter w = File.AppendText(_filePath);
            if (_isRunning && !_token.IsCancellationRequested && _textToWrite.TryDequeue(out StringBuilder text))
            {
                _processedSize += text.Length;
                await w.WriteAsync(text, _token);
                await w.FlushAsync();
            }
        }
    }
}

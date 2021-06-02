using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileWriter
    {
        internal float TotalSizeInGb => (float)TotalSize / 1000 / 1000 / 1000;

        private static long TotalSize => _textToWrite.Sum(t => (long)t.Length) + _processedSize;
        private static long _processedSize;
        private static bool _isRunning = false;

        //private static readonly object WriterLock = new object();
        private static readonly SemaphoreSlim writerLock = new SemaphoreSlim(1,1);
        private static ConcurrentQueue<string> _textToWrite = new ConcurrentQueue<string>();
        private readonly GeneratorOptions _generateOptions;
        private CancellationTokenSource _source = new CancellationTokenSource();
        private CancellationToken _token;
        private readonly string _filePath;

        public FileWriter(IOptions<GeneratorOptions> generateOptions)
        {
            _token = _source.Token;
            _generateOptions = generateOptions.Value;
            _filePath = Path.Combine(_generateOptions.DestinationDirectory, _generateOptions.DestinationFileName);
        }

        public void Run()
        {
            if (!_isRunning)
            {
                writerLock.Wait();
                try
                {
                    if (!_isRunning)
                    {
                        _isRunning = true;
                        _textToWrite.Clear();
                        _processedSize = 0;

                        // This is the task that will run
                        // in the background and do the actual file writing
                        Task.Run(WriteToFile, _token);
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
                await writerLock.WaitAsync();
                try
                {
                    if (_textToWrite.Any())
                    {
                        var queuePositions = _textToWrite.Count;
                        await Task.Delay(100);

                        while (_textToWrite.Any() && queuePositions > _textToWrite.Count)
                        {
                            queuePositions = _textToWrite.Count;
                            await Task.Delay(100);
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
        public void WriteText(string text)
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

                using (StreamWriter w = File.AppendText(_filePath))
                {
                    while (_isRunning && _textToWrite.TryDequeue(out string text))
                    {
                        _processedSize += text.Length;
                        await w.WriteAsync(text);
                    }
                    w.Flush();
                }

                await Task.Delay(100);
            }
        }
    }
}

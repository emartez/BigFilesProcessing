using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileWriter
    {
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

            // This is the task that will run
            // in the background and do the actual file writing
            Task.Run(WriteToFile, _token);
        }

        /// The public method where a thread can ask for a line
        /// to be written.
        public void WriteText(string text)
        {
            _textToWrite.Enqueue(text);
        }

        /// The actual file writer, running
        /// in the background.
        private async void WriteToFile()
        {
            while (true)
            {
                if (_token.IsCancellationRequested)
                {
                    return;
                }
                using (StreamWriter w = File.AppendText(_filePath))
                {
                    while (_textToWrite.TryDequeue(out string text))
                    {
                        await w.WriteAsync(text);
                    }
                    w.Flush();
                    Thread.Sleep(100);
                }
            }
        }
    }
}

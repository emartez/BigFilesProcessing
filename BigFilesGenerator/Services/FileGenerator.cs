using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileContentGenerator : IFileContentGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;
        private readonly ILogger<FileContentGenerator> _logger;
        private readonly FileWriter _fileWriter;
        private readonly GeneratorOptions _generateOptions;

        public FileContentGenerator(ISentencesGenerator sentencesGenerator, 
            ILogger<FileContentGenerator> logger, 
            IOptions<GeneratorOptions> generateOptions,
            FileWriter fileWriter)
        {
            _sentencesGenerator = sentencesGenerator;
            _logger = logger;
            _fileWriter = fileWriter;
            _generateOptions = generateOptions.Value;
        }

        public async Task Generate(byte maxFileSizeInGb)
        {
            Console.WriteLine($"Recreating directory {_generateOptions.DestinationDirectory}");

            Directory.Delete(_generateOptions.DestinationDirectory, true);
            Directory.CreateDirectory(_generateOptions.DestinationDirectory);

            Console.WriteLine($"Directory {_generateOptions.DestinationDirectory} has been recreated");

            var stopWatch = new Stopwatch();
            float totalSizeInGb = 0;
            int iterate = 0;
            int noOfFiles = 0;
            do
            {
                List<Task> tasks = new();
                stopWatch.Start();

                for (int i = 0; i < 3; i++)
                {
                    var chunkName = $"data_{noOfFiles++}.txt";
                    tasks.Add(GenerateChunk(_generateOptions.DestinationDirectory, chunkName));

                    //await GenerateChunk(destinationDirectory, $"data.txt");
                }

                await Task.WhenAll(tasks);
                stopWatch.Stop();

                DirectoryInfo info = new DirectoryInfo(_generateOptions.DestinationDirectory);
                totalSizeInGb = info.EnumerateFiles().Sum(file => file.Length) / 1024f / 1024f / 1024f;
                var totalSizePercentage = totalSizeInGb / maxFileSizeInGb * 100;

                Console.WriteLine($"Total size of directory {_generateOptions.DestinationDirectory} is {totalSizeInGb:0.00}[GB] ({totalSizePercentage:0.00}%)");
                iterate++;
            } while (totalSizeInGb < maxFileSizeInGb && iterate < 50);

            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
        }

        private async Task GenerateChunk(string destinationDirectory, string destinationFile)
        {
            var process = Process.GetCurrentProcess();
            var sentences = await _sentencesGenerator.GenerateData(5000000);
            //var size = sentences.
            //var loops = 100; //~50MB

            var destinationFilePath = Path.Combine(destinationDirectory, destinationFile);

            Console.WriteLine($"Scheduled generating file: {destinationFilePath}");
            _fileWriter.WriteText(sentences);
        }
    }
}

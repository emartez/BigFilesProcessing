using BigFilesGenerator.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileGenerator : IFileGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;
        private readonly ILogger<FileGenerator> _logger;
        private readonly FileWriter _fileWriter;
        private readonly GeneratorOptions _generateOptions;

        public FileGenerator(ISentencesGenerator sentencesGenerator, 
            ILogger<FileGenerator> logger, 
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
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            float totalSizeInGb = 0;
            int iterate = 0;
            int noOfFiles = 0;

            while (totalSizeInGb < maxFileSizeInGb && iterate < 100)
            {
                List<Task> tasks = new();
                for (int i = 0; i < 80; i++)
                {
                    var chunkName = $"data_{noOfFiles++}.txt";
                    tasks.Add(GenerateChunk(_generateOptions.DestinationDirectory, chunkName));

                    //await GenerateChunk(destinationDirectory, $"data.txt");
                }

                Task t = Task.WhenAll(tasks);
                try
                {
                    await t;
                }
                catch { }

                if (t.Status == TaskStatus.Faulted)
                    Console.WriteLine("Generation attempt failed");

                totalSizeInGb = _fileWriter.TotalSizeInGb;
                var totalSizePercentage = totalSizeInGb / maxFileSizeInGb * 100; //%
                Console.WriteLine($"Total size of result file is {totalSizeInGb:0.00}[GB] ({totalSizePercentage:0.00}%)");

                iterate++;
            };

            stopWatch.Stop();
            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
        }

        private async Task GenerateChunk(string destinationDirectory, string destinationFile)
        {
            //var process = Process.GetCurrentProcess();
            var sentences = await _sentencesGenerator.GenerateData(20000);

            //var destinationFilePath = Path.Combine(destinationDirectory, destinationFile);
            //Console.WriteLine($"Scheduled generating file: {destinationFilePath}");

            _fileWriter.WriteText(sentences);
        }
    }
}

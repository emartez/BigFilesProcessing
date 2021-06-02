using BigFilesGenerator.Configurations;
using BigFilesGenerator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Startup
{
    public class StartupService
    {
        private readonly ILogger<StartupService> _logger;
        private readonly GeneratorOptions _generateOptions;
        private readonly IFileGenerator _fileGenerator;
        private readonly FileWriter _fileWriter;
        static readonly CancellationTokenSource s_cts = new CancellationTokenSource();

        public StartupService(ILogger<StartupService> logger, IOptions<GeneratorOptions> generateOptions
            , IFileGenerator fileGenerator, FileWriter fileWriter)
        {
            _logger = logger;
            _generateOptions = generateOptions.Value;
            _fileGenerator = fileGenerator;
            _fileWriter = fileWriter;
        }

        public async Task Run()
        {
            _logger.LogInformation("Application started.");
            await IOService.RecreateDirectory(_generateOptions.DestinationDirectory);
            _logger.LogInformation($"Destination recreated. Data path is {_generateOptions.DestinationDirectory}");
            var expectedFileSize = GetExpectedFileSize();

            Console.WriteLine("Press the ENTER key to cancel...\n");
            Task cancelTask = Task.Run(() =>
            {
                while (Console.ReadKey().Key != ConsoleKey.Enter)
                {
                    Console.WriteLine("Press the ENTER key to cancel...");
                }

                Console.WriteLine("\nENTER key pressed: cancelling operations...\n");
                s_cts.Cancel();
            });

            _fileWriter.Run(s_cts.Token);
            await Task.WhenAny(cancelTask, _fileGenerator.Generate(expectedFileSize, s_cts.Token));
            Console.WriteLine($"Data generation finished, wait for saving file...");
            await _fileWriter.Stop();
        }

        private byte GetExpectedFileSize()
        {
            Console.WriteLine($"\nProvide approximate file size you want to be generated in GB (1-{_generateOptions.MaxFileSizeInGb} GB) -- 1 GB by default: ");
            var expectedFileSizeInput = Console.ReadLine();

            if (!byte.TryParse(expectedFileSizeInput, out byte expectedFileSize))
                expectedFileSize = 1;

            if (expectedFileSize > _generateOptions.MaxFileSizeInGb)
                throw new InvalidDataException($"Incorrect file size. It should be in range 1-{_generateOptions.MaxFileSizeInGb} GB");

            return expectedFileSize;
        }
    }
}

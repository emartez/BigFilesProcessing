using BigFilesGenerator.Configurations;
using BigFilesGenerator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace BigFilesGenerator.Startup
{
    public class StartupService : IStartupService
    {
        private readonly ILogger<StartupService> _logger;
        private readonly GeneratorOptions _generateOptions;
        private readonly IFileGenerator _fileGenerator;

        public StartupService(ILogger<StartupService> logger, IOptions<GeneratorOptions> generateOptions, IFileGenerator fileGenerator)
        {
            _logger = logger;
            _generateOptions = generateOptions.Value;
            _fileGenerator = fileGenerator;
        }

        public void Run()
        {
            _logger.LogInformation("Startup service started");

            Console.WriteLine("\nProvide approximate file size you want to be generated in GB (1-100 GB) -- 1 GB by default: ");
            var expectedFileSizeInput = Console.ReadLine();

            if (!byte.TryParse(expectedFileSizeInput, out byte expectedFileSize))
                expectedFileSize = 1;

            if (expectedFileSize > _generateOptions.MaxFileSizeInGb)
                throw new InvalidDataException("Incorrect file size. It should be in range 1-100 GB");

            var destinationFile = Path.Combine(_generateOptions.DestinationDirectory, _generateOptions.DestinationFileName);
            _fileGenerator.Generate(destinationFile, expectedFileSize);
        }
    }
}

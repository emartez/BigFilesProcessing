﻿using BigFilesGenerator.Configurations;
using BigFilesGenerator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BigFilesGenerator.Startup
{
    public class StartupService
    {
        private readonly ILogger<StartupService> _logger;
        private readonly GeneratorOptions _generateOptions;
        private readonly IFileContentGenerator _fileGenerator;

        public StartupService(ILogger<StartupService> logger, IOptions<GeneratorOptions> generateOptions, IFileContentGenerator fileGenerator)
        {
            _logger = logger;
            _generateOptions = generateOptions.Value;
            _fileGenerator = fileGenerator;
        }

        public async Task Run()
        {
            _logger.LogInformation("Startup service started");

            Console.WriteLine($"\nProvide approximate file size you want to be generated in GB (1-{_generateOptions.MaxFileSizeInGb} GB) -- 1 GB by default: ");
            var expectedFileSizeInput = Console.ReadLine();

            if (!byte.TryParse(expectedFileSizeInput, out byte expectedFileSize))
                expectedFileSize = 1;

            if (expectedFileSize > _generateOptions.MaxFileSizeInGb)
                throw new InvalidDataException($"Incorrect file size. It should be in range 1-{_generateOptions.MaxFileSizeInGb} GB");

            _logger.LogInformation($"Root data path is {_generateOptions.DestinationDirectory}");
            await _fileGenerator.Generate(expectedFileSize);
        }
    }
}

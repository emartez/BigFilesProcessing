using BigFilesGenerator.Configurations;
using BigFilesGenerator.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Startup
{
    public class StartupService : IHostedService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ILogger<StartupService> _logger;
        private readonly GeneratorOptions _options;
        private readonly IFileGenerator _fileGenerator;

        public StartupService(IHostApplicationLifetime appLifetime, 
            ILogger<StartupService> logger, 
            IOptions<GeneratorOptions> options, 
            IFileGenerator fileGenerator)
        {
            _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _fileGenerator = fileGenerator ?? throw new ArgumentNullException(nameof(fileGenerator));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        _logger.LogInformation("Application started.");
                        await IoService.RecreateDirectory(_options.DestinationDirectory, _logger);
                        await IoService.RecreateDirectory(_options.ResultDirectory, _logger);

                        var expectedFileSize = GetExpectedFileSize();
                        await Run(expectedFileSize, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception!");
                    }
                    finally
                    {
                        // Stop the application once the work is done
                        _appLifetime.StopApplication();
                    }
                });
            });

            return Task.CompletedTask;
        }

        private async Task Run(byte expectedFileSize, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            await _fileGenerator.Generate(expectedFileSize, cancellationToken);
            stopWatch.Stop();
            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
            Console.WriteLine($"Generation finished");
            Console.ReadLine();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private byte GetExpectedFileSize()
        {
            Console.WriteLine($"\nProvide approximate file size you want to be generated in GB (1-{_options.MaxFileSizeInGb} GB) -- 1 GB by default: ");
            var expectedFileSizeInput = Console.ReadLine();

            if (!byte.TryParse(expectedFileSizeInput, out byte expectedFileSize))
                expectedFileSize = 1;

            if (expectedFileSize > _options.MaxFileSizeInGb)
                throw new InvalidDataException($"Incorrect file size. It should be in range 1-{_options.MaxFileSizeInGb} GB");

            return expectedFileSize;
        }
    }
}

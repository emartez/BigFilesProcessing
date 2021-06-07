using BigFilesSorter.Configurations;
using BigFilesSorter.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Startup
{
    public class StartupService : IHostedService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ILogger<StartupService> _logger;
        private readonly IFileSorter _fileSorter;
        private readonly SorterOptions _options;

        public StartupService(IHostApplicationLifetime appLifetime,
            ILogger<StartupService> logger,
            IOptions<SorterOptions> options,
            IFileSorter fileSorter)
        {
            _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSorter = fileSorter;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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
                        //await IoService.RecreateDirectory(_options.DestinationDirectory, _logger);

                        Console.WriteLine($"\nPress any key to continue...");
                        Console.ReadKey();

                        await Run(cancellationToken);
                        Console.ReadLine();
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

        private async Task Run(CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await _fileSorter.SortAsync(cancellationToken);

            stopWatch.Stop();
            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
            Console.WriteLine($"Generation finished");
            Console.ReadLine();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

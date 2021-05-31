using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;

namespace BigFilesGenerator.Startup
{
    internal static class HostBuilderFactory
    {
        internal static IHostBuilder CreateHostBuilder(string[] args)
        {
            var configuration = BuildConfiguration();
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration.Build())
                .CreateLogger();

            Log.Information("Creating host builder");

            try
            {
                return Host.CreateDefaultBuilder(args)
                    .ConfigureServices((hostingContext, services) =>
                    {
                        services.AddTransient<IStartupService, StartupService>();
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.AddSerilog();
                    });
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host builder error");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static IConfigurationBuilder BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        }
    }
}

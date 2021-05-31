using BigFilesGenerator.Startup;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;

namespace BigFilesGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = HostBuilderFactory.CreateHostBuilder(args).Build();

            try
            {
                var svc = ActivatorUtilities.CreateInstance<StartupService>(host.Services);
                svc.Run();
            }
            catch(Exception ex)
            {
                Log.Fatal(ex, "Application error");
                throw;
            }
        }
    }
}

using BigFilesGenerator.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Threading.Tasks;

namespace BigFilesGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = HostBuilderFactory.CreateHostBuilder(args).RunConsoleAsync();
            //try
            //{
            //    var svc = ActivatorUtilities.CreateInstance<StartupService>(host.Services);
            //    await svc.Run();
            //}
            //catch(Exception ex)
            //{
            //    Log.Fatal(ex, "Application error");
            //    throw;
            //}
        }
    }
}

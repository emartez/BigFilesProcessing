using BigFilesGenerator.Startup;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace BigFilesGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await HostBuilderFactory.CreateHostBuilder(args).RunConsoleAsync();
        }
    }
}

using BigFilesSorter.Startup;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace BigFilesSorting
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await HostBuilderFactory.CreateHostBuilder(args).RunConsoleAsync();
        }
    }
}

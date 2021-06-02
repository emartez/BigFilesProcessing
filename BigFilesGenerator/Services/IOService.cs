using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    internal class IOService
    {
        public static Task RecreateDirectory(string directory, ILogger logger)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);

            Directory.CreateDirectory(directory);

            logger.LogWarning($"Destination directory '{directory}' recreated");
            return Task.CompletedTask;
        }
    }
}

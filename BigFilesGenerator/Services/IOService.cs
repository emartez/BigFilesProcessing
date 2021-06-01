using System.IO;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    internal class IOService
    {
        public static Task RecreateDirectory(string directory)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);

            Directory.CreateDirectory(directory);
            return Task.CompletedTask;
        }
    }
}

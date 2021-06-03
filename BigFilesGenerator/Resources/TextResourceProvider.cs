using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BigFilesGenerator.Resources
{
    public class TextResourceProvider : ITextResourceProvider
    {
        public async Task<string> ReadResource(string name)
        {
            // Determine path
            var assembly = Assembly.GetExecutingAssembly();
            string resourcePath = name;

            // Format: "{Namespace}.{Folder}.{filename}.{Extension}"
            if (!name.StartsWith(nameof(BigFilesGenerator)))
            {
                resourcePath = assembly.GetManifestResourceNames()
                    .Single(str => str.EndsWith(name));
            }

            using Stream stream = assembly.GetManifestResourceStream(resourcePath);
            using StreamReader reader = new(stream);
            return await reader.ReadToEndAsync();
        }
        public async Task<string[]> ReadResourceLines(string name)
        {
            var resourceText = await ReadResource(name);
            return resourceText.Split("\r\n");
        }
    }
}

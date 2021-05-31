using System.IO;
using System.Linq;
using System.Reflection;

namespace BigFilesGenerator.Resources
{
    public class TextResourceProvider : ITextResourceProvider
    {
        public string ReadResource(string name)
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

            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        public string[] ReadResourceLines(string name)
        {
            var resourceText = ReadResource(name);
            return resourceText.Split("\r\n");
        }
    }
}

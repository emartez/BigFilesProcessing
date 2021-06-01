using System.Threading.Tasks;

namespace BigFilesGenerator.Resources
{
    public interface ITextResourceProvider
    {
        Task<string> ReadResource(string name);
        Task<string[]> ReadResourceLines(string name);
    }
}
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public interface IFileContentGenerator
    {
        Task Generate(byte maxFileSizeInGb);
    }
}
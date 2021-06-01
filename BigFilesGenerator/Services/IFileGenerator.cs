using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public interface IFileGenerator
    {
        Task Generate(string destinationFile, byte maxFileSizeInGb);
    }
}
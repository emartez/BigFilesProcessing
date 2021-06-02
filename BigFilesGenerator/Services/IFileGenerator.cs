using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public interface IFileGenerator
    {
        Task Generate(byte maxFileSizeInGb, CancellationToken cancellationToken);
        Task GenerateChunks(byte maxFileSizeInGb, CancellationToken cancellationToken);
        Task Merge(CancellationToken cancellationToken);
    }
}
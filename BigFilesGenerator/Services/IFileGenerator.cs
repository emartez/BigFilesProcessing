using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public interface IFileGenerator
    {
        Task GenerateAsync(byte maxFileSizeInGb, CancellationToken cancellationToken);
        Task SimpleGenerateAsync(byte maxFileSizeInGb, CancellationToken cancellationToken);
    }
}
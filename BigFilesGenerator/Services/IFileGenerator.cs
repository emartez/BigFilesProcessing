using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public interface IFileGenerator
    {
        Task Generate(byte maxFileSizeInGb, CancellationToken cancellationToken);
    }
}
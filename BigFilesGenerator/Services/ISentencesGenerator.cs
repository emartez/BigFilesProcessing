using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public interface ISentencesGenerator
    {
        Task<StringBuilder> GenerateData(int sencencesNumber, CancellationToken cancellationToken);
    }
}
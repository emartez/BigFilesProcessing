using System.Threading;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public interface IFileSorter
    {
        Task SortAsync(CancellationToken cancellationToken);
    }
}
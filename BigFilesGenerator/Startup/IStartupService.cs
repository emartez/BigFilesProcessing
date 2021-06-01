using System.Threading.Tasks;

namespace BigFilesGenerator.Startup
{
    public interface IStartupService
    {
        Task Run();
    }
}
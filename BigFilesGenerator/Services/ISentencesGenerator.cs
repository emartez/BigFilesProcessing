using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public interface ISentencesGenerator
    {
        Task<string> GenerateData(int sencencesNumber = 1000);
    }
}
namespace BigFilesGenerator.Services
{
    public interface ISentencesGenerator
    {
        string[] GenerateData(int sencencesNumber = 1000);
    }
}
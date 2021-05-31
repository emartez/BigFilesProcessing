namespace BigFilesGenerator.Services
{
    public interface IFileGenerator
    {
        void Generate(string destinationFile, byte maxFileSizeInGb);
    }
}
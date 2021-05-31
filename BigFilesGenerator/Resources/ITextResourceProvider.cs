namespace BigFilesGenerator.Resources
{
    public interface ITextResourceProvider
    {
        string ReadResource(string name);
        string[] ReadResourceLines(string name);
    }
}
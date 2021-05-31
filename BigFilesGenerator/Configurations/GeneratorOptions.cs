namespace BigFilesGenerator.Configurations
{
    public class GeneratorOptions
    {
        public const string Generator = "Generator";

        public string DestinationDirectory { get; set; }
        public string DestinationFileName { get; set; }
        public byte MaxFileSizeInGb { get; set; }
    }
}

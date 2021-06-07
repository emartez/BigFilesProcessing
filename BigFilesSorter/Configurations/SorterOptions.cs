namespace BigFilesSorter.Configurations
{
    public class SorterOptions
    {
        public const string Generator = "Sorter";

        public string DestinationDirectory { get; set; }
        public string DestinationFileName { get; set; }
        public string SourceDirectory { get; set; }
        public string SourceFileName { get; set; }
        public byte WriterSemaphorAccess { get; set; }
        public int ApproximateLineLength { get; set; }
        public int ApproximateChunkFileSizeMb { get; set; }
        public int MaxMemoryUsageMb { get; set; }
    }
}

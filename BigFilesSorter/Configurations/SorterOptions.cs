namespace BigFilesSorter.Configurations
{
    public class SorterOptions
    {
        public const string Generator = "Sorter";

        public string DestinationDirectory { get; set; }
        public string SourceDirectory { get; set; }
        public string ResultFileName { get; set; }
    }
}

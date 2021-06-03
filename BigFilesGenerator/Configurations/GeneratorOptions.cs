namespace BigFilesGenerator.Configurations
{
    public class GeneratorOptions
    {
        public const string Generator = "Generator";

        public string DestinationDirectory { get; set; }
        public string ResultDirectory { get; set; }
        public string ResultFileName { get; set; }
        public byte MaxFileSizeInGb { get; set; }
        public byte SentenceDuplicationOccurrance { get; set; }
        public byte AllowedQueuedLength { get; set; }
        public int SchedullerIterationLimit { get; set; }
        public byte ParralelTaskSchedulingLimit { get; set; }
        public int SentencesPerBatch { get; set; }
        public int FilesMergedAtOnce { get; set; }
        public int WriterGenerationLoopLimit { get; set; }
        public bool GenerateChunksThenMerge { get; set; }
    }
}

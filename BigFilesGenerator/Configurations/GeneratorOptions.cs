﻿namespace BigFilesGenerator.Configurations
{
    public class GeneratorOptions
    {
        public const string Generator = "Generator";

        public string DestinationDirectory { get; set; }
        public string DestinationFileName { get; set; }
        public byte MaxFileSizeInGb { get; set; }
        public byte MaxWordsInSentence { get; set; }
        public byte SentenceDuplicationOccurrance { get; set; }
        public byte AllowedQueuedLength { get; set; }
        public int SchedullerIterationLimit { get; set; }
        public byte ParralelTaskSchedulingLimit { get; set; }
    }
}

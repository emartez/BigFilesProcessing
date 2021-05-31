using System;
using System.Diagnostics;
using System.IO;

namespace BigFilesGenerator.Services
{
    public class FileGenerator : IFileGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;

        public FileGenerator(ISentencesGenerator sentencesGenerator)
        {
            _sentencesGenerator = sentencesGenerator;
        }

        public void Generate(string destinationFile, byte maxFileSizeInGb)
        {
            string rootDirectoryPath = new DirectoryInfo(destinationFile).Parent.FullName;
            Console.WriteLine($"Root data path is {rootDirectoryPath}");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var loops = 100; //~83MB
            var sentences = _sentencesGenerator.GenerateData(50000);
            using var sw = new StreamWriter(destinationFile);

            for (int i = 1; i < loops; i++)
            for (int line = 0; line < sentences.Length; line++)
            {
                sw.WriteLine(sentences[line]);
            }


            stopWatch.Stop();
            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
        }
    }
}

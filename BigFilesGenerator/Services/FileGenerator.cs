using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class FileGenerator : IFileGenerator
    {
        private readonly ISentencesGenerator _sentencesGenerator;

        public FileGenerator(ISentencesGenerator sentencesGenerator)
        {
            _sentencesGenerator = sentencesGenerator;
        }

        public async Task Generate(string destinationFile, byte maxFileSizeInGb)
        {
            string destinationDirectory = new DirectoryInfo(destinationFile).Parent.FullName;
            Console.WriteLine($"Root data path is {destinationDirectory}");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            List<Task> tasks = new();

            for (int i = 0; i < 3; i++)
            {
                var chunkName = $"data_{i}.txt";
                tasks.Add(Task.Run(() => GenerateChunk(destinationDirectory, chunkName)));

                //await GenerateChunk(destinationDirectory, $"data.txt");
            }

            await Task.WhenAll(tasks);
            stopWatch.Stop();
            Console.WriteLine($"Elapsed time: {stopWatch.Elapsed.TotalSeconds}");
        }

        private async Task GenerateChunk(string destinationDirectory, string destinationFile)
        {
            var sentences = await _sentencesGenerator.GenerateData(5000000);
            //var size = sentences.
            //var loops = 100; //~50MB

            var destinationFilePath = Path.Combine(destinationDirectory, destinationFile);
            
            Console.WriteLine($"Scheduled generating file: {destinationFilePath}");

            //await File.AppendAllLinesAsync(destinationFilePath, sentences);

            //using var stram = new TextWriter()
            using var sw = new StreamWriter(destinationFilePath);
            await sw.WriteAsync(sentences);

            ////for (int i = 1; i < loops; i++)
            ////    for (int line = 0; line < sentences.Length; line++)
            ////    {
            ////        await sw.WriteLineAsync(sentences[line]);
            ////    }

            return;
        }
    }
}

using BigFilesGenerator.Configurations;
using BigFilesGenerator.Resources;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class SentencesGenerator : ISentencesGenerator
    {
        private const string WORDS_LIBRARY = "Words.txt";

        private readonly ITextResourceProvider _textResourceProvider;
        private readonly GeneratorOptions _generateOptions;

        public SentencesGenerator(ITextResourceProvider textResourceProvider, IOptions<GeneratorOptions> generateOptions)
        {
            _textResourceProvider = textResourceProvider;
            _generateOptions = generateOptions.Value;
        }

        public async Task<string> GenerateData(int noOfSentences)
        {
            var sentenceWordsTable = await GetWordsSentenceTable(noOfSentences);
            var randoms = GetRandomNumbers(noOfSentences);

            var builder = new StringBuilder();
            for (int i = 0, sentences = 0; sentences < noOfSentences; i++)
            {                
                for (int j = 0; j < _generateOptions.SentenceDuplicationOccurrance && sentences < noOfSentences; j++)
                {        
                    builder.Append(randoms[sentences]).Append('.');

                    for (int k = 0; k < _generateOptions.MaxWordsInSentence; k++)
                        builder.Append(' ').Append(sentenceWordsTable[k][i]);

                    builder.Append("\r\n");
                    sentences += 1;
                }
            }

            return builder.ToString();
        }

        private async Task<string[][]> GetWordsSentenceTable(int noOfSentences)
        {
            var words = await _textResourceProvider.ReadResourceLines(WORDS_LIBRARY);
            var merges = noOfSentences / words.Length / _generateOptions.SentenceDuplicationOccurrance;

            var sentenceWords = Enumerable.Repeat(words, merges + 1).SelectMany(c => c).ToArray();
            string[][] sentenceWordsTable = new string[_generateOptions.MaxWordsInSentence][];
            sentenceWordsTable[1] = sentenceWords;

            for (byte i = 0; i < _generateOptions.MaxWordsInSentence; i++)
            {
                Randomize(sentenceWords, ref sentenceWordsTable[i]);
            }

            return sentenceWordsTable;
        }

        public static void Randomize<T>(T[] items, ref T[] destinationItems)
        {
            Random rand = new Random();
            destinationItems = new T[items.Length];

            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (int i = 0; i < items.Length - 1; i++)
            {
                int j = rand.Next(i, items.Length);
                destinationItems[i] = items[j];
            }
        }

        public static int[] GetRandomNumbers(int noOfItems)
        {
            Random rand = new Random();
            var result = new int[noOfItems];
            for (int i = 0; i < noOfItems - 1; i++)
            {
                result[i] = rand.Next(1,noOfItems);
            }

            return result;
        }
    }
}

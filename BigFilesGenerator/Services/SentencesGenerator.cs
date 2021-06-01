using BigFilesGenerator.Configurations;
using BigFilesGenerator.Resources;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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

        //public async Task<string[]> NewGenerateData(int noOfSentences = 100000)
        //{
        //    var random = new Random();
        //    var words = await _textResourceProvider.ReadResourceLines(WORDS_LIBRARY);
        //    words = words.Select(word => string.Concat(" ", word)).ToArray();

        //    var merges = noOfSentences / words.Length;
        //    var sentenceWords = Enumerable.Repeat(words, merges + 1).SelectMany(c => c).ToArray();
        //    Randomize(sentenceWords);
        //    var generatedLines = new string[noOfSentences];

        //    var sentenceWordsIndex = sentenceWords.Length;
        //    for (int i = 0; i < generatedLines.Length - 1; i++)
        //    {
        //        if (sentenceWordsIndex >= sentenceWords.Length - 1)
        //        {
        //            sentenceWordsIndex = 0;
        //        }

        //        var currentWords = sentenceWords.Skip(sentenceWordsIndex).Take(random.Next(1, _generateOptions.MaxWordsInSentence)).ToArray();
        //        sentenceWordsIndex += currentWords.Length;

        //        generatedLines[i] = string.Concat(i, string.Join("",currentWords));
        //    }

        //    return generatedLines;
        //}

        public async Task<string> GenerateData(int noOfSentences = 100000)
        {
            var words = await _textResourceProvider.ReadResourceLines(WORDS_LIBRARY);
            words = words.Select(word => string.Concat(" ", word)).ToArray();

            var merges = noOfSentences / words.Length / _generateOptions.SentenceDuplicationOccurrance;

            var sentenceWords = Enumerable.Repeat(words, merges + 1).SelectMany(c => c).ToArray();
            string[][] sentenceWordsTable = new string[_generateOptions.MaxWordsInSentence][];
            var randoms = GetRandomNumbers(noOfSentences);

            for (byte i = 0; i < _generateOptions.MaxWordsInSentence; i++)
            {
                Randomize(sentenceWords, ref sentenceWordsTable[i]);
            }

            //var sentences = new string[sentenceWords.Length];

            var builder = new StringBuilder();
            for (int i = 0, sentences = 0; sentences < noOfSentences; i++)
            {                
                for (int j = 0; j < _generateOptions.SentenceDuplicationOccurrance && sentences < noOfSentences; j++)
                {        
                    builder.Append(randoms[sentences]).Append('.');

                    for (int k = 0; k < _generateOptions.MaxWordsInSentence; k++)
                        builder.Append(sentenceWordsTable[k][i]);

                    builder.Append("\r\n");
                    sentences += 1;
                }
            }

            return builder.ToString();
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
                //T temp = items[i];
                destinationItems[i] = items[j];
                //items[j] = temp;
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

        //private string[] DuplicateResults(string[] sentences)
        //{
        //    for (int i = 0; i < TEXT_DUPLICATION_OCCURANCE; i++)
        //    {
        //        sentences.Concat(sentences);
        //    }

        //    return sentences;
        //}

        //public async Task<string[]> Old2_GenerateData(int noOfSentences = 1000)
        //{
        //    var rand = new Random();
        //    var sentences = new List<string>();
        //    //string[,] sentences = new string[noOfSencences, noOfSencences];
        //    var words = await _textResourceProvider.ReadResourceLines(WORDS_LIBRARY);

        //    var merges = noOfSentences / words.Length / 2;

        //    var firstWords = Enumerable.Repeat(words, merges).SelectMany(c => c).ToArray();
        //    var secondWords = Enumerable.Repeat(words, merges).Skip(noOfSentences / 3*2).SelectMany(c => c).ToArray();
        //    var thirdWords = Enumerable.Repeat(words, merges/3*1).Skip(noOfSentences / 3*1).SelectMany(c => c).ToArray();

        //    for (int i = 0; i < firstWords.Length - 1; i++)
        //    {
        //        var secondWord = i < secondWords.Length - 1 ? secondWords[i] : "";
        //        var thirdWord = i < thirdWords.Length - 1 ? thirdWords[i] : "";
        //        sentences.Add($" {firstWords[i]}{secondWord}{thirdWord}");
        //    }

        //    return sentences.Concat(sentences).ToArray();
        //}

        //public async Task<string[]> Old_GenerateData(int noOfSencences = 1000)
        //{
        //    var rand = new Random();
        //    var sentences = new List<string>();
        //    var words = await _textResourceProvider.ReadResourceLines(WORDS_LIBRARY);

        //    for (int i = 0; i < noOfSencences; i++)
        //    {
        //        var wordsInSentence = rand.Next(1, _generateOptions.Value.MaxWordsInSentence);
        //        var sentence = new StringBuilder();

        //        for (int k = 0; k < wordsInSentence; k++)
        //        {
        //            sentence.Append(' ').Append(words[rand.Next(0, words.Length)]);
        //        }

        //        sentences.Add(sentence.ToString());
        //    }

        //    return sentences.ToArray();
        //}
    }
}

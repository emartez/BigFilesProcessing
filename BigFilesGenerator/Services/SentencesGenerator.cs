using BigFilesGenerator.Resources;
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
        private const byte MAX_WORDS_IN_SENTENCE = 4;
        private Random rand = new Random();

        private readonly ITextResourceProvider _textResourceProvider;

        public SentencesGenerator(ITextResourceProvider textResourceProvider)
        {
            _textResourceProvider = textResourceProvider;
        }

        public string[] GenerateData(int noOfSencences = 1000)
        {
            var sentences = new List<string>();
            var words = _textResourceProvider.ReadResourceLines(WORDS_LIBRARY);

            for (int i = 0; i < noOfSencences; i++)
            {
                var wordsInSentence = rand.Next(1, MAX_WORDS_IN_SENTENCE);
                var sentence = new StringBuilder();

                for (int k = 0; k < wordsInSentence; k++) {
                    sentence.Append(' ').Append(words[rand.Next(0, words.Length)]);
                }

                sentences.Add(sentence.ToString());
            }

            return sentences.ToArray();
        }
    }
}

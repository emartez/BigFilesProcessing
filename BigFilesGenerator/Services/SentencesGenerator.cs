using BigFilesGenerator.Configurations;
using BigFilesGenerator.Resources;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BigFilesGenerator.Services
{
    public class SentencesGenerator : ISentencesGenerator
    {
        private const string SENTENCES_LIBRARY = "Sentences.txt";
        private readonly ITextResourceProvider _textResourceProvider;
        private readonly GeneratorOptions _options;
        private static SemaphoreSlim _sentencesLock = new SemaphoreSlim(1,1);
        private static string[] _sentences = null;

        public SentencesGenerator(
            ITextResourceProvider textResourceProvider, 
            IOptions<GeneratorOptions> options)
        {
            _textResourceProvider = textResourceProvider ?? throw new ArgumentNullException(nameof(textResourceProvider));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<StringBuilder> GenerateData(int noOfSentences, CancellationToken cancellationToken)
        {
            await GetSentences(noOfSentences, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return null;

            var randoms = GetRandomNumbers(noOfSentences);

            var builder = new StringBuilder();
            for (int i = 0, sentences = 0; sentences < noOfSentences - 1; i++)
            {
                var number = randoms[sentences];
                for (int j = 0; j < _options.SentenceDuplicationOccurrance && sentences < noOfSentences; j++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return null;

                    builder.Append(randoms[sentences]).Append('.').Append(_sentences[i]).Append(number);
                    builder.Append("\r\n");
                    sentences++;
                }
            }

            return builder;
        }

        private async Task GetSentences(int noOfSentences, CancellationToken cancellationToken)
        {
            if (_sentences == null)
            {
                await _sentencesLock.WaitAsync(cancellationToken);
                try
                {
                    if (_sentences == null)
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        var words = await _textResourceProvider.ReadResourceLines(SENTENCES_LIBRARY);
                        var merges = noOfSentences / words.Length / _options.SentenceDuplicationOccurrance;

                        _sentences = Enumerable.Repeat(words, merges + 1).SelectMany(c => c).ToArray();
                    }
                } 
                finally
                {
                    _sentencesLock.Release();
                }
            }
        }

        public static int[] GetRandomNumbers(int noOfItems)
        {
            Random rand = new Random();
            var result = new int[noOfItems];
            for (int i = 0; i < noOfItems - 1; i++)
            {
                result[i] = rand.Next();
            }

            return result;
        }
    }
}

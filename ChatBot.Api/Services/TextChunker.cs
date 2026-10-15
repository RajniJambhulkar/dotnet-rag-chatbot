using ChatBot.Api.Models;

namespace ChatBot.Api.Services;

public sealed class TextChunker(int chunkSizeTokens = 500, int overlapTokens = 50)
{
    public IReadOnlyList<DocumentChunk> Chunk(string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSizeTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(overlapTokens);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlapTokens, chunkSizeTokens);

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<DocumentChunk>();
        var start = 0;

        while (start < words.Length)
        {
            var end = start;
            var tokenCount = 0;

            while (end < words.Length)
            {
                var wordTokens = EstimateTokens(words[end]);
                if (end > start && tokenCount + wordTokens > chunkSizeTokens)
                {
                    break;
                }

                tokenCount += wordTokens;
                end++;
            }

            chunks.Add(new DocumentChunk(string.Join(' ', words[start..end]), chunks.Count));

            if (end == words.Length)
            {
                break;
            }

            var nextStart = end;
            var overlapCount = 0;
            while (nextStart > start + 1)
            {
                var wordTokens = EstimateTokens(words[nextStart - 1]);
                if (overlapCount + wordTokens > overlapTokens)
                {
                    break;
                }

                overlapCount += wordTokens;
                nextStart--;
            }

            start = nextStart;
        }

        return chunks;
    }

    private static int EstimateTokens(string word) => Math.Max(1, (word.Length + 3) / 4);
}

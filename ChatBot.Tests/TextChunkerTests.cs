using ChatBot.Api.Services;

namespace ChatBot.Tests;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunk_RespectsApproximateTokenBudgetAndOverlap()
    {
        var chunker = new TextChunker(chunkSizeTokens: 4, overlapTokens: 1);

        var chunks = chunker.Chunk("a b c d e f");

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal("a b c d", chunks[0].Text);
        Assert.Equal(1, chunks[1].Index);
        Assert.Equal("d e f", chunks[1].Text);
    }

    [Fact]
    public void Chunk_ReturnsNoChunksForEmptyText()
    {
        var chunker = new TextChunker();

        Assert.Empty(chunker.Chunk(" \r\n "));
    }
}

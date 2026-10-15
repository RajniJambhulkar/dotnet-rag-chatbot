using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChatBot.Api.Options;

namespace ChatBot.Api.Services;

public sealed class IngestionService(
    DocumentTextExtractor extractor,
    TextChunker chunker,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    DocumentStore documentStore,
    IOptions<QdrantOptions> qdrantOptions,
    ILogger<IngestionService> logger)
{
    public async Task<int> IngestAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var text = await extractor.ExtractAsync(fileName, content, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("The uploaded document contains no extractable text.");
        }

        var chunks = chunker.Chunk(text);
        var embeddings = await embeddingGenerator.GenerateAsync(
            chunks.Select(chunk => chunk.Text),
            cancellationToken: cancellationToken);
        var vectors = embeddings.Select(embedding => embedding.Vector).ToArray();

        await documentStore.StoreAsync(fileName, chunks, vectors, cancellationToken);
        logger.LogInformation(
            "Ingested {ChunkCount} chunks from {FileName} into {CollectionName}",
            chunks.Count,
            fileName,
            qdrantOptions.Value.CollectionName);

        return chunks.Count;
    }
}

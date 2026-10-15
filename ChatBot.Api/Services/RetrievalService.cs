using ChatBot.Api.Models;
using ChatBot.Api.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ChatBot.Api.Services;

public interface IRetrievalService
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string query,
        int? topK = null,
        CancellationToken cancellationToken = default);
}

public sealed class RetrievalService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    DocumentStore documentStore,
    IOptions<RetrievalOptions> options) : IRetrievalService
{
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string query,
        int? topK = null,
        CancellationToken cancellationToken = default)
    {
        var resultCount = topK ?? options.Value.DefaultTopK;
        if (resultCount < 1 || resultCount > options.Value.MaximumTopK)
        {
            throw new ArgumentOutOfRangeException(
                nameof(topK),
                $"TopK must be between 1 and {options.Value.MaximumTopK}.");
        }

        var embedding = await embeddingGenerator.GenerateAsync(
            query,
            cancellationToken: cancellationToken);
        return await documentStore.SearchAsync(
            embedding.Vector,
            resultCount,
            cancellationToken);
    }
}

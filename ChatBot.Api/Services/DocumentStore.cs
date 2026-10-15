using ChatBot.Api.Options;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Security.Cryptography;
using System.Text;

namespace ChatBot.Api.Services;

public sealed class DocumentStore(
    QdrantClient client,
    IOptions<QdrantOptions> options)
{
    private readonly SemaphoreSlim _collectionGate = new(1, 1);
    private bool _collectionReady;

    public async Task StoreAsync(
        string fileName,
        IReadOnlyList<Models.DocumentChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        CancellationToken cancellationToken)
    {
        if (chunks.Count != vectors.Count)
        {
            throw new ArgumentException("There must be one embedding for each document chunk.");
        }

        await EnsureCollectionAsync(cancellationToken);

        var points = new List<PointStruct>(chunks.Count);
        for (var index = 0; index < chunks.Count; index++)
        {
            if (vectors[index].Length != options.Value.VectorSize)
            {
                throw new InvalidOperationException(
                    $"Embedding model returned {vectors[index].Length} dimensions; " +
                    $"Qdrant is configured for {options.Value.VectorSize}.");
            }

            points.Add(new PointStruct
            {
                Id = CreatePointId(fileName, chunks[index].Index),
                Vectors = vectors[index].ToArray(),
                Payload =
                {
                    ["file_name"] = fileName,
                    ["chunk_index"] = chunks[index].Index,
                    ["text"] = chunks[index].Text
                }
            });
        }

        if (points.Count > 0)
        {
            await client.UpsertAsync(
                options.Value.CollectionName,
                points,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Models.RetrievedChunk>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK,
        CancellationToken cancellationToken)
    {
        if (queryVector.Length != options.Value.VectorSize)
        {
            throw new InvalidOperationException(
                $"Query embedding has {queryVector.Length} dimensions; " +
                $"Qdrant is configured for {options.Value.VectorSize}.");
        }

        await EnsureCollectionAsync(cancellationToken);

        var results = await client.QueryAsync(
            options.Value.CollectionName,
            query: queryVector.ToArray(),
            limit: (ulong)(topK * 4),
            payloadSelector: new[] { "file_name", "chunk_index", "text" },
            cancellationToken: cancellationToken);

        return results
            .Select(point => new Models.RetrievedChunk(
                point.Payload["file_name"].StringValue,
                checked((int)point.Payload["chunk_index"].IntegerValue),
                point.Payload["text"].StringValue,
                point.Score))
            .DistinctBy(chunk => (chunk.FileName, chunk.ChunkIndex))
            .Take(topK)
            .ToArray();
    }

    private static Guid CreatePointId(string fileName, int chunkIndex)
    {
        var stableKey = Encoding.UTF8.GetBytes($"{fileName.ToUpperInvariant()}:{chunkIndex}");
        return new Guid(SHA256.HashData(stableKey)[..16]);
    }

    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_collectionReady)
        {
            return;
        }

        await _collectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_collectionReady)
            {
                return;
            }

            if (!await client.CollectionExistsAsync(options.Value.CollectionName, cancellationToken))
            {
                await client.CreateCollectionAsync(
                    options.Value.CollectionName,
                    new VectorParams
                    {
                        Size = options.Value.VectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);
            }

            _collectionReady = true;
        }
        finally
        {
            _collectionGate.Release();
        }
    }
}

namespace ChatBot.Api.Options;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 6334;

    public string CollectionName { get; set; } = "documents";

    public uint VectorSize { get; set; } = 768;
}

namespace ChatBot.Api.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434/v1";

    public string ChatModel { get; set; } = "llama3.2";

    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    public int EmbeddingDimensions { get; set; } = 768;

    public string? ApiKey { get; set; }
}

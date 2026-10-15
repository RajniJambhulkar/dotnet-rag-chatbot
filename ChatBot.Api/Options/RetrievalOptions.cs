namespace ChatBot.Api.Options;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    public int DefaultTopK { get; set; } = 4;

    public int MaximumTopK { get; set; } = 20;
}

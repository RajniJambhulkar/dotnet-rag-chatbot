namespace ChatBot.Api.Models;

public sealed record ChatStreamUpdate(
    string Type,
    string? Text = null,
    IReadOnlyList<SourceCitation>? Sources = null);

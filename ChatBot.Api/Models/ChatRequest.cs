namespace ChatBot.Api.Models;

public sealed record ChatRequest(string? ConversationId, string? Message, bool UseRag = false);

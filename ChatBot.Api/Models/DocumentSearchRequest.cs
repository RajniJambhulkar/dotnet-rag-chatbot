namespace ChatBot.Api.Models;

public sealed record DocumentSearchRequest(string? Query, int? TopK);

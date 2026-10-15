namespace ChatBot.Api.Models;

public sealed record RetrievedChunk(
    string FileName,
    int ChunkIndex,
    string Text,
    float Score);

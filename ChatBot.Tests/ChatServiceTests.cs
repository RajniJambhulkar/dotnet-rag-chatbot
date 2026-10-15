using System.Runtime.CompilerServices;
using ChatBot.Api.Models;
using ChatBot.Api.Services;
using Microsoft.Extensions.AI;

namespace ChatBot.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task StreamAsync_IncludesEarlierTurnsInTheNextRequest()
    {
        var client = new RecordingChatClient();
        var service = new ChatService(client, new EmptyRetrievalService());

        await foreach (var _ in service.StreamAsync("conversation-1", "first question"))
        {
        }

        await foreach (var _ in service.StreamAsync("conversation-1", "follow-up"))
        {
        }

        var secondRequest = client.Requests[1];
        Assert.Equal(3, secondRequest.Count);
        Assert.Equal(ChatRole.User, secondRequest[0].Role);
        Assert.Equal(ChatRole.Assistant, secondRequest[1].Role);
        Assert.Equal(ChatRole.User, secondRequest[2].Role);
    }

    [Fact]
    public async Task StreamAsync_WithNoRetrievedChunks_ReturnsExactNotFoundAnswer()
    {
        var client = new RecordingChatClient();
        var service = new ChatService(client, new EmptyRetrievalService());
        var updates = new List<ChatStreamUpdate>();

        await foreach (var update in service.StreamAsync(
            "conversation-1",
            "What is in my documents?",
            useRag: true))
        {
            updates.Add(update);
        }

        Assert.Equal(RagPromptBuilder.NotFoundAnswer, updates[0].Text);
        Assert.Equal("sources", updates[1].Type);
        Assert.Empty(updates[1].Sources!);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task StreamAsync_WithRetrievedChunksDeduplicatesSources()
    {
        var client = new RecordingChatClient("The limit is 10.");
        var chunk = new RetrievedChunk("guide.md", 3, "The limit is 10.", 0.9f);
        var retrieval = new StubRetrievalService([chunk, chunk]);
        var service = new ChatService(client, retrieval);
        var updates = new List<ChatStreamUpdate>();

        await foreach (var update in service.StreamAsync(
            "conversation-1",
            "What is the limit?",
            useRag: true))
        {
            updates.Add(update);
        }

        var prompt = client.Requests.Single()[0];
        Assert.Equal(ChatRole.System, prompt.Role);
        Assert.Contains("The limit is 10.", prompt.Text);
        Assert.Equal("sources", updates[^1].Type);
        var citations = updates[^1].Sources;
        Assert.NotNull(citations);
        var citation = Assert.Single(citations);
        Assert.Equal("guide.md", citation.FileName);
        Assert.Equal(3, citation.ChunkIndex);
    }

    [Fact]
    public async Task StreamAsync_WhenGroundedAnswerIsNotFoundOmitsSources()
    {
        var client = new RecordingChatClient(RagPromptBuilder.NotFoundAnswer);
        var chunk = new RetrievedChunk("guide.md", 3, "The limit is 10.", 0.9f);
        var retrieval = new StubRetrievalService([chunk, chunk]);
        var service = new ChatService(client, retrieval);
        var updates = new List<ChatStreamUpdate>();

        await foreach (var update in service.StreamAsync(
            "conversation-1",
            "What is the limit?",
            useRag: true))
        {
            updates.Add(update);
        }

        var prompt = client.Requests.Single()[0];
        Assert.Equal(ChatRole.System, prompt.Role);
        Assert.Contains("The limit is 10.", prompt.Text);
        Assert.Equal("sources", updates[^1].Type);
        var citations = updates[^1].Sources;
        Assert.NotNull(citations);
        Assert.Empty(citations);
    }

    private sealed class RecordingChatClient(string responseText = "reply") : IChatClient
    {
        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, responseText);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyRetrievalService : IRetrievalService
    {
        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            string query,
            int? topK = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RetrievedChunk>>([]);
    }

    private sealed class StubRetrievalService(IReadOnlyList<RetrievedChunk> chunks) : IRetrievalService
    {
        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            string query,
            int? topK = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(chunks);
    }
}

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using ChatBot.Api.Models;
using Microsoft.Extensions.AI;

namespace ChatBot.Api.Services;

public sealed class ChatService(IChatClient chatClient, IRetrievalService retrievalService)
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new();

    public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        string conversationId,
        string message,
        bool useRag = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = _conversations.GetOrAdd(conversationId, _ => new Conversation());
        await conversation.Gate.WaitAsync(cancellationToken);

        try
        {
            IReadOnlyList<RetrievedChunk> sources = [];
            var requestMessages = new List<ChatMessage>();
            if (useRag)
            {
                sources = await retrievalService.SearchAsync(message, cancellationToken: cancellationToken);
                requestMessages.Add(RagPromptBuilder.BuildSystemMessage(sources));
            }

            requestMessages.AddRange(conversation.Messages);
            requestMessages.Add(new ChatMessage(ChatRole.User, message));
            var responseText = new StringBuilder();

            if (useRag && sources.Count == 0)
            {
                responseText.Append(RagPromptBuilder.NotFoundAnswer);
                yield return new ChatStreamUpdate("delta", RagPromptBuilder.NotFoundAnswer);
            }
            else
            {
                await foreach (var update in chatClient.GetStreamingResponseAsync(
                    requestMessages,
                    cancellationToken: cancellationToken))
                {
                    if (string.IsNullOrEmpty(update.Text))
                    {
                        continue;
                    }

                    responseText.Append(update.Text);
                    yield return new ChatStreamUpdate("delta", update.Text);
                }
            }

            conversation.Messages.Add(new ChatMessage(ChatRole.User, message));
            conversation.Messages.Add(new ChatMessage(ChatRole.Assistant, responseText.ToString()));

            if (useRag)
            {
                var answerIsNotFound = string.Equals(
                    responseText.ToString().Trim(),
                    RagPromptBuilder.NotFoundAnswer,
                    StringComparison.OrdinalIgnoreCase);
                yield return new ChatStreamUpdate(
                    "sources",
                    Sources: answerIsNotFound
                        ? []
                        : sources
                            .Select(source => new SourceCitation(
                                source.FileName,
                                source.ChunkIndex,
                                source.Score))
                            .DistinctBy(source => (source.FileName, source.ChunkIndex))
                            .ToArray());
            }
        }
        finally
        {
            conversation.Gate.Release();
        }
    }

    private sealed class Conversation
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public List<ChatMessage> Messages { get; } = [];
    }
}

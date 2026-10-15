using ChatBot.Api.Models;
using ChatBot.Api.Services;
using Microsoft.Extensions.AI;

namespace ChatBot.Tests;

public sealed class RagPromptBuilderTests
{
    [Fact]
    public void BuildSystemMessage_RequiresContextOnlyAnswersAndCitesExcerpt()
    {
        var chunk = new RetrievedChunk("guide.md", 2, "The service retains data for 30 days.", 0.91f);

        var prompt = RagPromptBuilder.BuildSystemMessage([chunk]);

        Assert.Equal(ChatRole.System, prompt.Role);
        Assert.Contains("using only the document excerpts", prompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(RagPromptBuilder.NotFoundAnswer, prompt.Text);
        Assert.Contains("[Source: guide.md, chunk: 2]", prompt.Text);
        Assert.Contains(chunk.Text, prompt.Text);
    }
}

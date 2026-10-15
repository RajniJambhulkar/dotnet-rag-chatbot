using System.Text;
using System.Text.Json;
using ChatBot.Api.Models;
using ChatBot.Api.Services;

namespace ChatBot.Api;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", StreamChatAsync);
        return endpoints;
    }

    private static async Task<IResult> StreamChatAsync(
        ChatRequest request,
        HttpContext httpContext,
        ChatService chatService)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId) ||
            request.ConversationId.Length > 128)
        {
            return Results.BadRequest(new { error = "ConversationId must contain 1 to 128 characters." });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new { error = "Message is required." });
        }

        httpContext.Response.ContentType = request.UseRag
            ? "application/x-ndjson; charset=utf-8"
            : "text/plain; charset=utf-8";
        await foreach (var update in chatService.StreamAsync(
            request.ConversationId,
            request.Message,
            request.UseRag,
            httpContext.RequestAborted))
        {
            var bytes = request.UseRag
                ? JsonSerializer.SerializeToUtf8Bytes(update, JsonSerializerOptions.Web)
                : Encoding.UTF8.GetBytes(update.Text ?? string.Empty);
            await httpContext.Response.Body.WriteAsync(bytes, httpContext.RequestAborted);
            if (request.UseRag)
            {
                await httpContext.Response.Body.WriteAsync("\n"u8.ToArray(), httpContext.RequestAborted);
            }

            await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
        }

        return Results.Empty;
    }
}

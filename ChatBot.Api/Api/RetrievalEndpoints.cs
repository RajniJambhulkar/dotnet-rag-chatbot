using ChatBot.Api.Models;
using ChatBot.Api.Options;
using ChatBot.Api.Services;
using Microsoft.Extensions.Options;

namespace ChatBot.Api;

public static class RetrievalEndpoints
{
    public static IEndpointRouteBuilder MapRetrievalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/documents/search", SearchAsync);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        DocumentSearchRequest request,
        IRetrievalService retrievalService,
        IOptions<RetrievalOptions> options,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new { error = "Query is required." });
        }

        var topK = request.TopK ?? options.Value.DefaultTopK;
        if (topK < 1 || topK > options.Value.MaximumTopK)
        {
            return Results.BadRequest(new
            {
                error = $"TopK must be between 1 and {options.Value.MaximumTopK}."
            });
        }

        var results = await retrievalService.SearchAsync(
            request.Query,
            topK,
            httpContext.RequestAborted);

        return Results.Ok(new { query = request.Query, results });
    }
}

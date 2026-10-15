using ChatBot.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatBot.Api;

public static class DocumentEndpoints
{
    private const long MaximumFileSize = 25 * 1024 * 1024;
    private const long MaximumRequestSize = MaximumFileSize + 1024 * 1024;

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/documents", UploadAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestSize))
            .DisableAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile? file,
        IngestionService ingestionService,
        HttpContext httpContext)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "A non-empty file is required." });
        }

        if (file.Length > MaximumFileSize)
        {
            return Results.BadRequest(new { error = "Files must be 25 MB or smaller." });
        }

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Only PDF, TXT, and MD files are supported." });
        }

        await using var content = file.OpenReadStream();
        var chunks = await ingestionService.IngestAsync(
            fileName,
            content,
            httpContext.RequestAborted);

        return Results.Ok(new { fileName, chunks });
    }
}

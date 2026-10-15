using ChatBot.Api;
using ChatBot.Api.Options;
using ChatBot.Api.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using Qdrant.Client;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps),
        "Ollama:Endpoint must be an absolute HTTP or HTTPS URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ChatModel), "Ollama:ChatModel is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.EmbeddingModel), "Ollama:EmbeddingModel is required.")
    .Validate(options => options.EmbeddingDimensions > 0, "Ollama:EmbeddingDimensions must be positive.")
    .ValidateOnStart();

builder.Services
    .AddOptions<QdrantOptions>()
    .Bind(builder.Configuration.GetSection(QdrantOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Qdrant:Host is required.")
    .Validate(options => options.Port is > 0 and <= 65535, "Qdrant:Port must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CollectionName), "Qdrant:CollectionName is required.")
    .Validate(options => options.VectorSize > 0, "Qdrant:VectorSize must be positive.")
    .ValidateOnStart();

builder.Services
    .AddOptions<RetrievalOptions>()
    .Bind(builder.Configuration.GetSection(RetrievalOptions.SectionName))
    .Validate(options => options.DefaultTopK > 0, "Retrieval:DefaultTopK must be positive.")
    .Validate(
        options => options.MaximumTopK >= options.DefaultTopK,
        "Retrieval:MaximumTopK must be at least DefaultTopK.")
    .ValidateOnStart();

builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
    var credential = new ApiKeyCredential(
        string.IsNullOrWhiteSpace(options.ApiKey) ? "ollama" : options.ApiKey);
    var clientOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(options.Endpoint)
    };

    return new OpenAIClient(credential, clientOptions)
        .GetChatClient(options.ChatModel)
        .AsIChatClient();
});
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
    var credential = new ApiKeyCredential(
        string.IsNullOrWhiteSpace(options.ApiKey) ? "ollama" : options.ApiKey);
    var clientOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(options.Endpoint)
    };

    return new OpenAIClient(credential, clientOptions)
        .GetEmbeddingClient(options.EmbeddingModel)
        .AsIEmbeddingGenerator(options.EmbeddingDimensions);
});
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
    return new QdrantClient(options.Host, options.Port);
});
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<DocumentTextExtractor>();
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<IngestionService>();
builder.Services.AddSingleton<IRetrievalService, RetrievalService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapChatEndpoints();
app.MapDocumentEndpoints();
app.MapRetrievalEndpoints();

app.Run();

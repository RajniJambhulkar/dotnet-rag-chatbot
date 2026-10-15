# .NET RAG Chatbot

.NET RAG Chatbot is a small document-aware chatbot built with ASP.NET Core
Minimal APIs, Ollama, and Qdrant. It provides a browser-based chat UI, streams
answers from a local language model, and can ground answers in uploaded PDF,
plain-text, and Markdown files using retrieval-augmented generation (RAG).

The application runs locally by default. Chat and embedding requests are sent
to Ollama, and document chunks and their vectors are stored in Qdrant.

## Contents

- [Features](#features)
- [Architecture and how it works](#architecture-and-how-it-works)
- [Technology stack](#technology-stack)
- [Prerequisites](#prerequisites)
- [Run the application](#run-the-application)
- [Configuration](#configuration)
- [HTTP API](#http-api)
- [Project structure](#project-structure)
- [Tests](#tests)
- [Limitations](#limitations)
- [Possible improvements](#possible-improvements)

## Features

- Browser UI for ordinary chat and document-grounded chat.
- Streaming plain-text responses for ordinary chat.
- Streaming newline-delimited JSON (NDJSON) responses with source citations
  for RAG chat.
- In-memory, multi-turn conversation history keyed by a client-provided
  conversation ID.
- Upload and indexing of PDF, TXT, and Markdown (`.md`) files, up to 25 MB
  each.
- Text extraction, overlapping chunking, embeddings, and vector storage in
  Qdrant.
- Similarity search over indexed document chunks.
- Configurable Ollama endpoint, chat and embedding models, vector dimensions,
  Qdrant collection, and retrieval limits.
- Docker Compose configuration for Qdrant with a persistent data volume.

## Architecture and how it works

```text
Browser (ChatBot.Api/wwwroot/index.html)
    |
    | HTTP: /api/chat, /api/documents, /api/documents/search
    v
ASP.NET Core Minimal API
    |
    +-- ChatService --------------------> Ollama chat model
    |      |
    |      +-- ordinary chat: conversation history + user message
    |      +-- RAG: retrieve chunks -> context-only prompt -> chat model
    |
    +-- IngestionService
    |      +-- DocumentTextExtractor (PDF / TXT / MD)
    |      +-- TextChunker
    |      +-- Ollama embedding model
    |      +-- DocumentStore ----------> Qdrant
    |
    +-- RetrievalService
           +-- Ollama embedding model
           +-- DocumentStore ----------> Qdrant similarity search
```

### Document ingestion

1. The client uploads a PDF, TXT, or MD file to `POST /api/documents`.
2. The API extracts text. PDFs are read with PdfPig; text and Markdown files
   are decoded as UTF-8, with byte-order-mark detection.
3. `TextChunker` splits the extracted text into chunks targeting approximately
   500 estimated tokens, with approximately 50 tokens of overlap.
4. The embedding model creates a vector for each chunk.
5. `DocumentStore` creates the configured Qdrant collection if it does not
   exist, then stores vectors with the filename, chunk index, and text as
   payload.

### Chat and retrieval

For ordinary chat, the API sends the conversation's prior turns and the new
message to the configured chat model. Generated text is streamed to the
browser as `text/plain`.

For RAG chat, the API embeds the user's question, searches Qdrant for relevant
chunks, and includes those excerpts in a system message that instructs the
model to answer using only that context. The answer is streamed as NDJSON
`delta` events, followed by a `sources` event containing citations. When no
chunks are retrieved, the API returns the exact fallback
`I couldn't find this in the documents` without calling the chat model.

The standalone document search endpoint uses the same embedding and vector
search path, without generating a chat response.

## Technology stack

| Area | Technology |
| --- | --- |
| Runtime and web API | .NET 10, ASP.NET Core Minimal APIs |
| Language | C# with nullable reference types and implicit usings enabled |
| Chat and embeddings | Ollama through its OpenAI-compatible API |
| AI client abstractions | `Microsoft.Extensions.AI.OpenAI` |
| Vector database | Qdrant (`Qdrant.Client`), cosine similarity |
| PDF extraction | PdfPig |
| Browser UI | Static HTML, CSS, and JavaScript served by ASP.NET Core |
| Tests | xUnit and Microsoft.NET.Test.Sdk |
| Local vector database setup | Docker Compose |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Ollama](https://ollama.com/) installed and running locally
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) with
  Docker Compose, to run Qdrant
- The chat and embedding models configured for Ollama

The default models are `llama3.2:3b` for chat and `nomic-embed-text` for
embeddings. Pull them with:

```powershell
ollama pull llama3.2:3b
ollama pull nomic-embed-text
```

The embedding model must return 768-dimensional vectors with the default
configuration. Ensure Ollama is available at `http://localhost:11434` before
using chat or document features.

## Run the application

From the repository root:

1. Start Qdrant:

   ```powershell
   docker compose up -d
   ```

   Compose maps Qdrant's HTTP API to port `6333` and gRPC to port `6334`.
   Its data is stored in the `qdrant_data` Docker volume.

2. Start the API using its HTTP launch profile:

   ```powershell
   dotnet run --project .\ChatBot.Api\ChatBot.Api.csproj --launch-profile http
   ```

   The default HTTP address is `http://localhost:5209`. Open that URL to use
   the browser UI.

3. Upload a document from the **Your documents** panel. When indexing
   completes, enable **Use my documents (RAG)** to ask questions about indexed
   content.

The browser UI keeps its conversation ID for follow-up messages in that tab.
The conversation itself is held in API process memory and is lost when the API
restarts.

## Configuration

Defaults are in `ChatBot.Api/appsettings.json`. Environment variables can
override .NET configuration values by replacing `:` with `__`.

| Setting | Default | Description |
| --- | --- | --- |
| `Ollama:Endpoint` | `http://localhost:11434/v1` | Ollama OpenAI-compatible API base URL |
| `Ollama:ChatModel` | `llama3.2:3b` | Model used to generate chat responses |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Model used for document and query embeddings |
| `Ollama:EmbeddingDimensions` | `768` | Expected embedding output dimensions |
| `Ollama:ApiKey` | Not set | Optional API key for an OpenAI-compatible endpoint |
| `Qdrant:Host` | `localhost` | Qdrant host name |
| `Qdrant:Port` | `6334` | Qdrant gRPC port |
| `Qdrant:CollectionName` | `documents` | Collection used to store document vectors |
| `Qdrant:VectorSize` | `768` | Collection vector dimensions |
| `Retrieval:DefaultTopK` | `4` | Default number of chunks returned for retrieval |
| `Retrieval:MaximumTopK` | `20` | Maximum allowed search result count |

For example, in PowerShell:

```powershell
$env:Ollama__Endpoint = "http://localhost:11434/v1"
$env:Ollama__ApiKey = "your-local-provider-key"
$env:Ollama__ChatModel = "llama3.2:3b"
dotnet run --project .\ChatBot.Api\ChatBot.Api.csproj --launch-profile http
```

Do not put real credentials in committed settings files. Use environment
variables or .NET user-secrets for sensitive values.

`Ollama:EmbeddingDimensions` and `Qdrant:VectorSize` must agree with the
embedding model's actual output. Qdrant fixes a collection's vector size when
the collection is created. If you change embedding models or dimensions, use a
new collection or back up and recreate the existing collection before
re-indexing documents.

## HTTP API

All endpoints are served by the API at `http://localhost:5209` with the
default launch profile. Request bodies use JSON except for document upload.

### `POST /api/chat`

Request:

```json
{
  "conversationId": "demo",
  "message": "Hello!",
  "useRag": false
}
```

`conversationId` is required and must contain 1–128 characters. Reuse it for
follow-up messages in the same conversation. `message` is required.
`useRag` is optional and defaults to `false`.

- With `useRag: false`, the response is streamed as `text/plain`.
- With `useRag: true`, the response is streamed as
  `application/x-ndjson; charset=utf-8`. Each line is a JSON event, for
  example:

  ```json
  {"type":"delta","text":"The answer"}
  {"type":"sources","sources":[{"fileName":"guide.md","chunkIndex":0,"score":0.82}]}
  ```

  There may be multiple `delta` events. The final `sources` event contains
  citations with filename, chunk index, and similarity score. If no chunks
  are retrieved, the answer is `I couldn't find this in the documents` and
  the citations list is empty.

PowerShell example:

```powershell
$body = @{
  conversationId = "demo"
  message = "Hello!"
  useRag = $false
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5209/api/chat" `
  -Method Post -ContentType "application/json" -Body $body
```

### `POST /api/documents`

Uploads and indexes one document using `multipart/form-data`. The form field
must be named `file`. Accepted extensions are `.pdf`, `.txt`, and `.md`;
maximum file size is 25 MB.

Example with `curl.exe`:

```powershell
curl.exe --request POST "http://localhost:5209/api/documents" `
  --form "file=@C:\path\to\your-document.pdf"
```

Successful response:

```json
{
  "fileName": "your-document.pdf",
  "chunks": 12
}
```

An empty file, unsupported extension, or file over the size limit is rejected
with a `400 Bad Request` response. A document with no extractable text also
cannot be indexed.

### `POST /api/documents/search`

Embeds a query and returns the closest indexed chunks. `topK` is optional; it
defaults to `Retrieval:DefaultTopK` and must be between 1 and
`Retrieval:MaximumTopK`.

Request:

```json
{
  "query": "What is HttpContext?",
  "topK": 4
}
```

PowerShell example:

```powershell
$body = @{
  query = "What is HttpContext?"
  topK = 4
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5209/api/documents/search" `
  -Method Post -ContentType "application/json" -Body $body
```

The response has a `query` and `results` array. Each result contains `fileName`,
`chunkIndex`, `text`, and `score`.

## Project structure

```text
.
├── ChatBot.slnx
├── docker-compose.yml
├── README.md
├── ChatBot.Api/
│   ├── Api/
│   │   ├── ChatEndpoints.cs
│   │   ├── DocumentEndpoints.cs
│   │   └── RetrievalEndpoints.cs
│   ├── Models/
│   │   ├── ChatRequest.cs
│   │   ├── ChatStreamUpdate.cs
│   │   ├── DocumentChunk.cs
│   │   ├── DocumentSearchRequest.cs
│   │   ├── RetrievedChunk.cs
│   │   └── SourceCitation.cs
│   ├── Options/
│   │   ├── OllamaOptions.cs
│   │   ├── QdrantOptions.cs
│   │   └── RetrievalOptions.cs
│   ├── Services/
│   │   ├── ChatService.cs
│   │   ├── DocumentStore.cs
│   │   ├── DocumentTextExtractor.cs
│   │   ├── IngestionService.cs
│   │   ├── RagPromptBuilder.cs
│   │   ├── RetrievalService.cs
│   │   └── TextChunker.cs
│   ├── wwwroot/
│   │   └── index.html
│   ├── appsettings.json
│   ├── ChatBot.Api.csproj
│   └── Program.cs
└── ChatBot.Tests/
    ├── ChatServiceTests.cs
    ├── RagPromptBuilderTests.cs
    ├── TextChunkerTests.cs
    └── ChatBot.Tests.csproj
```

- **Api** defines the HTTP routes, validation, and streaming response formats.
- **Models** contains request, chunk, citation, and streaming event types.
- **Options** binds and validates configuration sections.
- **Services** implements chat history, document extraction and ingestion,
  chunking, retrieval, Qdrant storage, and RAG prompt construction.
- **wwwroot** contains the static browser UI served by ASP.NET Core.
- **ChatBot.Tests** contains unit tests for chat history, RAG prompt and
  fallback behavior, citation deduplication, and chunking.

## Tests

Run the test suite from the repository root:

```powershell
dotnet test .\ChatBot.slnx
```

The unit tests use test doubles for chat and retrieval services and do not
require a running Ollama or Qdrant instance.

## Limitations

- Conversations are stored in process memory, have no persistence or expiry,
  and are not shared between API instances.
- The document collection is shared by all callers; there is no user or
  tenant-level isolation, authentication, or authorization.
- The API supports only PDF, TXT, and Markdown uploads, with a 25 MB limit.
- PDF extraction reads embedded text only. Scanned or image-only PDFs require
  OCR, which is not implemented.
- Chunk size is approximate: token counts are estimated from whitespace-
  separated words and character length rather than the model tokenizer.
- Search and RAG quality depend on the embedding model, chunking, and retrieval
  settings. Retrieved chunks can be incomplete or irrelevant, and generated
  answers are not guaranteed to be correct.
- Re-uploading a file with the same name reuses its chunk IDs. If the new
  version has fewer chunks, old trailing chunks can remain in Qdrant.
- There are no endpoints to list, delete, or replace indexed documents.
- The API is intended for local development, not direct exposure to untrusted
  networks. It has no authentication, rate limiting, or upload malware
  scanning.
- Changing embedding dimensions or model without rebuilding the collection
  and re-indexing can make stored vectors incompatible with new query vectors.

## Possible improvements

- Persist conversations and add retention policies.
- Add document list, delete, and replace operations, including cleanup of stale
  chunks when a file is re-indexed.
- Add user/tenant isolation and authentication/authorization.
- Add integration tests for streaming, ingestion, and retrieval with test
  doubles or disposable Ollama and Qdrant services.
- Add OCR and richer PDF extraction for tables and layout.
- Use model-aware tokenization and evaluate chunk size, overlap, and top-k
  against a representative question set.
- Add observability for request duration, model failures, ingestion progress,
  and retrieval quality.
- Add rate limits, stricter operational upload controls, and malware/content
  scanning before exposing the service beyond localhost.
- Improve the browser UI with conversation management, document management,
  and richer citation display.

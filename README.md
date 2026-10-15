# Local Documents Chat

A small .NET 10 Minimal API chatbot with streamed Ollama chat, document ingestion,
Qdrant vector search, and a browser UI for plain chat or retrieval-augmented
generation (RAG).

## Architecture

```text
Browser (wwwroot/index.html)
  ├── POST /api/chat
  │     ├── Plain chat: IChatClient -> streamed text
  │     └── RAG: question -> IEmbeddingGenerator -> Qdrant top-k chunks
  │                       -> context-only prompt -> streamed NDJSON + sources
  ├── POST /api/documents -> extract -> chunk -> embeddings -> Qdrant
  └── POST /api/documents/search -> query embedding -> Qdrant matches

Ollama: OpenAI-compatible chat and embeddings endpoint
Qdrant: vector collection and chunk metadata
```

The API is organized into `Api`, `Models`, `Options`, and `Services` folders in
`ChatBot.Api`. `ChatBot.Tests` contains the unit tests.

## Requirements

- .NET 10 SDK
- [Ollama](https://ollama.com/) running locally
- Docker Desktop with Docker Compose

## Setup and run

From the repository root:

1. Download the chat and embedding models:

   ```powershell
   ollama pull llama3.2:3b
   ollama pull nomic-embed-text
   ```

2. Start Qdrant:

   ```powershell
   docker compose up -d
   ```

   Qdrant exposes its HTTP API on port `6333` and gRPC on port `6334`. Data
   persists in the `qdrant_data` Docker volume.

3. Start the API and open the UI at the address printed by ASP.NET Core:

   ```powershell
   dotnet run --project ChatBot.Api
   ```

   With the default launch profile, browse to `http://localhost:5209`.

4. Upload a PDF, TXT, or Markdown document using the upload panel. Once
   processing succeeds, turn on **Use my documents (RAG)** to ask about it. Keep
   the browser tab open to retain the in-memory conversation ID.

The same UI can be opened from the API root (`/`). The chat conversation history
is held in API memory and is lost when the process restarts.

## Configuration

Defaults are in `ChatBot.Api/appsettings.json`:

| Setting | Default | Purpose |
| --- | --- | --- |
| `Ollama:Endpoint` | `http://localhost:11434/v1` | Ollama OpenAI-compatible API |
| `Ollama:ChatModel` | `llama3.2:3b` | Chat completion model |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Document and query embeddings |
| `Ollama:EmbeddingDimensions` | `768` | Expected embedding dimensions |
| `Qdrant:Host` | `localhost` | Qdrant host |
| `Qdrant:Port` | `6334` | Qdrant gRPC port |
| `Qdrant:CollectionName` | `documents` | Vector collection |
| `Qdrant:VectorSize` | `768` | Collection vector dimensions |
| `Retrieval:DefaultTopK` | `4` | Default number of retrieved chunks |
| `Retrieval:MaximumTopK` | `20` | Maximum search result count |

The embedding model's output size must match both `Ollama:EmbeddingDimensions`
and `Qdrant:VectorSize`. A collection's vector size is fixed when it is created;
if you change embedding dimensions or model, use a new collection or recreate
the old one after backing up any data you need.

For a custom Ollama endpoint or an endpoint requiring an API key, use
configuration overrides, for example:

```powershell
$env:Ollama__Endpoint = "http://localhost:11434/v1"
$env:Ollama__ApiKey = "your-local-provider-key"
```

Do not put secrets in committed settings files. Use environment variables or
.NET user-secrets for sensitive values.

## API examples

Examples below use Command Prompt (`cmd.exe`) quoting. Change the port if your
API prints a different URL.

### Plain streamed chat

```cmd
curl.exe --no-buffer --request POST "http://localhost:5209/api/chat" --header "Content-Type: application/json" --data "{\"conversationId\":\"demo\",\"message\":\"Hello!\"}"
```

Reuse the same `conversationId` for follow-up messages. Plain chat streams
response text directly.

### Upload a document

```cmd
curl.exe --request POST "http://localhost:5209/api/documents" --form "file=@C:\path\to\your-document.pdf"
```

Uploads accept PDF, TXT, and MD files up to 25 MB. A successful response
contains the filename and number of indexed chunks.

### Search document chunks

```cmd
curl.exe --request POST "http://localhost:5209/api/documents/search" --header "Content-Type: application/json" --data "{\"query\":\"What is HttpContext?\",\"topK\":4}"
```

Search returns matching text, filename, chunk index, and similarity score.

### Ask a grounded question

```cmd
curl.exe --no-buffer --request POST "http://localhost:5209/api/chat" --header "Content-Type: application/json" --data "{\"conversationId\":\"demo\",\"message\":\"What is HttpContext?\",\"useRag\":true}"
```

RAG mode streams newline-delimited JSON (`application/x-ndjson`): `delta`
events contain answer text, followed by a `sources` event with citations. When
no chunks are found, the API returns `I couldn't find this in the documents`.
The prompt also instructs the model to use only retrieved excerpts and return
that exact fallback when they don't contain an answer.

## Chunking and retrieval trade-offs

- The chunker targets about 500 estimated tokens with about 50 tokens of
  overlap. It estimates tokens from whitespace-separated words and character
  length, rather than using the embedding model's tokenizer.
- Retrieval returns 4 chunks by default. More chunks may improve recall but can
  add irrelevant context and increase prompt size.
- Embeddings depend on the selected model. Changing embedding models can change
  vector dimensions and similarity behavior; keep the ingestion and query model
  identical.
- PDF text extraction does not perform OCR. Image-only scanned PDFs need OCR
  before ingestion.

## Tests

Run the unit tests from the repository root:

```powershell
dotnet test ChatBot.slnx
```

The current tests cover approximate chunking and overlap, multi-turn chat
history, RAG prompt construction, the no-results fallback, and citation
deduplication.

## Possible improvements

- Persist conversations instead of keeping them only in process memory.
- Use the embedding model tokenizer for precise chunk sizing.
- Add OCR for scanned PDFs and richer extraction for tables/layout.
- Add document management (list, delete, replace) and tenant/user isolation.
- Add streaming, ingestion, and retrieval integration tests using test doubles
  or disposable local services.
- Add authentication, rate limiting, and upload malware/content scanning before
  exposing the API beyond localhost.
- Tune top-k and chunk sizes against a representative set of questions and
  documents.
- Add a Blazor UI or richer frontend if the single-page HTML UI outgrows its
  needs.

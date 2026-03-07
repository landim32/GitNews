# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
dotnet build                                    # Build the solution
dotnet run --project GitNews.Console            # Run the console app (one-shot)
dotnet run --project GitNews.Worker             # Run the worker (scheduled)
dotnet ef migrations add <Name> --project GitNews.Infra --startup-project GitNews.Console  # New migration
```

## Architecture

.NET 8 solution following Clean Architecture with 7 projects:

```
GitNews.Console  ─┐
GitNews.Worker   ─┤→ GitNews.Application → GitNews.Infra       → GitNews.Domain
                  │                       → GitNews.DTO
                  │                       → GitNews.Infra.Interfaces → GitNews.DTO
```

- **GitNews.DTO** — Data Transfer Objects, settings classes (`GitNewsSettings`, `WorkerSettings`). No dependencies.
- **GitNews.Infra.Interfaces** — Repository and AppService interfaces with generics. Depends on DTO only.
- **GitNews.Domain** — Entity models (`Article`, `ProcessedCommit`). No dependencies.
- **GitNews.Infra** — Implementations: `GitHubAppService` (Octokit), `BlogGeneratorAppService` (OpenAI), `EmbeddingAppService`, `MarkdownWriterAppService`, `ProcessedCommitRepository`, `ArticleRepository`, `GitNewsDbContext` (PostgreSQL/pgvector). Depends on Domain, DTO, Infra.Interfaces.
- **GitNews.Application** — `Startup.cs` with DI registration (`ConfigureServices` extension method), logging setup. Depends on all layers.
- **GitNews.Console** — One-shot CLI entry point. Reads config from `appsettings.json`, environment variables (standard `__` convention, no prefix), and CLI args.
- **GitNews.Worker** — Background worker that runs daily at a configured time (default 19:00). Uses `BackgroundService` with `Host.CreateApplicationBuilder`.

## Data Flow

1. `IGitHubAppService.GetRepositoryNamesAsync` → lists all repos for an owner
2. For each repo: `GetRepositoryContextAsync` → fetches README, commit count, and recent commits with diffs
3. `IProcessedCommitRepository` → filters out already-processed commits (PostgreSQL)
4. `IBlogGeneratorAppService.GenerateBlogPostAsync` → sends prompt to OpenAI, receives JSON with title/content/category/tags
5. `IEmbeddingAppService.GenerateEmbeddingAsync` → generates vector embedding, `IArticleRepository.FindSimilarAsync` checks for duplicates
6. `IMarkdownWriterAppService.WritePostAsync` → saves as `.md` with YAML front matter

## Article Generation Rules (in BlogGeneratorAppService.BuildPrompt)

- AI analyzes commit diffs to identify new packages, patterns, architectures, techniques
- Each technical novelty becomes an article topic with real code snippets as examples
- Projects with ≤3 total commits are "new" → article includes project description
- Projects with >3 commits → article focuses only on technical novelties, not project description
- If no novelties found → returns "Sem novidades" title, which is skipped

## Database

PostgreSQL with EF Core Code First. `GitNewsDbContext` in `GitNews.Infra/Context/`. Design-time factory (`GitNewsDbContextFactory`) reads connection string from `GitNews.Console/appsettings.json`. Migrations auto-applied on startup.

Tables: `processed_commits` (unique index on repository+sha), `articles` (with pgvector embedding column).

## Configuration

`appsettings.json` is gitignored. Use `appsettings.example.json` as template. Settings model: `GitNewsSettings` (GitHub, OpenAI, Output, Database sections). Worker adds `WorkerSettings` (ScheduleTime).

## Logging

All services use `ILogger<T>` via DI. Log messages are in English. Console logging configured via `services.AddLogging(builder => builder.AddConsole())` in Startup.cs. AI-generated article content remains in Portuguese (pt-BR).

## Infrastructure

`docker-compose.yml` provides:
- PostgreSQL 16 with pgvector on port 5432
- GitNews Worker container (scheduled daily processing)

Configuration via `.env` file (see `.env.example`).

# GitNews - AI-Powered Blog Generator from GitHub Repositories

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1)
![OpenAI](https://img.shields.io/badge/OpenAI-GPT--4-412991)
![License](https://img.shields.io/badge/License-MIT-green)

## Overview

**GitNews** is an automated blog article generator that analyzes commits from GitHub repositories, identifies technical novelties (new packages, design patterns, architectural changes), and generates detailed articles using OpenAI's GPT-4. Articles are stored in PostgreSQL with pgvector embeddings to prevent duplicate content. Built with **.NET 8** following **Clean Architecture**.

Available as a one-shot **CLI tool** or a **scheduled background worker** that runs daily.

---

## 🚀 Features

- 🔍 **Automatic Repository Scanning** - Lists and processes all repositories from a GitHub account
- 🤖 **AI-Powered Article Generation** - Uses GPT-4 to analyze commit diffs and generate technical blog posts
- 🧠 **Embedding-Based Deduplication** - Generates vector embeddings to detect and skip similar articles
- 📊 **Smart Commit Filtering** - Tracks processed commits in PostgreSQL to avoid reprocessing
- 🆕 **New Project Detection** - Projects with ≤3 commits get a project description; others focus on technical novelties
- ⏰ **Scheduled Worker** - Background service that runs daily at a configurable time
- 🐳 **Docker Ready** - Full Docker Compose setup with PostgreSQL and the Worker service

---

## 🛠️ Technologies Used

### Core Framework
- **.NET 8** - Target framework for all projects

### Database
- **PostgreSQL 16** - Primary data store with pgvector extension
- **Entity Framework Core 8** - ORM with Code First migrations
- **Pgvector** - Vector similarity search for article deduplication

### External APIs
- **Octokit 13** - GitHub API client for repository and commit data
- **OpenAI API** - GPT-4 for article generation, text-embedding-3-small for embeddings

### DevOps
- **Docker / Docker Compose** - Containerized deployment
- **GitVersion** - Semantic versioning from commit messages
- **GitHub Actions** - Automated tagging and release creation

---

## 📁 Project Structure

```
GitNews/
├── GitNews.DTO/                  # Data Transfer Objects, settings classes
├── GitNews.Infra.Interfaces/     # Repository & AppService interfaces (generics)
│   ├── AppServices/              # IGitHubAppService, IBlogGeneratorAppService, IEmbeddingAppService
│   └── Repository/               # IProcessedCommitRepository, IArticleRepository
├── GitNews.Domain/               # Entity models, service interfaces & implementations
│   ├── Interfaces/               # IGitNewsProcessorService
│   ├── Models/                   # Article, ProcessedCommit
│   └── Services/                 # GitNewsProcessorService (orchestration)
├── GitNews.Infra/                # Infrastructure implementations
│   ├── AppServices/              # GitHubAppService, BlogGeneratorAppService, EmbeddingAppService
│   ├── Context/                  # GitNewsDbContext, design-time factory
│   ├── Migrations/               # EF Core migrations
│   └── Repository/               # ProcessedCommitRepository, ArticleRepository
├── GitNews.Application/          # DI composition root (Startup.cs)
├── GitNews.Console/              # CLI entry point (one-shot execution)
├── GitNews.Worker/               # Background worker (scheduled daily)
├── docker-compose.yml            # PostgreSQL + Worker services
├── .env.example                  # Environment variable template
└── CLAUDE.md                     # AI assistant instructions
```

### Architecture

```
GitNews.Console  ─┐
GitNews.Worker   ─┤→ GitNews.Application → GitNews.Infra       → GitNews.Domain
                  │                       → GitNews.DTO
                  │                       → GitNews.Infra.Interfaces → GitNews.DTO
```

---

## ⚙️ Environment Configuration

### 1. Copy the environment template

```bash
cp .env.example .env
```

### 2. Edit the `.env` file

```bash
POSTGRES_DB=gitnews
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_secure_password_here
POSTGRES_PORT=5432

GITHUB_TOKEN=your_github_token_here
GITHUB_OWNER=your_github_username
GITHUB_MAXCOMMITS=30
GITHUB_INCLUDEFORKS=false

OPENAI_APIKEY=your_openai_api_key_here
OPENAI_MODEL=gpt-4
OPENAI_BASEURL=https://api.openai.com/v1

WORKER_SCHEDULETIME=19:00
```

⚠️ **IMPORTANT**:
- Never commit the `.env` file with real credentials
- You need a **GitHub Personal Access Token** with `repo` scope
- You need an **OpenAI API Key** with access to GPT-4 and embeddings

---

## 🐳 Docker Setup

### Quick Start with Docker Compose

#### 1. Configure environment

```bash
cp .env.example .env
# Edit .env with your credentials
```

#### 2. Build and Start Services

```bash
docker-compose up -d --build
```

This starts:
- **PostgreSQL 16** with pgvector on port 5432
- **GitNews Worker** scheduled to run daily at the configured time

#### 3. Verify Deployment

```bash
docker-compose ps
docker-compose logs -f worker
```

### Docker Compose Commands

| Action | Command |
|--------|---------|
| Start services | `docker-compose up -d` |
| Start with rebuild | `docker-compose up -d --build` |
| Stop services | `docker-compose stop` |
| View status | `docker-compose ps` |
| View worker logs | `docker-compose logs -f worker` |
| Remove containers | `docker-compose down` |
| Remove containers and volumes (⚠️) | `docker-compose down -v` |

---

## 🔧 Manual Setup (Without Docker)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 16](https://www.postgresql.org/download/) with [pgvector](https://github.com/pgvector/pgvector) extension
- GitHub Personal Access Token
- OpenAI API Key

### Setup Steps

#### 1. Start PostgreSQL (via Docker or local install)

```bash
docker-compose up -d postgres
```

#### 2. Configure the application

```bash
cp GitNews.Console/appsettings.example.json GitNews.Console/appsettings.json
# Edit appsettings.json with your credentials
```

#### 3. Build the solution

```bash
dotnet build
```

#### 4. Run the Console app (one-shot)

```bash
dotnet run --project GitNews.Console
```

Or with CLI arguments:

```bash
dotnet run --project GitNews.Console -- \
  --owner your-github-username \
  --github-token ghp_xxx \
  --openai-key sk-xxx \
  --connection-string "Host=localhost;Port=5432;Database=gitnews;Username=postgres;Password=postgres"
```

#### 5. Run the Worker (scheduled)

```bash
cp GitNews.Worker/appsettings.example.json GitNews.Worker/appsettings.json
# Edit appsettings.json
dotnet run --project GitNews.Worker
```

### CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `-o, --owner` | GitHub account owner | — |
| `--github-token` | GitHub access token | — |
| `--openai-key` | OpenAI API key | — |
| `-m, --model` | ChatGPT model | `gpt-4` |
| `--max-commits` | Max commits per repo | `30` |
| `--include-forks` | Include forked repositories | `false` |
| `--connection-string` | PostgreSQL connection string | — |
| `-h, --help` | Show help | — |

### Environment Variables

All settings can be configured via environment variables with the `GITNEWS_` prefix:

| Variable | Description |
|----------|-------------|
| `GITNEWS_GITHUB__TOKEN` | GitHub access token |
| `GITNEWS_GITHUB__OWNER` | Account owner |
| `GITNEWS_OPENAI__APIKEY` | OpenAI API key |
| `GITNEWS_OPENAI__MODEL` | ChatGPT model |
| `GITNEWS_DATABASE__CONNECTIONSTRING` | PostgreSQL connection string |

---

## 📚 Data Flow

```
1. GitHub API        → List all repositories for an owner
2. For each repo     → Fetch README, commit count, recent commits with diffs
3. PostgreSQL        → Filter out already-processed commits
4. OpenAI GPT-4      → Analyze diffs, generate article (title, content, category, tags)
5. OpenAI Embeddings → Generate vector embedding for the article
6. pgvector          → Check similarity against existing articles (skip duplicates)
7. PostgreSQL        → Save article and mark commits as processed
```

---

## 💾 Database

### Tables

- **`articles`** - Generated blog articles with pgvector embedding column (1536 dimensions)
- **`processed_commits`** - Tracks processed commits with unique index on `(repository, sha)`

### Migrations

Migrations are auto-applied on startup. To create a new migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project GitNews.Infra \
  --startup-project GitNews.Console
```

### Backup

```bash
docker-compose exec postgres pg_dump -U postgres gitnews > backup.sql
```

### Restore

```bash
docker-compose exec -T postgres psql -U postgres gitnews < backup.sql
```

---

## 🔄 CI/CD

### GitHub Actions

**Version and Tag** (`version-tag.yml`):
- Triggers on push to `main`
- Uses GitVersion for semantic versioning based on commit messages
- Creates and pushes Git tags automatically

**Create Release** (`create-release.yml`):
- Triggers after successful tagging
- Creates GitHub Releases for major/minor changes
- Generates changelog from commit messages

### Commit Message Conventions

| Prefix | Version Bump |
|--------|-------------|
| `major:` or `breaking:` | Major (x.0.0) |
| `feat:` or `feature:` | Minor (0.x.0) |
| `fix:` or `patch:` | Patch (0.0.x) |

---

## 🔍 Troubleshooting

### Common Issues

#### Database connection failed

**Check:**
```bash
docker-compose ps postgres
docker-compose logs postgres
```

**Common causes:**
- PostgreSQL container not running
- Wrong connection string or port
- pgvector extension not installed

#### GitHub API rate limit

**Common causes:**
- Too many repositories being processed
- Token without proper permissions

**Solutions:**
- Reduce `MaxCommits` setting
- Use a token with `repo` scope
- The app has built-in retry with exponential backoff for rate limits

#### OpenAI API errors

**Common causes:**
- Invalid API key
- Insufficient credits
- Model not available

**Solutions:**
- Verify API key at [platform.openai.com](https://platform.openai.com)
- Check billing and usage limits
- Try a different model (e.g., `gpt-3.5-turbo`)

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Development Setup

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Make your changes
4. Build and verify (`dotnet build`)
5. Commit your changes (`git commit -m 'feat: add some AmazingFeature'`)
6. Push to the branch (`git push origin feature/AmazingFeature`)
7. Open a Pull Request

---

## 👨‍💻 Author

Developed by **[Rodrigo Landim Carneiro](https://github.com/landim32)**

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Built with [.NET 8](https://dotnet.microsoft.com/)
- GitHub API via [Octokit](https://github.com/octokit/octokit.net)
- AI powered by [OpenAI](https://openai.com/)
- Vector search with [pgvector](https://github.com/pgvector/pgvector)

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/landim32/GitNews/issues)

---

**⭐ If you find this project useful, please consider giving it a star!**

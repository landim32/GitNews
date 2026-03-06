using GitNews.Domain.Interfaces;
using GitNews.Domain.Models;
using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using GitNews.Infra.Interfaces.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitNews.Domain.Services;

public class GitNewsProcessorService : IGitNewsProcessorService
{
    private readonly IGitHubAppService _githubService;
    private readonly IBlogGeneratorAppService _blogGenerator;
    private readonly IEmbeddingAppService _embeddingService;
    private readonly IProcessedCommitRepository<ProcessedCommit> _commitRepo;
    private readonly IArticleRepository<Article> _articleRepo;
    private readonly GitHubSettings _githubSettings;
    private readonly ILogger<GitNewsProcessorService> _logger;

    public GitNewsProcessorService(
        IGitHubAppService githubService,
        IBlogGeneratorAppService blogGenerator,
        IEmbeddingAppService embeddingService,
        IProcessedCommitRepository<ProcessedCommit> commitRepo,
        IArticleRepository<Article> articleRepo,
        IOptions<GitHubSettings> githubSettings,
        ILogger<GitNewsProcessorService> logger)
    {
        _githubService = githubService;
        _blogGenerator = blogGenerator;
        _embeddingService = embeddingService;
        _commitRepo = commitRepo;
        _articleRepo = articleRepo;
        _githubSettings = githubSettings.Value;
        _logger = logger;
    }

    public async Task<ProcessingResultInfo> ProcessAllRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var repoNames = await _githubService.GetRepositoryNamesAsync(
            _githubSettings.Owner,
            _githubSettings.IncludeForks);
        var repositories = repoNames.ToList();

        _logger.LogInformation("Total repositories to process: {Count}", repositories.Count);

        var result = new ProcessingResultInfo { TotalCount = repositories.Count };

        for (int i = 0; i < repositories.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var repoName = repositories[i];
            _logger.LogInformation("[{Current}/{Total}] Processing: {Owner}/{Repo}",
                i + 1, repositories.Count, _githubSettings.Owner, repoName);

            try
            {
                var processed = await ProcessRepositoryAsync(repoName, cancellationToken);
                if (processed == null)
                    result.SkippedCount++;
                else if (processed == true)
                    result.SuccessCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {Repo}", repoName);
                result.ErrorCount++;
            }
        }

        _logger.LogInformation("Processing completed! Success: {Success} | Skipped: {Skipped} | Errors: {Errors} | Total: {Total}",
            result.SuccessCount, result.SkippedCount, result.ErrorCount, result.TotalCount);

        return result;
    }

    private async Task<bool?> ProcessRepositoryAsync(string repoName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[1/6] Collecting repository data...");
        var context = await _githubService.GetRepositoryContextAsync(
            _githubSettings.Owner,
            repoName,
            _githubSettings.MaxCommits);

        if (context.Commits.Count == 0)
        {
            _logger.LogInformation("No recent commits found. Skipping");
            return null;
        }

        _logger.LogInformation("[2/6] Checking already processed commits...");
        var repoFullName = $"{_githubSettings.Owner}/{repoName}";
        var unprocessedCommits = new List<CommitInfoDto>();

        foreach (var commit in context.Commits)
        {
            if (!await _commitRepo.IsCommitProcessedAsync(repoFullName, commit.Sha))
                unprocessedCommits.Add(commit);
        }

        if (unprocessedCommits.Count == 0)
        {
            _logger.LogInformation("All commits already processed. Skipping");
            return null;
        }

        _logger.LogInformation("New commits: {NewCount} of {TotalCount}", unprocessedCommits.Count, context.Commits.Count);
        context.Commits = unprocessedCommits;

        _logger.LogInformation("Total commits in repo: {TotalCommitCount} ({ProjectType})",
            context.TotalCommitCount,
            context.TotalCommitCount <= 3 ? "new project" : "existing project");

        _logger.LogInformation("[3/6] Generating blog article via ChatGPT...");
        var blogPost = await _blogGenerator.GenerateBlogPostAsync(context);

        await _commitRepo.MarkAsProcessedAsync(repoFullName, unprocessedCommits.Select(c => c.Sha));

        if (blogPost.Title.Contains("Sem novidades", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(blogPost.Content))
        {
            _logger.LogInformation("No technical novelties identified. Skipping");
            return null;
        }

        _logger.LogInformation("[4/6] Generating article embedding...");
        var embeddingText = $"{blogPost.Title} {blogPost.Content}";
        var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText);

        _logger.LogInformation("[5/6] Checking for similar articles...");
        var similarArticles = await _articleRepo.FindSimilarAsync(embedding);

        if (similarArticles.Count > 0)
        {
            _logger.LogInformation("Similar article already exists: \"{Title}\". Skipping", similarArticles[0].Title);
            return null;
        }

        _logger.LogInformation("Title: {Title}", blogPost.Title);
        _logger.LogInformation("Category: {Category}", blogPost.Category);
        _logger.LogInformation("Tags: {Tags}", string.Join(", ", blogPost.Tags));

        _logger.LogInformation("[6/6] Saving article to database...");
        var article = new Article
        {
            Title = blogPost.Title,
            Content = blogPost.Content,
            Category = blogPost.Category,
            Tags = string.Join(", ", blogPost.Tags),
            Repository = repoFullName,
            Author = blogPost.Author,
            Slug = blogPost.Slug,
            Embedding = embedding,
            CreatedAt = DateTime.UtcNow
        };
        await _articleRepo.SaveAsync(article);
        _logger.LogInformation("Article saved: {Title}", article.Title);

        return true;
    }
}

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
    private readonly IDallEAppService _dallEService;
    private readonly IMediumAppService _mediumService;
    private readonly ILinkedInAppService _linkedInService;
    private readonly IProcessedCommitRepository<ProcessedCommit> _commitRepo;
    private readonly IArticleRepository<Article> _articleRepo;
    private readonly GitHubSettings _githubSettings;
    private readonly ILogger<GitNewsProcessorService> _logger;

    public GitNewsProcessorService(
        IGitHubAppService githubService,
        IBlogGeneratorAppService blogGenerator,
        IEmbeddingAppService embeddingService,
        IDallEAppService dallEService,
        IMediumAppService mediumService,
        ILinkedInAppService linkedInService,
        IProcessedCommitRepository<ProcessedCommit> commitRepo,
        IArticleRepository<Article> articleRepo,
        IOptions<GitHubSettings> githubSettings,
        ILogger<GitNewsProcessorService> logger)
    {
        _githubService = githubService;
        _blogGenerator = blogGenerator;
        _embeddingService = embeddingService;
        _dallEService = dallEService;
        _mediumService = mediumService;
        _linkedInService = linkedInService;
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
        _logger.LogInformation("[1/7] Collecting repository data...");
        var context = await _githubService.GetRepositoryContextAsync(
            _githubSettings.Owner,
            repoName,
            _githubSettings.MaxCommits);

        if (context.Commits.Count == 0)
        {
            _logger.LogInformation("No recent commits found. Skipping");
            return null;
        }

        _logger.LogInformation("[2/7] Checking already processed commits...");
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

        _logger.LogInformation("[3/7] Generating blog article via ChatGPT...");
        var blogPost = await _blogGenerator.GenerateBlogPostAsync(context);

        await _commitRepo.MarkAsProcessedAsync(repoFullName, unprocessedCommits.Select(c => c.Sha));

        if (blogPost.Title.Contains("Sem novidades", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(blogPost.Content))
        {
            _logger.LogInformation("No technical novelties identified. Skipping");
            return null;
        }

        _logger.LogInformation("[4/7] Generating article embedding...");
        var embeddingText = $"{blogPost.Title} {blogPost.Content}";
        var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText);

        _logger.LogInformation("[5/7] Checking for similar articles...");
        var similarArticles = await _articleRepo.FindSimilarAsync(embedding);

        if (similarArticles.Count > 0)
        {
            _logger.LogInformation("Similar article already exists: \"{Title}\". Skipping", similarArticles[0].Title);
            return null;
        }

        _logger.LogInformation("Title: {Title}", blogPost.Title);
        _logger.LogInformation("Category: {Category}", blogPost.Category);
        _logger.LogInformation("Tags: {Tags}", string.Join(", ", blogPost.Tags));

        _logger.LogInformation("[6/7] Generating article image via DALL-E...");
        string? imageBase64 = null;
        try
        {
            var imagePrompt = $"Create a modern, minimalist tech blog header image about: {blogPost.Title}. Style: flat design, vibrant colors, no text.";
            imageBase64 = await _dallEService.GenerateImageBase64Async(imagePrompt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate image. Article will be saved without image");
        }

        _logger.LogInformation("[7/7] Saving article to database...");
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
            ImageBase64 = imageBase64,
            CreatedAt = DateTime.UtcNow
        };
        await _articleRepo.SaveAsync(article);
        _logger.LogInformation("Article saved: {Title}", article.Title);

        return true;
    }

    public async Task<bool> ExportOldestUnprocessedArticleAsync(string outputDir, CancellationToken cancellationToken = default)
    {
        var article = await _articleRepo.FindOldestUnprocessedAsync();

        if (article == null)
        {
            _logger.LogInformation("No unprocessed articles found");
            return false;
        }

        _logger.LogInformation("Exporting article: {Title}", article.Title);

        Directory.CreateDirectory(outputDir);

        var slug = article.Slug;
        if (string.IsNullOrWhiteSpace(slug))
            slug = $"article-{article.Id}";

        // Generate image if missing
        if (string.IsNullOrWhiteSpace(article.ImageBase64))
        {
            _logger.LogInformation("Article has no image. Generating via DALL-E...");
            try
            {
                var imagePrompt = $"Create a modern, minimalist tech blog header image about: {article.Title}. Style: flat design, vibrant colors, no text.";
                article.ImageBase64 = await _dallEService.GenerateImageBase64Async(imagePrompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate image");
            }
        }

        // Save image as PNG
        var imageFileName = $"{slug}.png";
        if (!string.IsNullOrWhiteSpace(article.ImageBase64))
        {
            var imagePath = Path.Combine(outputDir, imageFileName);
            var imageBytes = Convert.FromBase64String(article.ImageBase64);
            await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken);
            _logger.LogInformation("Image saved: {Path}", imagePath);
        }

        // Build markdown with YAML front matter
        var md = new System.Text.StringBuilder();
        md.AppendLine("---");
        md.AppendLine($"title: \"{article.Title.Replace("\"", "\\\"")}\"");
        md.AppendLine($"slug: \"{slug}\"");
        md.AppendLine($"category: \"{article.Category}\"");
        md.AppendLine($"tags: [{string.Join(", ", article.Tags.Split(',', StringSplitOptions.TrimEntries).Select(t => $"\"{t}\""))}]");
        md.AppendLine($"author: \"{article.Author}\"");
        md.AppendLine($"date: \"{article.CreatedAt:yyyy-MM-dd}\"");
        if (!string.IsNullOrWhiteSpace(article.ImageBase64))
            md.AppendLine($"image: \"{imageFileName}\"");
        md.AppendLine("---");
        md.AppendLine();
        md.AppendLine(article.Content);

        var mdPath = Path.Combine(outputDir, $"{slug}.md");
        await File.WriteAllTextAsync(mdPath, md.ToString(), cancellationToken);
        _logger.LogInformation("Markdown saved: {Path}", mdPath);

        // Mark as processed
        article.IsProcessed = true;
        await _articleRepo.UpdateAsync(article);
        _logger.LogInformation("Article marked as processed: {Title}", article.Title);

        return true;
    }

    public async Task GenerateMissingImagesAsync(CancellationToken cancellationToken = default)
    {
        var articles = await _articleRepo.FindWithoutImageAsync();

        if (articles.Count == 0)
        {
            _logger.LogInformation("All articles already have images");
            return;
        }

        _logger.LogInformation("Found {Count} articles without images", articles.Count);

        for (int i = 0; i < articles.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var article = articles[i];
            _logger.LogInformation("[{Current}/{Total}] Generating image for: {Title}", i + 1, articles.Count, article.Title);

            try
            {
                var imagePrompt = $"Create a modern, minimalist tech blog header image about: {article.Title}. Style: flat design, vibrant colors, no text.";
                article.ImageBase64 = await _dallEService.GenerateImageBase64Async(imagePrompt);
                await _articleRepo.UpdateAsync(article);
                _logger.LogInformation("Image saved for article: {Title}", article.Title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate image for article: {Title}", article.Title);
            }
        }
    }

    public async Task<bool> PublishOldestUnprocessedToMediumAsync(CancellationToken cancellationToken = default)
    {
        var article = await _articleRepo.FindOldestUnprocessedAsync();

        if (article == null)
        {
            _logger.LogInformation("No unprocessed articles found to publish");
            return false;
        }

        _logger.LogInformation("Publishing to Medium: {Title}", article.Title);

        // Ensure user is logged in
        await _mediumService.EnsureLoggedInAsync(cancellationToken);

        // Generate image if missing
        if (string.IsNullOrWhiteSpace(article.ImageBase64))
        {
            _logger.LogInformation("Article has no image. Generating via DALL-E...");
            try
            {
                var imagePrompt = $"Create a modern, minimalist tech blog header image about: {article.Title}. Style: flat design, vibrant colors, no text.";
                article.ImageBase64 = await _dallEService.GenerateImageBase64Async(imagePrompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate image");
            }
        }

        // Prepare cover image bytes
        byte[]? coverImage = null;
        if (!string.IsNullOrWhiteSpace(article.ImageBase64))
        {
            coverImage = Convert.FromBase64String(article.ImageBase64);
        }

        // Parse tags
        var tags = article.Tags
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Take(5)
            .ToArray();

        // Publish
        var articleUrl = await _mediumService.PublishArticleAsync(
            article.Title,
            article.Content,
            tags,
            coverImage,
            cancellationToken);

        _logger.LogInformation("Article published on Medium: {Url}", articleUrl);

        // Mark as processed
        article.IsProcessed = true;
        await _articleRepo.UpdateAsync(article);
        _logger.LogInformation("Article marked as processed: {Title}", article.Title);

        return true;
    }

    public async Task<bool> PublishOldestUnprocessedToLinkedInAsync(CancellationToken cancellationToken = default)
    {
        var article = await _articleRepo.FindOldestUnprocessedAsync();

        if (article == null)
        {
            _logger.LogInformation("No unprocessed articles found to publish");
            return false;
        }

        _logger.LogInformation("Publishing to LinkedIn: {Title}", article.Title);

        // Ensure user is logged in
        await _linkedInService.EnsureLoggedInAsync(cancellationToken);

        // Generate image if missing
        if (string.IsNullOrWhiteSpace(article.ImageBase64))
        {
            _logger.LogInformation("Article has no image. Generating via DALL-E...");
            try
            {
                var imagePrompt = $"Create a modern, minimalist tech blog header image about: {article.Title}. Style: flat design, vibrant colors, no text.";
                article.ImageBase64 = await _dallEService.GenerateImageBase64Async(imagePrompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate image");
            }
        }

        // Prepare cover image bytes
        byte[]? coverImage = null;
        if (!string.IsNullOrWhiteSpace(article.ImageBase64))
        {
            coverImage = Convert.FromBase64String(article.ImageBase64);
        }

        // Parse tags
        var tags = article.Tags
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Take(5)
            .ToArray();

        // Publish
        var articleUrl = await _linkedInService.PublishArticleAsync(
            article.Title,
            article.Content,
            tags,
            coverImage,
            cancellationToken);

        _logger.LogInformation("Article published on LinkedIn: {Url}", articleUrl);

        // Mark as processed
        article.IsProcessed = true;
        await _articleRepo.UpdateAsync(article);
        _logger.LogInformation("Article marked as processed: {Title}", article.Title);

        return true;
    }
}

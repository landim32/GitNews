using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace GitNews.Infra.AppServices;

public class GitHubAppService : IGitHubAppService
{
    private readonly GitHubClient _client;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubAppService> _logger;

    public GitHubAppService(IOptions<GitHubSettings> settings, ILogger<GitHubAppService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _client = new GitHubClient(new ProductHeaderValue("GitNews"))
        {
            Credentials = new Credentials(_settings.Token)
        };
    }

    public async Task<IReadOnlyList<string>> GetRepositoryNamesAsync(string owner, bool includeForks)
    {
        _logger.LogInformation("Listing repositories for {Owner}...", owner);

        var repos = await _client.Repository.GetAllForUser(owner);

        var filtered = repos.AsEnumerable();

        if (!includeForks)
            filtered = filtered.Where(r => !r.Fork);

        var names = filtered.Select(r => r.Name).ToList();
        _logger.LogInformation("Total repositories found: {Count}", names.Count);

        return names;
    }

    public async Task<RepositoryContextInfo> GetRepositoryContextAsync(string owner, string repository, int maxCommits)
    {
        var context = new RepositoryContextInfo
        {
            Owner = owner,
            Repository = repository
        };

        _logger.LogInformation("Fetching README for {Owner}/{Repository}...", owner, repository);
        context.ReadmeContent = await GetReadmeAsync(owner, repository);

        _logger.LogInformation("Fetching total commit count...");
        context.TotalCommitCount = await GetTotalCommitCountAsync(owner, repository);
        _logger.LogInformation("Total commits in repository: {Count}", context.TotalCommitCount);

        _logger.LogInformation("Fetching last {MaxCommits} commits from main...", maxCommits);
        context.Commits = await GetCommitsAsync(owner, repository, maxCommits);

        _logger.LogInformation("Commits found: {Count}", context.Commits.Count);
        return context;
    }

    private async Task<int> GetTotalCommitCountAsync(string owner, string repository)
    {
        try
        {
            var repo = await _client.Repository.Get(owner, repository);
            var defaultBranch = repo.DefaultBranch;

            var request = new CommitRequest { Sha = defaultBranch };
            var apiOptions = new ApiOptions { PageSize = 1, PageCount = 1 };

            var commits = await _client.Repository.Commit.GetAll(owner, repository, request, apiOptions);

            var allCommits = await _client.Repository.Commit.GetAll(owner, repository, request);
            return allCommits.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count commits");
            return int.MaxValue;
        }
    }

    private async Task<string> GetReadmeAsync(string owner, string repository)
    {
        try
        {
            var readmeContents = await _client.Repository.Content.GetReadme(owner, repository);
            return readmeContents.Content;
        }
        catch (NotFoundException)
        {
            _logger.LogDebug("README.md not found");
            return string.Empty;
        }
    }

    private async Task<List<CommitInfoDto>> GetCommitsAsync(string owner, string repository, int maxCommits)
    {
        var commits = new List<CommitInfoDto>();

        try
        {
            var request = new CommitRequest
            {
                Sha = "main",
                Since = DateTimeOffset.UtcNow.AddDays(-90)
            };

            var apiOptions = new ApiOptions
            {
                PageSize = maxCommits,
                PageCount = 1
            };

            var githubCommits = await _client.Repository.Commit.GetAll(owner, repository, request, apiOptions);

            foreach (var githubCommit in githubCommits.Take(maxCommits))
            {
                var commitDetail = await _client.Repository.Commit.Get(owner, repository, githubCommit.Sha);

                var commitInfo = new CommitInfoDto
                {
                    Sha = githubCommit.Sha,
                    Message = githubCommit.Commit.Message,
                    Author = githubCommit.Commit.Author?.Name ?? "Unknown",
                    Date = githubCommit.Commit.Author?.Date ?? DateTimeOffset.MinValue,
                    Files = commitDetail.Files?.Select(f => new FileChangeInfo
                    {
                        FileName = f.Filename,
                        Status = f.Status,
                        Additions = f.Additions,
                        Deletions = f.Deletions,
                        Patch = TruncatePatch(f.Patch ?? string.Empty, 500)
                    }).ToList() ?? new List<FileChangeInfo>()
                };

                commits.Add(commitInfo);
            }
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Branch 'main' not found in {Owner}/{Repository}. Trying default branch...", owner, repository);

            try
            {
                var repo = await _client.Repository.Get(owner, repository);
                var defaultBranch = repo.DefaultBranch;

                var request = new CommitRequest
                {
                    Sha = defaultBranch,
                    Since = DateTimeOffset.UtcNow.AddDays(-90)
                };

                var apiOptions = new ApiOptions
                {
                    PageSize = maxCommits,
                    PageCount = 1
                };

                var githubCommits = await _client.Repository.Commit.GetAll(owner, repository, request, apiOptions);

                foreach (var githubCommit in githubCommits.Take(maxCommits))
                {
                    var commitDetail = await _client.Repository.Commit.Get(owner, repository, githubCommit.Sha);

                    var commitInfo = new CommitInfoDto
                    {
                        Sha = githubCommit.Sha,
                        Message = githubCommit.Commit.Message,
                        Author = githubCommit.Commit.Author?.Name ?? "Unknown",
                        Date = githubCommit.Commit.Author?.Date ?? DateTimeOffset.MinValue,
                        Files = commitDetail.Files?.Select(f => new FileChangeInfo
                        {
                            FileName = f.Filename,
                            Status = f.Status,
                            Additions = f.Additions,
                            Deletions = f.Deletions,
                            Patch = TruncatePatch(f.Patch ?? string.Empty, 500)
                        }).ToList() ?? new List<FileChangeInfo>()
                    };

                    commits.Add(commitInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch commits from {Owner}/{Repository}", owner, repository);
            }
        }

        return commits;
    }

    private static string TruncatePatch(string patch, int maxLength)
    {
        if (string.IsNullOrEmpty(patch))
            return string.Empty;

        return patch.Length <= maxLength
            ? patch
            : patch[..maxLength] + "\n... (truncated)";
    }
}

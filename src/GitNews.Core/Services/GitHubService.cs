using GitNews.Core.Interfaces;
using GitNews.Core.Models;
using Microsoft.Extensions.Options;
using Octokit;

namespace GitNews.Core.Services;

public class GitHubService : IGitHubService
{
    private readonly GitHubClient _client;
    private readonly GitHubSettings _settings;

    public GitHubService(IOptions<GitHubSettings> settings)
    {
        _settings = settings.Value;
        _client = new GitHubClient(new ProductHeaderValue("GitNews"))
        {
            Credentials = new Credentials(_settings.Token)
        };
    }

    public async Task<RepositoryContext> GetRepositoryContextAsync(string owner, string repository, int maxCommits)
    {
        var context = new RepositoryContext
        {
            Owner = owner,
            Repository = repository
        };

        Console.WriteLine($"Buscando README do repositório {owner}/{repository}...");
        context.ReadmeContent = await GetReadmeAsync(owner, repository);

        Console.WriteLine($"Buscando últimos {maxCommits} commits...");
        context.Commits = await GetCommitsAsync(owner, repository, maxCommits);

        Console.WriteLine($"Total de commits encontrados: {context.Commits.Count}");
        return context;
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
            Console.WriteLine("README.md não encontrado no repositório.");
            return string.Empty;
        }
    }

    private async Task<List<CommitInfo>> GetCommitsAsync(string owner, string repository, int maxCommits)
    {
        var commits = new List<CommitInfo>();

        var request = new CommitRequest
        {
            // Pega commits dos últimos 90 dias por padrão
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

            var commitInfo = new CommitInfo
            {
                Sha = githubCommit.Sha,
                Message = githubCommit.Commit.Message,
                Author = githubCommit.Commit.Author?.Name ?? "Desconhecido",
                Date = githubCommit.Commit.Author?.Date ?? DateTimeOffset.MinValue,
                Files = commitDetail.Files?.Select(f => new FileChange
                {
                    FileName = f.Filename,
                    Status = f.Status,
                    Additions = f.Additions,
                    Deletions = f.Deletions,
                    Patch = TruncatePatch(f.Patch ?? string.Empty, 500)
                }).ToList() ?? new List<FileChange>()
            };

            commits.Add(commitInfo);
        }

        return commits;
    }

    private static string TruncatePatch(string patch, int maxLength)
    {
        if (string.IsNullOrEmpty(patch))
            return string.Empty;

        return patch.Length <= maxLength
            ? patch
            : patch[..maxLength] + "\n... (truncado)";
    }
}

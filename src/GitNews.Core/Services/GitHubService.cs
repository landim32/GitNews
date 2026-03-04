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

    public async Task<IReadOnlyList<string>> GetRepositoryNamesAsync(string owner, bool includeForks)
    {
        Console.WriteLine($"Listando repositórios de {owner}...");

        var repos = await _client.Repository.GetAllForUser(owner, new RepositoryRequest
        {
            Sort = RepositorySort.Updated,
            Direction = SortDirection.Descending
        });

        var filtered = repos.AsEnumerable();

        if (!includeForks)
            filtered = filtered.Where(r => !r.Fork);

        var names = filtered.Select(r => r.Name).ToList();
        Console.WriteLine($"Total de repositórios encontrados: {names.Count}");

        return names;
    }

    public async Task<RepositoryContext> GetRepositoryContextAsync(string owner, string repository, int maxCommits)
    {
        var context = new RepositoryContext
        {
            Owner = owner,
            Repository = repository
        };

        Console.WriteLine($"  Buscando README de {owner}/{repository}...");
        context.ReadmeContent = await GetReadmeAsync(owner, repository);

        Console.WriteLine($"  Buscando últimos {maxCommits} commits da main...");
        context.Commits = await GetCommitsAsync(owner, repository, maxCommits);

        Console.WriteLine($"  Commits encontrados: {context.Commits.Count}");
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
            Console.WriteLine("  README.md não encontrado.");
            return string.Empty;
        }
    }

    private async Task<List<CommitInfo>> GetCommitsAsync(string owner, string repository, int maxCommits)
    {
        var commits = new List<CommitInfo>();

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
        }
        catch (NotFoundException)
        {
            Console.WriteLine($"  Branch 'main' não encontrada em {owner}/{repository}. Tentando branch padrão...");

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Erro ao buscar commits: {ex.Message}");
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
            : patch[..maxLength] + "\n... (truncado)";
    }
}

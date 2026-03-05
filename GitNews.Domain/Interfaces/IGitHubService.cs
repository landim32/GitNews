using GitNews.Domain.Models;

namespace GitNews.Domain.Interfaces;

public interface IGitHubService
{
    Task<IReadOnlyList<string>> GetRepositoryNamesAsync(string owner, bool includeForks);
    Task<RepositoryContext> GetRepositoryContextAsync(string owner, string repository, int maxCommits);
}

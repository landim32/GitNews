using GitNews.Core.Models;

namespace GitNews.Core.Interfaces;

public interface IGitHubService
{
    Task<IReadOnlyList<string>> GetRepositoryNamesAsync(string owner, bool includeForks);
    Task<RepositoryContext> GetRepositoryContextAsync(string owner, string repository, int maxCommits);
}

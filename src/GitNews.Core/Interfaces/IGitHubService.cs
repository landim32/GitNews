using GitNews.Core.Models;

namespace GitNews.Core.Interfaces;

public interface IGitHubService
{
    Task<RepositoryContext> GetRepositoryContextAsync(string owner, string repository, int maxCommits);
}

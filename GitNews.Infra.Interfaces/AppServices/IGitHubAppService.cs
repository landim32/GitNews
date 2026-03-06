using GitNews.DTO;

namespace GitNews.Infra.Interfaces.AppServices;

public interface IGitHubAppService
{
    Task<IReadOnlyList<string>> GetRepositoryNamesAsync(string owner, bool includeForks);
    Task<RepositoryContextInfo> GetRepositoryContextAsync(string owner, string repository, int maxCommits);
}

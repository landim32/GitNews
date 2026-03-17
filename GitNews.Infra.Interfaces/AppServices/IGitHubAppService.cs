using GitNews.DTO;

namespace GitNews.Infra.Interfaces.AppServices;

public interface IGitHubAppService
{
    Task<IReadOnlyList<string>> GetRepositoryNamesAsync(string owner, bool includeForks);
    Task<IReadOnlyList<RepositoryInfo>> GetAllRepositoriesAsync(string owner, bool includeForks, string organizations);
    Task<RepositoryContextInfo> GetRepositoryContextAsync(string owner, string repository, int maxCommits);
    Task<CommitInfoDto?> GetCommitDetailAsync(string owner, string repository, string sha);
}

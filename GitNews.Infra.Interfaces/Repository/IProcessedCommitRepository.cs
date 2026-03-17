namespace GitNews.Infra.Interfaces.Repository;

public interface IProcessedCommitRepository<TModel> where TModel : class
{
    Task<bool> IsCommitProcessedAsync(string repository, string sha);
    Task MarkAsProcessedAsync(string repository, string sha);
    Task MarkAsProcessedAsync(string repository, IEnumerable<string> shas);
    Task ImportCommitsAsync(string repository, IEnumerable<string> shas);
    Task<List<TModel>> GetRandomUnprocessedAsync(int count);
    Task SetCommitProcessedAsync(long id);
}

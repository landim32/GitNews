using GitNews.DTO;

namespace GitNews.Domain.Interfaces;

public interface IGitNewsProcessorService
{
    Task<ProcessingResultInfo> ImportCommitsAsync(CancellationToken cancellationToken = default);
    Task<ProcessingResultInfo> ProcessArticlesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExportOldestUnprocessedArticleAsync(string outputDir, CancellationToken cancellationToken = default);
    Task<bool> PublishOldestUnprocessedToMediumAsync(CancellationToken cancellationToken = default);
    Task<bool> PublishOldestUnprocessedToNNewsAsync(CancellationToken cancellationToken = default);
}

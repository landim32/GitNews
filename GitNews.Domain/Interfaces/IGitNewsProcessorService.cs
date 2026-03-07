using GitNews.DTO;

namespace GitNews.Domain.Interfaces;

public interface IGitNewsProcessorService
{
    Task<ProcessingResultInfo> ProcessAllRepositoriesAsync(CancellationToken cancellationToken = default);
    Task GenerateMissingImagesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExportOldestUnprocessedArticleAsync(string outputDir, CancellationToken cancellationToken = default);
}

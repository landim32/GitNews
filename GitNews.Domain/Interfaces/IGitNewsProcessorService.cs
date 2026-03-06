using GitNews.DTO;

namespace GitNews.Domain.Interfaces;

public interface IGitNewsProcessorService
{
    Task<ProcessingResultInfo> ProcessAllRepositoriesAsync(CancellationToken cancellationToken = default);
}

using GitNews.Infra.Interfaces.AppServices;

namespace GitNews.Worker;

public class NonInteractiveUserInteractionService : IUserInteractionService
{
    public Task WaitForUserActionAsync(string message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            $"User interaction required but running in non-interactive mode: {message}");
    }
}

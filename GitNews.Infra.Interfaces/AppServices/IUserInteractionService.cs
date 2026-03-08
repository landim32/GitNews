namespace GitNews.Infra.Interfaces.AppServices;

public interface IUserInteractionService
{
    Task WaitForUserActionAsync(string message, CancellationToken cancellationToken = default);
}

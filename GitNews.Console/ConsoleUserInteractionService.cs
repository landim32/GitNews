using GitNews.Infra.Interfaces.AppServices;

namespace GitNews.Console;

public class ConsoleUserInteractionService : IUserInteractionService
{
    public Task WaitForUserActionAsync(string message, CancellationToken cancellationToken = default)
    {
        System.Console.WriteLine();
        System.Console.WriteLine(message);
        System.Console.ReadLine();
        return Task.CompletedTask;
    }
}

using GitNews.Domain.Interfaces;
using GitNews.DTO;
using GitNews.Infra.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitNews.Worker;

public class GitNewsWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GitNewsWorker> _logger;
    private readonly WorkerSettings _workerSettings;

    public GitNewsWorker(
        IServiceProvider serviceProvider,
        ILogger<GitNewsWorker> logger,
        IOptions<WorkerSettings> workerSettings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _workerSettings = workerSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GitNews Worker started. Scheduled to run daily at {ScheduleTime}", _workerSettings.ScheduleTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNextRun();
            _logger.LogInformation("Next run at {NextRun} (in {Hours}h {Minutes}m)",
                DateTime.Now.Add(delay).ToString("yyyy-MM-dd HH:mm"),
                (int)delay.TotalHours,
                delay.Minutes);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            _logger.LogInformation("Starting scheduled processing...");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var provider = scope.ServiceProvider;

                var dbContext = provider.GetRequiredService<GitNewsDbContext>();
                await dbContext.Database.MigrateAsync(stoppingToken);
                _logger.LogInformation("Database updated");

                var processor = provider.GetRequiredService<IGitNewsProcessorService>();
                await processor.ProcessAllRepositoriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled processing");
            }
        }

        _logger.LogInformation("GitNews Worker stopped");
    }

    private TimeSpan CalculateDelayUntilNextRun()
    {
        if (!TimeSpan.TryParse(_workerSettings.ScheduleTime, out var scheduledTime))
        {
            _logger.LogWarning("Invalid ScheduleTime '{ScheduleTime}'. Defaulting to 19:00", _workerSettings.ScheduleTime);
            scheduledTime = new TimeSpan(19, 0, 0);
        }

        var now = DateTime.Now;
        var nextRun = now.Date.Add(scheduledTime);

        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}

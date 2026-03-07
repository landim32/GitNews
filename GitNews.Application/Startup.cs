using GitNews.Domain.Interfaces;
using GitNews.Domain.Services;
using GitNews.DTO;
using GitNews.Infra;
using GitNews.Infra.AppServices;
using GitNews.Infra.Interfaces.AppServices;
using GitNews.Infra.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitNews.Application;

public static class Startup
{
    public static IServiceCollection ConfigureServices(
        this IServiceCollection services,
        Action<GitNewsSettings> configureSettings)
    {
        var settings = new GitNewsSettings();
        configureSettings(settings);

        services.Configure<GitHubSettings>(opt =>
        {
            opt.Token = settings.GitHub.Token;
            opt.Owner = settings.GitHub.Owner;
            opt.MaxCommits = settings.GitHub.MaxCommits;
            opt.IncludeForks = settings.GitHub.IncludeForks;
        });

        services.Configure<OpenAISettings>(opt =>
        {
            opt.ApiKey = settings.OpenAI.ApiKey;
            opt.Model = settings.OpenAI.Model;
            opt.BaseUrl = settings.OpenAI.BaseUrl;
        });

        // Logging
        services.AddLogging(builder => builder.AddConsole());

        // Database provider
        var provider = settings.Database.Provider?.Trim();
        if (string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSqliteInfra(settings.Database.ConnectionString);
        }
        else
        {
            services.AddPostgreSqlInfra(settings.Database.ConnectionString);
        }

        // AppServices
        services.AddSingleton<IGitHubAppService, GitHubAppService>();
        services.AddHttpClient<IBlogGeneratorAppService, BlogGeneratorAppService>();
        services.AddHttpClient<IEmbeddingAppService, EmbeddingAppService>();
        services.AddHttpClient<IDallEAppService, DallEAppService>();

        // Domain Services
        services.AddScoped<IGitNewsProcessorService, GitNewsProcessorService>();

        return services;
    }
}

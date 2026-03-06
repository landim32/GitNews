using GitNews.Domain.Interfaces;
using GitNews.Domain.Models;
using GitNews.Domain.Services;
using GitNews.DTO;
using GitNews.Infra.AppServices;
using GitNews.Infra.Context;
using GitNews.Infra.Interfaces.AppServices;
using GitNews.Infra.Interfaces.Repository;
using GitNews.Infra.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

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

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(settings.Database.ConnectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<GitNewsDbContext>(options =>
            options.UseNpgsql(dataSource, o => o.UseVector()));

        // Repositories
        services.AddScoped<IProcessedCommitRepository<ProcessedCommit>, ProcessedCommitRepository>();
        services.AddScoped<IArticleRepository<Article>, ArticleRepository>();

        // AppServices
        services.AddSingleton<IGitHubAppService, GitHubAppService>();
        services.AddHttpClient<IBlogGeneratorAppService, BlogGeneratorAppService>();
        services.AddHttpClient<IEmbeddingAppService, EmbeddingAppService>();

        // Domain Services
        services.AddScoped<IGitNewsProcessorService, GitNewsProcessorService>();

        return services;
    }
}

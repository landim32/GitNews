using GitNews.Domain.Interfaces;
using GitNews.Domain.Models;
using GitNews.Domain.Services;
using GitNews.Infra.Services;
using Microsoft.Extensions.DependencyInjection;

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
            opt.Repository = settings.GitHub.Repository;
            opt.MaxCommits = settings.GitHub.MaxCommits;
            opt.IncludeForks = settings.GitHub.IncludeForks;
        });

        services.Configure<OpenAISettings>(opt =>
        {
            opt.ApiKey = settings.OpenAI.ApiKey;
            opt.Model = settings.OpenAI.Model;
            opt.BaseUrl = settings.OpenAI.BaseUrl;
        });

        services.Configure<OutputSettings>(opt =>
        {
            opt.OutputDirectory = settings.Output.OutputDirectory;
        });

        services.AddSingleton<IGitHubService, GitHubService>();
        services.AddHttpClient<IBlogGeneratorService, BlogGeneratorService>();
        services.AddSingleton<IMarkdownWriter, MarkdownWriter>();

        return services;
    }
}

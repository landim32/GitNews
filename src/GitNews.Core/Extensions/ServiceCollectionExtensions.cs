using GitNews.Core.Interfaces;
using GitNews.Core.Models;
using GitNews.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GitNews.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGitNewsServices(
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

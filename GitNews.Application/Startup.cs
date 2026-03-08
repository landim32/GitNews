using GitNews.Domain.Interfaces;
using GitNews.Domain.Services;
using GitNews.DTO;
using GitNews.Infra;
using GitNews.Infra.AppServices;
using GitNews.Infra.Interfaces.AppServices;
using GitNews.Infra.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NAuth.ACL;
using NAuth.ACL.Interfaces;
using NAuth.DTO;
using NNews.ACL;
using NNews.ACL.Interfaces;
using NNews.DTO.Settings;

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

        services.Configure<NNewsSettings>(opt =>
        {
            opt.ApiUrl = settings.NNews.ApiUrl;
            opt.Email = settings.NNews.Email;
            opt.Password = settings.NNews.Password;
        });

        services.Configure<NNewsSetting>(opt =>
        {
            opt.ApiUrl = settings.NNews.ApiUrl;
        });

        services.Configure<NAuthSetting>(opt =>
        {
            opt.ApiUrl = settings.NNews.ApiUrl;
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
        services.AddSingleton<IMediumAppService, MediumAppService>();
        services.AddSingleton<ILinkedInAppService, LinkedInAppService>();
        services.AddHttpClient<IArticleClient, ArticleClient>();
        services.AddHttpClient<IUserClient, UserClient>();
        services.AddScoped<INNewsAppService, NNewsAppService>();

        // Domain Services
        services.AddScoped<IGitNewsProcessorService, GitNewsProcessorService>();

        return services;
    }
}

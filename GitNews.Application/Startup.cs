using GitNews.Domain.Interfaces;
using GitNews.Domain.Services;
using GitNews.DTO;
using GitNews.Infra;
using GitNews.Infra.AppServices;
using GitNews.Infra.Interfaces.AppServices;
using GitNews.Infra.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GitNews.Infra.Handlers;
using NAuth.ACL;
using NAuth.ACL.Interfaces;
using NAuth.DTO.Settings;
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
            opt.AuthUrl = settings.NNews.AuthUrl;
            opt.Email = settings.NNews.Email;
            opt.Password = settings.NNews.Password;
            opt.TenantId = settings.NNews.TenantId;
        });

        var nnewsApiUrl = string.IsNullOrWhiteSpace(settings.NNews.ApiUrl)
            ? "https://not-configured/"
            : settings.NNews.ApiUrl.TrimEnd('/') + "/";

        var nauthApiUrl = string.IsNullOrWhiteSpace(settings.NNews.AuthUrl)
            ? "https://not-configured/"
            : settings.NNews.AuthUrl.TrimEnd('/') + "/";

        services.Configure<NNewsSetting>(opt =>
        {
            opt.ApiUrl = nnewsApiUrl;
        });

        services.Configure<NAuthSetting>(opt =>
        {
            opt.ApiUrl = nauthApiUrl;
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
        services.AddSingleton<ICodeImageAppService, CodeImageAppService>();
        services.AddSingleton<NNewsTokenStore>();
        services.AddTransient<NNewsTenantHandler>();
        services.AddTransient<NNewsAuthHandler>();
        services.AddHttpClient<IUserClient, UserClient>()
            .AddHttpMessageHandler<NNewsTenantHandler>();
        services.AddHttpClient<IArticleClient, ArticleClient>()
            .AddHttpMessageHandler<NNewsTenantHandler>()
            .AddHttpMessageHandler<NNewsAuthHandler>();
        services.AddHttpClient<ICategoryClient, CategoryClient>()
            .AddHttpMessageHandler<NNewsTenantHandler>()
            .AddHttpMessageHandler<NNewsAuthHandler>();
        services.AddHttpClient<INNewsAppService, NNewsAppService>()
            .AddHttpMessageHandler<NNewsTenantHandler>()
            .AddHttpMessageHandler<NNewsAuthHandler>();

        // Domain Services
        services.AddScoped<IGitNewsProcessorService, GitNewsProcessorService>();

        return services;
    }
}

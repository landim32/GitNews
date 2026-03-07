using GitNews.Domain.Models;
using GitNews.Infra.Context;
using GitNews.Infra.Interfaces.Repository;
using GitNews.Infra.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GitNews.Infra;

public static class InfraServiceRegistration
{
    public static IServiceCollection AddPostgreSqlInfra(this IServiceCollection services, string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<GitNewsDbContext>(options =>
            options.UseNpgsql(dataSource, o => o.UseVector()));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<GitNewsDbContext>());

        services.AddScoped<IProcessedCommitRepository<ProcessedCommit>, ProcessedCommitRepository>();
        services.AddScoped<IArticleRepository<Article>, ArticleRepository>();

        return services;
    }
}

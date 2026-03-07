using GitNews.Domain.Models;
using GitNews.Infra.Interfaces.Repository;
using GitNews.Infra.Sqlite.Context;
using GitNews.Infra.Sqlite.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GitNews.Infra.Sqlite;

public static class InfraServiceRegistration
{
    public static IServiceCollection AddSqliteInfra(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GitNewsSqliteDbContext>(options =>
            options.UseSqlite(connectionString));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<GitNewsSqliteDbContext>());

        services.AddScoped<IProcessedCommitRepository<ProcessedCommit>, ProcessedCommitRepository>();
        services.AddScoped<IArticleRepository<Article>, ArticleRepository>();

        return services;
    }
}

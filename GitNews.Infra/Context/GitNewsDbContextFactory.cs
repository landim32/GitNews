using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GitNews.Infra.Context;

public class GitNewsDbContextFactory : IDesignTimeDbContextFactory<GitNewsDbContext>
{
    public GitNewsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "GitNews.Console"))
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var connectionString = configuration["Database:ConnectionString"]
            ?? "Host=localhost;Port=5432;Database=gitnews;Username=postgres;Password=postgres";

        // Design-time factory is PostgreSQL only — skip if connection string is for SQLite
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            connectionString = "Host=localhost;Port=5432;Database=gitnews;Username=postgres;Password=postgres";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<GitNewsDbContext>();
        optionsBuilder.UseNpgsql(dataSource, o => o.UseVector());

        return new GitNewsDbContext(optionsBuilder.Options);
    }
}

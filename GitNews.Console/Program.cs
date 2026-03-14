using GitNews.Application;
using GitNews.Domain.Interfaces;
using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitNews.Console;

class Program
{
    static async Task<int> Main(string[] args)
    {
        System.Console.WriteLine("========================================");
        System.Console.WriteLine("  GitNews - GitHub Blog Generator");
        System.Console.WriteLine("========================================");
        System.Console.WriteLine();

        try
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var settings = new GitNewsSettings();
            configuration.Bind(settings);

            ParseArguments(args, settings);

            if (!ValidateSettings(settings))
                return 1;

            var services = new ServiceCollection();
            services.ConfigureServices(opt =>
            {
                opt.GitHub = settings.GitHub;
                opt.OpenAI = settings.OpenAI;
                opt.Database = settings.Database;
            });
            services.AddSingleton<IUserInteractionService, ConsoleUserInteractionService>();

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database updated");
            }

            var command = GetCommand(args);

            using (var scope = provider.CreateScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<IGitNewsProcessorService>();

                if (command == "export")
                {
                    var outputDir = GetOutputDir(args);
                    var exported = await processor.ExportOldestUnprocessedArticleAsync(outputDir);
                    return exported ? 0 : 1;
                }

                if (command == "publish-medium")
                {
                    var published = await processor.PublishOldestUnprocessedToMediumAsync();
                    return published ? 0 : 1;
                }

                if (command == "publish-nnews")
                {
                    var published = await processor.PublishOldestUnprocessedToNNewsAsync();
                    return published ? 0 : 1;
                }

                if (command == "process")
                {
                    var result = await processor.ProcessAllRepositoriesAsync();
                    return result.HasErrors ? 1 : 0;
                }

                System.Console.WriteLine("Error: No valid command specified.");
                System.Console.WriteLine();
                PrintHelp();
                return 1;
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
            System.Console.WriteLine();
            System.Console.WriteLine("Use --help for available options.");
            return 1;
        }
    }

    private static string GetOutputDir(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--output-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "output");
    }

    private static string? GetCommand(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.Equals("--export", StringComparison.OrdinalIgnoreCase))
                return "export";
            if (arg.Equals("--publish-medium", StringComparison.OrdinalIgnoreCase))
                return "publish-medium";
            if (arg.Equals("--publish-nnews", StringComparison.OrdinalIgnoreCase))
                return "publish-nnews";
            if (arg.Equals("--process", StringComparison.OrdinalIgnoreCase))
                return "process";
        }
        return null;
    }

    private static void ParseArguments(string[] args, GitNewsSettings settings)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--help" or "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                case "--export":
                    break;
                case "--publish-medium":
                    break;
                case "--publish-nnews":
                    break;
                case "--process":
                    break;
                case "--output-dir":
                    if (i + 1 < args.Length) i++;
                    break;
                case "--owner" or "-o":
                    if (i + 1 < args.Length) settings.GitHub.Owner = args[++i];
                    break;
                case "--github-token":
                    if (i + 1 < args.Length) settings.GitHub.Token = args[++i];
                    break;
                case "--openai-key":
                    if (i + 1 < args.Length) settings.OpenAI.ApiKey = args[++i];
                    break;
                case "--model" or "-m":
                    if (i + 1 < args.Length) settings.OpenAI.Model = args[++i];
                    break;
                case "--max-commits":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var maxCommits))
                        settings.GitHub.MaxCommits = maxCommits;
                    break;
                case "--max-articles":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var maxArticles))
                        settings.GitHub.MaxArticles = maxArticles;
                    break;
                case "--include-forks":
                    settings.GitHub.IncludeForks = true;
                    break;
                case "--connection-string":
                    if (i + 1 < args.Length) settings.Database.ConnectionString = args[++i];
                    break;
            }
        }
    }

    private static bool ValidateSettings(GitNewsSettings settings)
    {
        var valid = true;

        if (string.IsNullOrWhiteSpace(settings.GitHub.Token))
        {
            System.Console.WriteLine("Error: GitHub token not configured.");
            System.Console.WriteLine("  Use --github-token <token> or env var GitHub__Token");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.GitHub.Owner))
        {
            System.Console.WriteLine("Error: Repository owner not configured.");
            System.Console.WriteLine("  Use --owner <owner> or env var GitHub__Owner");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.OpenAI.ApiKey))
        {
            System.Console.WriteLine("Error: OpenAI API key not configured.");
            System.Console.WriteLine("  Use --openai-key <key> or env var OpenAI__ApiKey");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.Database.ConnectionString))
        {
            System.Console.WriteLine("Error: PostgreSQL connection string not configured.");
            System.Console.WriteLine("  Use --connection-string <conn> or env var Database__ConnectionString");
            valid = false;
        }

        return valid;
    }

    private static void PrintHelp()
    {
        System.Console.WriteLine("GitNews - Blog article generator from GitHub repositories");
        System.Console.WriteLine();
        System.Console.WriteLine("Usage: GitNews.Console [options]");
        System.Console.WriteLine();
        System.Console.WriteLine("Commands:");
        System.Console.WriteLine("  --process                   Process all repositories and generate articles");
        System.Console.WriteLine("  --export                    Export oldest unprocessed article to output/ (markdown + image)");
        System.Console.WriteLine("  --output-dir <dir>          Output directory for --export (default: ./output)");
        System.Console.WriteLine("  --publish-medium            Publish oldest unprocessed article to Medium via Chrome CDP");
        System.Console.WriteLine("  --publish-nnews             Publish oldest unprocessed article to NNews API");
        System.Console.WriteLine();
        System.Console.WriteLine("Options:");
        System.Console.WriteLine("  -o, --owner <owner>         GitHub account owner");
        System.Console.WriteLine("  --github-token <token>      GitHub access token");
        System.Console.WriteLine("  --openai-key <key>          OpenAI API key (ChatGPT)");
        System.Console.WriteLine("  -m, --model <model>         ChatGPT model (default: gpt-4)");
        System.Console.WriteLine("  --max-commits <n>           Max commits per repository (default: 30)");
        System.Console.WriteLine("  --max-articles <n>          Max articles to generate per run (default: 3)");
        System.Console.WriteLine("  --include-forks             Include forked repositories");
        System.Console.WriteLine("  --connection-string <conn>  PostgreSQL connection string");
        System.Console.WriteLine("  -h, --help                  Show this help");
        System.Console.WriteLine();
        System.Console.WriteLine("Environment variables:");
        System.Console.WriteLine("  GitHub__Token                GitHub access token");
        System.Console.WriteLine("  GitHub__Owner                Account owner");
        System.Console.WriteLine("  OpenAI__ApiKey               OpenAI API key");
        System.Console.WriteLine("  OpenAI__Model                ChatGPT model");
        System.Console.WriteLine("  Database__ConnectionString   PostgreSQL connection string");
        System.Console.WriteLine();
        System.Console.WriteLine("Examples:");
        System.Console.WriteLine("  # Process all repositories from an account:");
        System.Console.WriteLine("  GitNews.Console --process --owner microsoft --github-token ghp_xxx --openai-key sk-xxx");
        System.Console.WriteLine();
        System.Console.WriteLine("  # Process with environment variables:");
        System.Console.WriteLine("  GitHub__Owner=microsoft GitNews.Console --process");
        System.Console.WriteLine();
        System.Console.WriteLine("  # Export an article:");
        System.Console.WriteLine("  GitNews.Console --export --output-dir ./output");
    }
}

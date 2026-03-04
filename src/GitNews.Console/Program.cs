using GitNews.Core.Extensions;
using GitNews.Core.Interfaces;
using GitNews.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables("GITNEWS_")
                .Build();

            var settings = new GitNewsSettings();
            configuration.Bind(settings);

            // Permite sobrescrever via argumentos de linha de comando
            ParseArguments(args, settings);

            if (!ValidateSettings(settings))
                return 1;

            var services = new ServiceCollection();
            services.AddGitNewsServices(opt =>
            {
                opt.GitHub = settings.GitHub;
                opt.OpenAI = settings.OpenAI;
                opt.Output = settings.Output;
            });

            var provider = services.BuildServiceProvider();

            var githubService = provider.GetRequiredService<IGitHubService>();
            var blogGenerator = provider.GetRequiredService<IBlogGeneratorService>();
            var markdownWriter = provider.GetRequiredService<IMarkdownWriter>();

            // 1. Buscar dados do repositório
            System.Console.WriteLine("[1/3] Coletando dados do repositório GitHub...");
            var context = await githubService.GetRepositoryContextAsync(
                settings.GitHub.Owner,
                settings.GitHub.Repository,
                settings.GitHub.MaxCommits);

            if (context.Commits.Count == 0)
            {
                System.Console.WriteLine("Nenhum commit encontrado. Encerrando.");
                return 0;
            }

            // 2. Gerar artigo de blog via ChatGPT
            System.Console.WriteLine("[2/3] Gerando artigo de blog via ChatGPT...");
            var blogPost = await blogGenerator.GenerateBlogPostAsync(context);

            System.Console.WriteLine($"  Título: {blogPost.Title}");
            System.Console.WriteLine($"  Categoria: {blogPost.Category}");
            System.Console.WriteLine($"  Tags: {string.Join(", ", blogPost.Tags)}");

            // 3. Salvar como Markdown
            System.Console.WriteLine("[3/3] Salvando artigo em Markdown...");
            var filePath = await markdownWriter.WritePostAsync(blogPost);

            System.Console.WriteLine();
            System.Console.WriteLine("========================================");
            System.Console.WriteLine("  Artigo gerado com sucesso!");
            System.Console.WriteLine($"  Arquivo: {filePath}");
            System.Console.WriteLine("========================================");

            return 0;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Erro: {ex.Message}");
            System.Console.WriteLine();
            System.Console.WriteLine("Use --help para ver as opções disponíveis.");
            return 1;
        }
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
                case "--owner" or "-o":
                    if (i + 1 < args.Length) settings.GitHub.Owner = args[++i];
                    break;
                case "--repo" or "-r":
                    if (i + 1 < args.Length) settings.GitHub.Repository = args[++i];
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
                case "--output" or "-d":
                    if (i + 1 < args.Length) settings.Output.OutputDirectory = args[++i];
                    break;
                case "--max-commits":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var max))
                        settings.GitHub.MaxCommits = max;
                    break;
            }
        }
    }

    private static bool ValidateSettings(GitNewsSettings settings)
    {
        var valid = true;

        if (string.IsNullOrWhiteSpace(settings.GitHub.Token))
        {
            System.Console.WriteLine("Erro: Token do GitHub não configurado.");
            System.Console.WriteLine("  Use --github-token <token> ou variável GITNEWS_GITHUB__TOKEN");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.GitHub.Owner))
        {
            System.Console.WriteLine("Erro: Owner do repositório não configurado.");
            System.Console.WriteLine("  Use --owner <owner> ou variável GITNEWS_GITHUB__OWNER");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.GitHub.Repository))
        {
            System.Console.WriteLine("Erro: Repositório não configurado.");
            System.Console.WriteLine("  Use --repo <repo> ou variável GITNEWS_GITHUB__REPOSITORY");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.OpenAI.ApiKey))
        {
            System.Console.WriteLine("Erro: API Key do OpenAI não configurada.");
            System.Console.WriteLine("  Use --openai-key <key> ou variável GITNEWS_OPENAI__APIKEY");
            valid = false;
        }

        return valid;
    }

    private static void PrintHelp()
    {
        System.Console.WriteLine("GitNews - Gerador de artigos de blog a partir de repositórios GitHub");
        System.Console.WriteLine();
        System.Console.WriteLine("Uso: GitNews.Console [opções]");
        System.Console.WriteLine();
        System.Console.WriteLine("Opções:");
        System.Console.WriteLine("  -o, --owner <owner>         Owner do repositório GitHub");
        System.Console.WriteLine("  -r, --repo <repo>           Nome do repositório GitHub");
        System.Console.WriteLine("  --github-token <token>      Token de acesso do GitHub");
        System.Console.WriteLine("  --openai-key <key>          API Key do OpenAI (ChatGPT)");
        System.Console.WriteLine("  -m, --model <model>         Modelo do ChatGPT (padrão: gpt-4)");
        System.Console.WriteLine("  -d, --output <dir>          Diretório de saída (padrão: ./output)");
        System.Console.WriteLine("  --max-commits <n>           Máximo de commits a analisar (padrão: 30)");
        System.Console.WriteLine("  -h, --help                  Exibir esta ajuda");
        System.Console.WriteLine();
        System.Console.WriteLine("Variáveis de ambiente:");
        System.Console.WriteLine("  GITNEWS_GITHUB__TOKEN       Token de acesso do GitHub");
        System.Console.WriteLine("  GITNEWS_GITHUB__OWNER       Owner do repositório");
        System.Console.WriteLine("  GITNEWS_GITHUB__REPOSITORY  Nome do repositório");
        System.Console.WriteLine("  GITNEWS_OPENAI__APIKEY      API Key do OpenAI");
        System.Console.WriteLine("  GITNEWS_OPENAI__MODEL       Modelo do ChatGPT");
        System.Console.WriteLine("  GITNEWS_OUTPUT__OUTPUTDIRECTORY  Diretório de saída");
        System.Console.WriteLine();
        System.Console.WriteLine("Exemplo:");
        System.Console.WriteLine("  GitNews.Console --owner microsoft --repo vscode --github-token ghp_xxx --openai-key sk-xxx");
    }
}

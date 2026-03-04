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
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
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

            // Determinar quais repositórios processar
            List<string> repositories;

            if (!string.IsNullOrWhiteSpace(settings.GitHub.Repository))
            {
                // Repositório específico configurado
                repositories = new List<string> { settings.GitHub.Repository };
                System.Console.WriteLine($"Processando repositório: {settings.GitHub.Owner}/{settings.GitHub.Repository}");
            }
            else
            {
                // Buscar todos os repositórios da conta
                var repoNames = await githubService.GetRepositoryNamesAsync(
                    settings.GitHub.Owner,
                    settings.GitHub.IncludeForks);
                repositories = repoNames.ToList();
            }

            System.Console.WriteLine($"Total de repositórios a processar: {repositories.Count}");
            System.Console.WriteLine();

            var successCount = 0;
            var errorCount = 0;

            for (int i = 0; i < repositories.Count; i++)
            {
                var repoName = repositories[i];
                System.Console.WriteLine($"[{i + 1}/{repositories.Count}] Processando: {settings.GitHub.Owner}/{repoName}");
                System.Console.WriteLine("----------------------------------------");

                try
                {
                    // 1. Buscar dados do repositório
                    System.Console.WriteLine("  [1/3] Coletando dados do repositório...");
                    var context = await githubService.GetRepositoryContextAsync(
                        settings.GitHub.Owner,
                        repoName,
                        settings.GitHub.MaxCommits);

                    if (context.Commits.Count == 0)
                    {
                        System.Console.WriteLine("  Nenhum commit recente encontrado. Pulando.");
                        System.Console.WriteLine();
                        continue;
                    }

                    // 2. Gerar artigo de blog via ChatGPT
                    System.Console.WriteLine("  [2/3] Gerando artigo de blog via ChatGPT...");
                    var blogPost = await blogGenerator.GenerateBlogPostAsync(context);

                    System.Console.WriteLine($"  Título: {blogPost.Title}");
                    System.Console.WriteLine($"  Categoria: {blogPost.Category}");
                    System.Console.WriteLine($"  Tags: {string.Join(", ", blogPost.Tags)}");

                    // 3. Salvar como Markdown
                    System.Console.WriteLine("  [3/3] Salvando artigo em Markdown...");
                    var filePath = await markdownWriter.WritePostAsync(blogPost);

                    System.Console.WriteLine($"  Arquivo: {filePath}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"  Erro ao processar {repoName}: {ex.Message}");
                    errorCount++;
                }

                System.Console.WriteLine();
            }

            System.Console.WriteLine("========================================");
            System.Console.WriteLine("  Processamento concluído!");
            System.Console.WriteLine($"  Sucesso: {successCount} | Erros: {errorCount} | Total: {repositories.Count}");
            System.Console.WriteLine("========================================");

            return errorCount > 0 ? 1 : 0;
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
                case "--include-forks":
                    settings.GitHub.IncludeForks = true;
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
        System.Console.WriteLine("  -o, --owner <owner>         Owner da conta GitHub");
        System.Console.WriteLine("  -r, --repo <repo>           Nome de um repositório específico (opcional)");
        System.Console.WriteLine("                              Se omitido, processa todos os repositórios");
        System.Console.WriteLine("  --github-token <token>      Token de acesso do GitHub");
        System.Console.WriteLine("  --openai-key <key>          API Key do OpenAI (ChatGPT)");
        System.Console.WriteLine("  -m, --model <model>         Modelo do ChatGPT (padrão: gpt-4)");
        System.Console.WriteLine("  -d, --output <dir>          Diretório de saída (padrão: ./output)");
        System.Console.WriteLine("  --max-commits <n>           Máximo de commits por repositório (padrão: 30)");
        System.Console.WriteLine("  --include-forks             Incluir repositórios forkados");
        System.Console.WriteLine("  -h, --help                  Exibir esta ajuda");
        System.Console.WriteLine();
        System.Console.WriteLine("Variáveis de ambiente:");
        System.Console.WriteLine("  GITNEWS_GITHUB__TOKEN       Token de acesso do GitHub");
        System.Console.WriteLine("  GITNEWS_GITHUB__OWNER       Owner da conta");
        System.Console.WriteLine("  GITNEWS_GITHUB__REPOSITORY  Repositório específico (opcional)");
        System.Console.WriteLine("  GITNEWS_OPENAI__APIKEY      API Key do OpenAI");
        System.Console.WriteLine("  GITNEWS_OPENAI__MODEL       Modelo do ChatGPT");
        System.Console.WriteLine("  GITNEWS_OUTPUT__OUTPUTDIRECTORY  Diretório de saída");
        System.Console.WriteLine();
        System.Console.WriteLine("Exemplos:");
        System.Console.WriteLine("  # Processar todos os repositórios de uma conta:");
        System.Console.WriteLine("  GitNews.Console --owner microsoft --github-token ghp_xxx --openai-key sk-xxx");
        System.Console.WriteLine();
        System.Console.WriteLine("  # Processar um repositório específico:");
        System.Console.WriteLine("  GitNews.Console --owner microsoft --repo vscode --github-token ghp_xxx --openai-key sk-xxx");
    }
}

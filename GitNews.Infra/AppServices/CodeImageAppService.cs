using System.Diagnostics;
using System.Text.RegularExpressions;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;

namespace GitNews.Infra.AppServices;

public class CodeImageAppService : ICodeImageAppService
{
    private readonly ILogger<CodeImageAppService> _logger;
    private const string GermaniumProject = "c:/repos/germanium/src/Germanium/Germanium.csproj";

    private static readonly string DefaultCode = @"using System;

namespace GitNews;

public class Article
{
    public string Title { get; set; }
    public string Content { get; set; }
    public string Category { get; set; }
    public DateTime CreatedAt { get; set; }
}";

    public CodeImageAppService(ILogger<CodeImageAppService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GenerateImageFromMarkdownAsync(string markdownContent)
    {
        var (code, language) = ExtractCodeBlock(markdownContent);

        var extension = GetFileExtension(language);
        var tempDir = Path.Combine(Path.GetTempPath(), "gitnews-germanium");
        Directory.CreateDirectory(tempDir);

        var inputFile = Path.Combine(tempDir, $"code{extension}");
        var outputFile = Path.Combine(tempDir, "cover.png");

        try
        {
            await File.WriteAllTextAsync(inputFile, code);

            var args = $"run --project \"{GermaniumProject}\" -- " +
                       $"\"{inputFile}\" " +
                       $"-o \"{outputFile}\" " +
                       $"-w 800 --height 450 " +
                       $"-t dracula " +
                       $"--padding 32";

            _logger.LogInformation("Generating code image with Germanium ({Language})", language);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Germanium failed (exit code {process.ExitCode}): {stderr}");

            if (!File.Exists(outputFile))
                throw new InvalidOperationException("Germanium did not produce an output file.");

            var imageBytes = await File.ReadAllBytesAsync(outputFile);
            _logger.LogInformation("Code image generated: {Size} bytes", imageBytes.Length);

            return imageBytes;
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    private static (string Code, string Language) ExtractCodeBlock(string markdown)
    {
        // Try to find ```csharp or ```cs first
        var csharpMatch = Regex.Match(markdown, @"```(?:csharp|cs)\s*\n(.*?)```", RegexOptions.Singleline);
        if (csharpMatch.Success)
            return (csharpMatch.Groups[1].Value.Trim(), "csharp");

        // Try any other code block
        var anyMatch = Regex.Match(markdown, @"```(\w*)\s*\n(.*?)```", RegexOptions.Singleline);
        if (anyMatch.Success)
        {
            var lang = string.IsNullOrWhiteSpace(anyMatch.Groups[1].Value) ? "generic" : anyMatch.Groups[1].Value;
            return (anyMatch.Groups[2].Value.Trim(), lang);
        }

        // No code block found — use default code
        return (DefaultCode, "csharp");
    }

    private static string GetFileExtension(string language) => language.ToLowerInvariant() switch
    {
        "csharp" or "cs" => ".cs",
        "javascript" or "js" => ".js",
        "typescript" or "ts" => ".ts",
        "python" or "py" => ".py",
        "rust" or "rs" => ".rs",
        "go" => ".go",
        "java" => ".java",
        "cpp" or "c++" or "c" => ".cpp",
        _ => ".txt",
    };
}

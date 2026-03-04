namespace GitNews.Core.Models;

public class GitNewsSettings
{
    public GitHubSettings GitHub { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
}

public class GitHubSettings
{
    public string Token { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public int MaxCommits { get; set; } = 30;
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

public class OutputSettings
{
    public string OutputDirectory { get; set; } = "./output";
}

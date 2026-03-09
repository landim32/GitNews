namespace GitNews.DTO;

public class GitNewsSettings
{
    public GitHubSettings GitHub { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public NNewsSettings NNews { get; set; } = new();
}

public class GitHubSettings
{
    public string Token { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public int MaxCommits { get; set; } = 30;
    /// <summary>
    /// Se true, inclui repositórios forkados. Padrão: false.
    /// </summary>
    public bool IncludeForks { get; set; } = false;
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

public class DatabaseSettings
{
    public string Provider { get; set; } = "PostgreSQL";
    public string ConnectionString { get; set; } = string.Empty;
}

public class NNewsSettings
{
    public string ApiUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class WorkerSettings
{
    /// <summary>
    /// Time of day to run the worker (HH:mm format). Default: 19:00.
    /// </summary>
    public string ScheduleTime { get; set; } = "19:00";
}

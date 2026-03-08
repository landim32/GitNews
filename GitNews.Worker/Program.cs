using GitNews.Application;
using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using GitNews.Worker;

var builder = Host.CreateApplicationBuilder(args);

var settings = new GitNewsSettings();
builder.Configuration.Bind(settings);

builder.Services.ConfigureServices(opt =>
{
    opt.GitHub = settings.GitHub;
    opt.OpenAI = settings.OpenAI;
    opt.Database = settings.Database;
});
builder.Services.AddSingleton<IUserInteractionService, NonInteractiveUserInteractionService>();

builder.Services.Configure<WorkerSettings>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<GitNewsSettings>(builder.Configuration.Bind);

builder.Services.AddHostedService<GitNewsWorker>();

var host = builder.Build();
host.Run();

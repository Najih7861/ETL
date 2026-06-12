using EtlTool.Api.Interfaces;
using EtlTool.Api.Models;
using EtlTool.Api.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Run from the repo root so relative paths (storage/, config/, logs/) resolve correctly,
// no matter where the API is launched from.
Directory.SetCurrentDirectory(FindRepoRoot(builder.Environment.ContentRootPath));

// Logging: Serilog to console + rolling daily file under logs/.
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/etl-api-.log", rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

// Services.
builder.Services.Configure<EtlOptions>(builder.Configuration.GetSection(EtlOptions.SectionName));
builder.Services.AddScoped<IEtlService, EtlService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "EtlTool API", Version = "v1", Description = "File-based CSV→CSV ETL." }));

var app = builder.Build();

// HTTP pipeline.
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "EtlTool API v1"));
app.UseSerilogRequestLogging();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

// Walks up from the content root until the solution file is found (the repo root).
static string FindRepoRoot(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
            return dir.FullName;
        dir = dir.Parent;
    }
    return startDir;
}

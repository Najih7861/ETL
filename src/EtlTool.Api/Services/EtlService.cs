using EtlTool.Api.Interfaces;
using EtlTool.Api.Models;
using EtlTool.Core.Models;
using EtlTool.Core.Pipeline;
using Microsoft.Extensions.Options;

namespace EtlTool.Api.Services;

/// <summary>
/// Thin wrapper over the <c>EtlTool.Core</c> engine. Uses the folders/config from
/// <see cref="EtlOptions"/> (appsettings.json) — there is no per-request input.
/// </summary>
public sealed class EtlService : IEtlService
{
    private readonly ILogger<EtlService> _logger;
    private readonly EtlOptions _options;

    public EtlService(ILogger<EtlService> logger, IOptions<EtlOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public BatchEtlResult RunEtl()
    {
        var errorPolicy = _options.OnError.Equals("fail", StringComparison.OrdinalIgnoreCase)
            ? ErrorPolicy.Fail
            : ErrorPolicy.Skip;

        _logger.LogInformation("API ETL run: input={Input} output={Output} invalid={Invalid} config={Config} onError={Policy}",
            _options.InputDir, _options.OutputDir, _options.InvalidDir, _options.ConfigPath, errorPolicy);

        var folderProcessor = new FolderEtlProcessor(_logger);
        return folderProcessor.ProcessFolder(
            _options.InputDir, _options.OutputDir, _options.InvalidDir, _options.ConfigPath, errorPolicy);
    }

    public IReadOnlyList<string> ListInputFiles()
    {
        if (!Directory.Exists(_options.InputDir))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(_options.InputDir, "*.csv")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
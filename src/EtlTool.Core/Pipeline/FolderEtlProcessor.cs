using System.Diagnostics;
using EtlTool.Core.Mapping;
using EtlTool.Core.Models;
using Microsoft.Extensions.Logging;

namespace EtlTool.Core.Pipeline;

/// <summary>
/// Scans an input folder for every *.csv file and runs the ETL pipeline over each one,
/// writing &lt;name&gt;.out.csv into the output folder. Aggregates a <see cref="BatchEtlResult"/>.
/// </summary>
public sealed class FolderEtlProcessor
{
    private readonly ILogger _logger;

    public FolderEtlProcessor(ILogger logger) => _logger = logger;

    public BatchEtlResult ProcessFolder(
        string inputDir, string outputDir, string invalidDir, string mappingFilePath, ErrorPolicy errorPolicy)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchResult = new BatchEtlResult();

        var mapping = EtlMappingLoader.LoadFromFile(mappingFilePath);
        _logger.LogInformation("Loaded mapping config: {Config} ({Columns} columns, {Filters} filters)",
            mappingFilePath, mapping.Columns.Count, mapping.Filters.Count);

        if (!Directory.Exists(inputDir))
            throw new DirectoryNotFoundException($"Input folder not found: {inputDir}");

        Directory.CreateDirectory(outputDir);

        var inputFiles = Directory.EnumerateFiles(inputDir, "*.csv")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation("Discovered {Count} CSV file(s) in {Dir}", inputFiles.Count, inputDir);
        if (inputFiles.Count == 0)
        {
            _logger.LogWarning("No CSV files found in {Dir} — nothing to do.", inputDir);
            stopwatch.Stop();
            batchResult.TotalElapsedMs = stopwatch.ElapsedMilliseconds;
            return batchResult;
        }

        var fileProcessor = new FileEtlProcessor(_logger);
        foreach (var inputFile in inputFiles)
        {
            _logger.LogInformation("Processing {File}", Path.GetFileName(inputFile));

            var fileResult = fileProcessor.ProcessFile(inputFile, outputDir, invalidDir, mapping, errorPolicy);
            batchResult.Files.Add(fileResult);

            var outputNames = string.Join(", ", fileResult.OutputFiles.Select(Path.GetFileName));
            _logger.LogInformation(
                "  Done {File}: read={Read} written={Written} skipped={Skipped} invalid={Invalid} in {Ms}ms -> [{Outputs}]",
                Path.GetFileName(inputFile), fileResult.RowsRead, fileResult.RowsWritten,
                fileResult.RowsSkipped, fileResult.RowsInvalid, fileResult.ElapsedMs, outputNames);

            if (fileResult.Failed && errorPolicy == ErrorPolicy.Fail)
            {
                _logger.LogError("Stopping batch after failure (on-error=fail).");
                break;
            }
        }

        stopwatch.Stop();
        batchResult.TotalElapsedMs = stopwatch.ElapsedMilliseconds;
        return batchResult;
    }
}

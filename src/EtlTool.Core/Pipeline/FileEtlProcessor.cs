using System.Diagnostics;
using EtlTool.Core.Extract;
using EtlTool.Core.Load;
using EtlTool.Core.Mapping;
using EtlTool.Core.Models;
using EtlTool.Core.Transform;
using Microsoft.Extensions.Logging;

namespace EtlTool.Core.Pipeline;

/// <summary>Runs the Extract → Transform → Load pipeline for a single CSV file.</summary>
public sealed class FileEtlProcessor
{
    private readonly ILogger _logger;
    private readonly CsvExtractor _extractor = new();

    public FileEtlProcessor(ILogger logger) => _logger = logger;

    public FileEtlResult ProcessFile(
        string inputFilePath, string outputDir, string invalidDir, EtlMapping mapping, ErrorPolicy errorPolicy)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new FileEtlResult { InputFile = inputFilePath };
        var fileName = Path.GetFileName(inputFilePath);
        var baseName = Path.GetFileNameWithoutExtension(inputFilePath);
        var transformer = new RowTransformer(mapping);

        // One open writer per output split (e.g. _male and _female) + a lazy invalid-records writer.
        var splits = OpenOutputSplits(mapping, outputDir, baseName, result);
        var invalidPath = Path.Combine(invalidDir, baseName + "_invalid.csv");
        var invalidWriter = new InvalidRecordWriter(invalidPath, mapping);

        try
        {
            foreach (var inputRow in _extractor.ExtractRows(inputFilePath, mapping.Source))
            {
                result.RowsRead++;

                if (!transformer.RowPassesFilters(inputRow))
                {
                    result.RowsSkipped++;   // intentionally excluded by a global filter
                    continue;
                }

                CsvRow outputRow;
                try
                {
                    outputRow = transformer.TransformRow(inputRow);
                }
                catch (Exception ex) when (ex is not RowProcessingException)
                {
                    RecordInvalid(invalidWriter, inputRow, ex.Message, fileName, result);
                    if (errorPolicy == ErrorPolicy.Fail)
                        throw new RowProcessingException($"row {result.RowsRead}: {ex.Message}", ex);
                    continue;
                }

                // Route the row to every split whose filter it matches.
                var wroteAnywhere = false;
                foreach (var split in splits)
                {
                    if (split.Filter is null || RowTransformer.RowMatchesFilter(inputRow, split.Filter))
                    {
                        split.Loader.WriteRow(outputRow);
                        wroteAnywhere = true;
                    }
                }

                if (wroteAnywhere)
                    result.RowsWritten++;
                else
                    RecordInvalid(invalidWriter, inputRow, "no matching output split", fileName, result);
            }
        }
        catch (RowProcessingException ex)
        {
            result.Failed = true;
            result.Error = ex.Message;
            _logger.LogError("  [{File}] aborted (on-error=fail): {Message}", fileName, ex.Message);
        }
        catch (Exception ex)
        {
            result.Failed = true;
            result.Error = ex.Message;
            _logger.LogError(ex, "  [{File}] failed: {Message}", fileName, ex.Message);
        }
        finally
        {
            foreach (var split in splits)
                split.Loader.Dispose();
            invalidWriter.Dispose();
        }

        if (invalidWriter.HasInvalidRows)
            result.InvalidFile = invalidPath;

        stopwatch.Stop();
        result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    /// <summary>Writes a row to the invalid file, logs it, and bumps the invalid counter.</summary>
    private void RecordInvalid(InvalidRecordWriter invalidWriter, CsvRow inputRow, string reason, string fileName, FileEtlResult result)
    {
        result.RowsInvalid++;
        var message = $"row {result.RowsRead}: {reason}";
        result.RowErrors.Add(message);
        invalidWriter.Write(inputRow, reason);
        _logger.LogWarning("  [{File}] invalid {Message}", fileName, message);
    }

    /// <summary>
    /// Creates and opens the output writers. With no splits configured, writes a single
    /// "&lt;name&gt;.out.csv" that takes every row; otherwise one "&lt;name&gt;_&lt;suffix&gt;.csv" per split.
    /// </summary>
    private static List<OpenSplit> OpenOutputSplits(EtlMapping mapping, string outputDir, string baseName, FileEtlResult result)
    {
        var splits = new List<OpenSplit>();

        if (mapping.Outputs.Count == 0)
        {
            var path = Path.Combine(outputDir, baseName + ".out.csv");
            result.OutputFiles.Add(path);
            splits.Add(new OpenSplit(Filter: null, new CsvLoader(path, mapping)));
            return splits;
        }

        foreach (var output in mapping.Outputs)
        {
            var path = Path.Combine(outputDir, $"{baseName}_{output.Suffix}.csv");
            result.OutputFiles.Add(path);
            splits.Add(new OpenSplit(output.Filter, new CsvLoader(path, mapping)));
        }
        return splits;
    }

    /// <summary>An output file's filter paired with its open writer.</summary>
    private sealed record OpenSplit(RowFilter? Filter, CsvLoader Loader);
}

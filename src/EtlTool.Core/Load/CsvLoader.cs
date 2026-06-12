using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using EtlTool.Core.Mapping;
using EtlTool.Core.Models;

namespace EtlTool.Core.Load;

/// <summary>
/// LOAD stage: a stateful, streaming CSV writer. Open it, write rows one at a time with
/// <see cref="WriteRow"/>, then dispose. Output column set and order come from the mapping's
/// target columns.
/// </summary>
public sealed class CsvLoader : IDisposable
{
    private readonly StreamWriter _streamWriter;
    private readonly CsvWriter _csvWriter;
    private readonly string[] _outputColumns;

    public CsvLoader(string outputFilePath, EtlMapping mapping)
    {
        var encoding = ResolveEncoding(mapping.Target.Encoding);
        var csvWriterConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = mapping.Target.Delimiter,
        };

        _outputColumns = mapping.Columns.Select(c => c.Target).ToArray();
        _streamWriter = new StreamWriter(outputFilePath, append: false, encoding);
        _csvWriter = new CsvWriter(_streamWriter, csvWriterConfig);

        if (mapping.Target.WriteHeader)
            WriteHeaderRow();
    }

    private void WriteHeaderRow()
    {
        foreach (var columnName in _outputColumns)
            _csvWriter.WriteField(columnName);
        _csvWriter.NextRecord();
    }

    public void WriteRow(CsvRow row)
    {
        foreach (var columnName in _outputColumns)
            _csvWriter.WriteField(FormatCell(row[columnName]));
        _csvWriter.NextRecord();
    }

    // Write UTF-8 without a byte-order mark so downstream CSV readers don't see a stray BOM.
    private static Encoding ResolveEncoding(string encodingName)
    {
        var normalized = encodingName.Replace("-", "").ToLowerInvariant();
        return normalized == "utf8"
            ? new UTF8Encoding(false)
            : Encoding.GetEncoding(encodingName);
    }

    private static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    public void Dispose()
    {
        _csvWriter.Dispose();
        _streamWriter.Dispose();
    }
}

using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using EtlTool.Core.Mapping;
using EtlTool.Core.Models;

namespace EtlTool.Core.Load;

/// <summary>
/// Writes rows that could not be processed to an "invalid" CSV — the original input columns
/// plus an InvalidReason column, so they can be fixed and re-run. The file is created lazily
/// (only when the first invalid row appears), so clean inputs produce no invalid file.
/// </summary>
public sealed class InvalidRecordWriter : IDisposable
{
    private const string ReasonColumn = "InvalidReason";

    private readonly string _invalidFilePath;
    private readonly EtlMapping _mapping;

    private StreamWriter? _streamWriter;
    private CsvWriter? _csvWriter;
    private string[]? _columns;

    public InvalidRecordWriter(string invalidFilePath, EtlMapping mapping)
    {
        _invalidFilePath = invalidFilePath;
        _mapping = mapping;
    }

    /// <summary>True once at least one invalid row has been written (the file now exists).</summary>
    public bool HasInvalidRows { get; private set; }

    public void Write(CsvRow inputRow, string reason)
    {
        if (_csvWriter is null)
            OpenAndWriteHeader(inputRow);

        foreach (var column in _columns!)
            _csvWriter!.WriteField(FormatCell(inputRow[column]));
        _csvWriter!.WriteField(reason);
        _csvWriter.NextRecord();

        HasInvalidRows = true;
    }

    private void OpenAndWriteHeader(CsvRow firstRow)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_invalidFilePath)!);
        _columns = firstRow.Cells.Keys.ToArray();

        var encoding = ResolveEncoding(_mapping.Target.Encoding);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = _mapping.Target.Delimiter };
        _streamWriter = new StreamWriter(_invalidFilePath, append: false, encoding);
        _csvWriter = new CsvWriter(_streamWriter, config);

        foreach (var column in _columns)
            _csvWriter.WriteField(column);
        _csvWriter.WriteField(ReasonColumn);
        _csvWriter.NextRecord();
    }

    // UTF-8 without a BOM, matching the normal output files.
    private static Encoding ResolveEncoding(string name)
    {
        var normalized = name.Replace("-", "").ToLowerInvariant();
        return normalized == "utf8" ? new UTF8Encoding(false) : Encoding.GetEncoding(name);
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
        _csvWriter?.Dispose();
        _streamWriter?.Dispose();
    }
}

namespace EtlTool.Core.Models;

/// <summary>Outcome of processing a single input CSV file.</summary>
public sealed class FileEtlResult
{
    public string InputFile { get; init; } = "";

    /// <summary>One or more output files produced from this input (one per output split).</summary>
    public List<string> OutputFiles { get; } = new();

    /// <summary>The invalid-records file, if any invalid rows were written.</summary>
    public string? InvalidFile { get; set; }

    public long RowsRead { get; set; }
    public long RowsWritten { get; set; }

    /// <summary>Rows intentionally excluded by a global filter.</summary>
    public long RowsSkipped { get; set; }

    /// <summary>Rows that could not be processed (transform error or no matching output split).</summary>
    public long RowsInvalid { get; set; }

    public long ElapsedMs { get; set; }
    public bool Failed { get; set; }
    public string? Error { get; set; }

    /// <summary>Human-readable messages for rows that failed to transform.</summary>
    public List<string> RowErrors { get; } = new();
}

/// <summary>Aggregate outcome of a whole-folder batch run.</summary>
public sealed class BatchEtlResult
{
    public List<FileEtlResult> Files { get; } = new();
    public long TotalElapsedMs { get; set; }

    public int FilesProcessed => Files.Count;
    public int FilesFailed => Files.Count(f => f.Failed);
    public long TotalRowsRead => Files.Sum(f => f.RowsRead);
    public long TotalRowsWritten => Files.Sum(f => f.RowsWritten);
    public long TotalRowsSkipped => Files.Sum(f => f.RowsSkipped);
    public long TotalRowsInvalid => Files.Sum(f => f.RowsInvalid);
}

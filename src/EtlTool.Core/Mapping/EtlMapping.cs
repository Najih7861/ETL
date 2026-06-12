namespace EtlTool.Core.Mapping;

/// <summary>Root mapping/conversion definition, deserialized from mapping.json.</summary>
public sealed class EtlMapping
{
    public SourceFormat Source { get; set; } = new();
    public TargetFormat Target { get; set; } = new();

    /// <summary>Global row filters applied to every output (e.g. keep only active customers).</summary>
    public List<RowFilter> Filters { get; set; } = new();

    public List<ColumnMapping> Columns { get; set; } = new();

    /// <summary>
    /// Optional output splits. Each one writes a separate file containing only the rows that
    /// match its filter (e.g. one file for male, one for female). When empty, a single
    /// "&lt;name&gt;.out.csv" is written with every row.
    /// </summary>
    public List<OutputSplit> Outputs { get; set; } = new();
}

/// <summary>How to read the input CSV.</summary>
public sealed class SourceFormat
{
    public string Delimiter { get; set; } = ",";
    public bool HasHeader { get; set; } = true;
    public string Encoding { get; set; } = "utf-8";

    /// <summary>
    /// Remove extra spaces from EVERY field as it is read: trims the ends and collapses
    /// internal whitespace runs to a single space. Applies to all columns.
    /// </summary>
    public bool NormalizeWhitespace { get; set; } = false;
}

/// <summary>How to write the output CSV.</summary>
public sealed class TargetFormat
{
    public string Delimiter { get; set; } = ",";
    public string Encoding { get; set; } = "utf-8";
    public bool WriteHeader { get; set; } = true;
}

/// <summary>A row-level include/exclude rule.</summary>
public sealed class RowFilter
{
    public string Column { get; set; } = "";
    /// <summary>equals | notEquals | contains | nonEmpty</summary>
    public string Op { get; set; } = "equals";
    public string? Value { get; set; }
}

/// <summary>One output file plus the filter that decides which rows land in it.</summary>
public sealed class OutputSplit
{
    /// <summary>Appended to the output file name, e.g. "male" =&gt; customers_male.csv.</summary>
    public string Suffix { get; set; } = "";

    /// <summary>Rows matching this filter are written to this file. Null =&gt; all rows.</summary>
    public RowFilter? Filter { get; set; }
}

/// <summary>A single output column: where it comes from and how it is converted.</summary>
public sealed class ColumnMapping
{
    /// <summary>Single source column name. Null/absent =&gt; use <see cref="Sources"/> or <see cref="Default"/>.</summary>
    public string? Source { get; set; }

    /// <summary>Multiple source columns combined into one (e.g. ["firstname","lastname"]).</summary>
    public List<string>? Sources { get; set; }

    /// <summary>Joiner used between <see cref="Sources"/> values. Defaults to a single space.</summary>
    public string? Separator { get; set; }

    /// <summary>Output column name (required).</summary>
    public string Target { get; set; } = "";

    /// <summary>int | decimal | string | date | bool. Null =&gt; passthrough as text.</summary>
    public string? Type { get; set; }

    /// <summary>Built-in transform name: trim | upper | lower.</summary>
    public string? Transform { get; set; }

    /// <summary>Value used when the source is missing/empty, or for a constant column.</summary>
    public string? Default { get; set; }

    /// <summary>For type=date: how to parse the input value (e.g. "MM/dd/yyyy").</summary>
    public string? SourceFormat { get; set; }

    /// <summary>For type=date: how to render the output value (e.g. "yyyy-MM-dd").</summary>
    public string? Format { get; set; }
}

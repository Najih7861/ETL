namespace EtlTool.Core.Models;

/// <summary>
/// A single CSV record: named cells keyed by column name. Values are strings when first
/// extracted and may become typed values (long, decimal, …) after transformation.
/// Column lookups are case-sensitive (exact header match).
/// </summary>
public sealed class CsvRow
{
    private readonly Dictionary<string, object?> _cells;

    public CsvRow() => _cells = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Gets or sets a cell by column name. Reading a missing column returns null.</summary>
    public object? this[string columnName]
    {
        get => _cells.TryGetValue(columnName, out var value) ? value : null;
        set => _cells[columnName] = value;
    }

    public bool HasColumn(string columnName) => _cells.ContainsKey(columnName);

    public IReadOnlyDictionary<string, object?> Cells => _cells;
}

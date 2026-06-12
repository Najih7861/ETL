using EtlTool.Core.Mapping;
using EtlTool.Core.Models;

namespace EtlTool.Core.Transform;

/// <summary>
/// TRANSFORM stage: evaluates row filters and maps an input row to an output row
/// (rename/select/reorder + combine + transform + type conversion + defaults).
/// </summary>
public sealed class RowTransformer
{
    private readonly EtlMapping _mapping;

    public RowTransformer(EtlMapping mapping) => _mapping = mapping;

    /// <summary>Returns true if the row passes every global filter.</summary>
    public bool RowPassesFilters(CsvRow row)
        => _mapping.Filters.All(filter => RowMatchesFilter(row, filter));

    /// <summary>Evaluates a single filter against a row (used for global filters and output splits).</summary>
    public static bool RowMatchesFilter(CsvRow row, RowFilter filter)
    {
        var cellValue = row[filter.Column]?.ToString();
        return filter.Op.ToLowerInvariant() switch
        {
            "equals" => string.Equals(cellValue, filter.Value, StringComparison.Ordinal),
            "notequals" => !string.Equals(cellValue, filter.Value, StringComparison.Ordinal),
            "contains" => cellValue is not null && filter.Value is not null &&
                          cellValue.Contains(filter.Value, StringComparison.Ordinal),
            "nonempty" => !string.IsNullOrWhiteSpace(cellValue),
            _ => throw new InvalidOperationException($"Unknown filter op '{filter.Op}'."),
        };
    }

    /// <summary>Builds the output row from the input row. Throws if a value fails type conversion or validation.</summary>
    public CsvRow TransformRow(CsvRow inputRow)
    {
        var outputRow = new CsvRow();
        foreach (var column in _mapping.Columns)
        {
            var value = ResolveSourceValue(inputRow, column);

            var transform = TransformRegistry.ResolveTransform(column.Transform);
            if (transform is not null)
                value = transform.Apply(value);

            value = ValueTypeConverter.ConvertToType(value, column);

            ValueValidator.Validate(value, column);

            outputRow[column.Target] = value;
        }
        return outputRow;
    }

    /// <summary>
    /// Works out a column's value: combine multiple sources, read a single source (with a
    /// default fallback when missing/empty), or use a constant default.
    /// </summary>
    private static object? ResolveSourceValue(CsvRow inputRow, ColumnMapping column)
    {
        // Combine several columns into one, e.g. firstname + " " + lastname => Fullname.
        if (column.Sources is { Count: > 0 })
        {
            var separator = column.Separator ?? " ";
            var parts = column.Sources.Select(name => inputRow[name]?.ToString() ?? string.Empty);
            return string.Join(separator, parts);
        }

        if (column.Source is null)
            return column.Default;

        var value = inputRow[column.Source];
        var isMissingOrEmpty = value is null || (value is string s && s.Length == 0);
        return isMissingOrEmpty && column.Default is not null ? column.Default : value;
    }
}

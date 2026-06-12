using System.Globalization;
using EtlTool.Core.Mapping;

namespace EtlTool.Core.Transform;

/// <summary>Converts a cell's value into the target type declared in the mapping.</summary>
public static class ValueTypeConverter
{
    public static object? ConvertToType(object? value, ColumnMapping column)
    {
        if (string.IsNullOrWhiteSpace(column.Type))
            return value;

        var text = value?.ToString();
        // Empty input stays empty rather than throwing on conversion.
        if (string.IsNullOrEmpty(text))
            return value;

        var culture = CultureInfo.InvariantCulture;
        switch (column.Type.ToLowerInvariant())
        {
            case "string":
                return text;

            case "int":
                return long.Parse(text, NumberStyles.Integer, culture);

            case "decimal":
                return decimal.Parse(text, NumberStyles.Number, culture);

            case "bool":
                return ParseBoolean(text);

            case "date":
                var parsedDate = column.SourceFormat is not null
                    ? DateTime.ParseExact(text, column.SourceFormat, culture, DateTimeStyles.None)
                    : DateTime.Parse(text, culture, DateTimeStyles.None);
                // Render to the requested output format (string), or ISO-8601 by default.
                return parsedDate.ToString(column.Format ?? "o", culture);

            default:
                throw new InvalidOperationException($"Unknown type '{column.Type}'.");
        }
    }

    private static bool ParseBoolean(string text)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "true": case "1": case "yes": case "y": return true;
            case "false": case "0": case "no": case "n": return false;
            default: throw new FormatException($"'{text}' is not a recognized boolean.");
        }
    }
}

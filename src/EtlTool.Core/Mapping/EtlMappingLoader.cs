using System.Text.Json;
using System.Text.RegularExpressions;

namespace EtlTool.Core.Mapping;

/// <summary>Loads and validates an <see cref="EtlMapping"/> from a JSON file.</summary>
public static class EtlMappingLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly HashSet<string> KnownFilterOps =
        new(StringComparer.OrdinalIgnoreCase) { "equals", "notEquals", "contains", "nonEmpty" };

    private static readonly HashSet<string> KnownColumnTypes =
        new(StringComparer.OrdinalIgnoreCase) { "int", "decimal", "string", "date", "bool" };

    public static EtlMapping LoadFromFile(string mappingFilePath)
    {
        if (!File.Exists(mappingFilePath))
            throw new FileNotFoundException($"Mapping config not found: {mappingFilePath}", mappingFilePath);

        var json = File.ReadAllText(mappingFilePath);
        EtlMapping? mapping;
        try
        {
            mapping = JsonSerializer.Deserialize<EtlMapping>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Mapping config is not valid JSON: {ex.Message}", ex);
        }

        if (mapping is null)
            throw new InvalidOperationException($"Mapping config deserialized to null: {mappingFilePath}");

        ValidateMapping(mapping);
        return mapping;
    }

    public static void ValidateMapping(EtlMapping mapping)
    {
        if (mapping.Columns is null || mapping.Columns.Count == 0)
            throw new InvalidOperationException("Mapping config must define at least one column under 'columns'.");

        foreach (var column in mapping.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.Target))
                throw new InvalidOperationException("Every column must define a non-empty 'target'.");

            var hasSources = column.Sources is { Count: > 0 };
            if (column.Source is null && !hasSources && column.Default is null)
                throw new InvalidOperationException(
                    $"Column '{column.Target}' has no 'source', 'sources', or 'default' value.");

            if (column.Type is not null && !KnownColumnTypes.Contains(column.Type))
                throw new InvalidOperationException(
                    $"Column '{column.Target}' uses unknown type '{column.Type}'. Allowed: {string.Join(", ", KnownColumnTypes)}.");

            if (column.Validation is not null)
                ValidateColumnValidation(column.Target, column.Validation);
        }

        foreach (var filter in mapping.Filters ?? new List<RowFilter>())
            ValidateFilter(filter);

        foreach (var output in mapping.Outputs ?? new List<OutputSplit>())
        {
            if (string.IsNullOrWhiteSpace(output.Suffix))
                throw new InvalidOperationException("Every output split must define a non-empty 'suffix'.");
            if (output.Filter is not null)
                ValidateFilter(output.Filter);
        }
    }

    private static void ValidateColumnValidation(string target, ColumnValidation rules)
    {
        if (rules.Pattern is not null)
        {
            try { _ = new Regex(rules.Pattern); }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"Column '{target}' has an invalid validation pattern '{rules.Pattern}': {ex.Message}", ex);
            }
        }

        if (rules.MinLength is { } minLen && minLen < 0)
            throw new InvalidOperationException($"Column '{target}' has a negative 'minLength'.");
        if (rules.MaxLength is { } maxLen && maxLen < 0)
            throw new InvalidOperationException($"Column '{target}' has a negative 'maxLength'.");
        if (rules.MinLength is { } min && rules.MaxLength is { } max && min > max)
            throw new InvalidOperationException(
                $"Column '{target}' has 'minLength' ({min}) greater than 'maxLength' ({max}).");

        if (rules.Min is { } minVal && rules.Max is { } maxVal && minVal > maxVal)
            throw new InvalidOperationException(
                $"Column '{target}' has 'min' ({minVal}) greater than 'max' ({maxVal}).");

        if (rules.AllowedValues is { Count: 0 })
            throw new InvalidOperationException($"Column '{target}' has an empty 'allowedValues' list.");
    }

    private static void ValidateFilter(RowFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Column))
            throw new InvalidOperationException("Every filter must define a 'column'.");
        if (!KnownFilterOps.Contains(filter.Op))
            throw new InvalidOperationException(
                $"Filter on '{filter.Column}' uses unknown op '{filter.Op}'. Allowed: {string.Join(", ", KnownFilterOps)}.");
    }
}

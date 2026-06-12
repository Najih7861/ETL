using System.Text.RegularExpressions;

namespace EtlTool.Core.Transform;

/// <summary>Removes leading/trailing whitespace.</summary>
public sealed class TrimTransform : IValueTransform
{
    public string Name => "trim";
    public object? Apply(object? value) => value?.ToString()?.Trim();
}

/// <summary>
/// Removes extra spaces: trims the ends and collapses any run of whitespace into a single
/// space (e.g. "Alice   Johnson" =&gt; "Alice Johnson"). Useful for combined name fields.
/// </summary>
public sealed partial class NormalizeSpacesTransform : IValueTransform
{
    public string Name => "normalizeSpaces";

    public object? Apply(object? value)
    {
        var text = value?.ToString();
        return text is null ? null : WhitespaceRuns().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();
}

/// <summary>Upper-cases the value (invariant culture).</summary>
public sealed class UpperCaseTransform : IValueTransform
{
    public string Name => "upper";
    public object? Apply(object? value) => value?.ToString()?.ToUpperInvariant();
}

/// <summary>Lower-cases the value (invariant culture).</summary>
public sealed class LowerCaseTransform : IValueTransform
{
    public string Name => "lower";
    public object? Apply(object? value) => value?.ToString()?.ToLowerInvariant();
}

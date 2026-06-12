using System.Text.RegularExpressions;

namespace EtlTool.Core.Transform;

/// <summary>Shared whitespace cleanup used by the extractor and the normalizeSpaces transform.</summary>
public static partial class Whitespace
{
    /// <summary>Trims the ends and collapses any run of whitespace into a single space.</summary>
    public static string? Normalize(object? value)
    {
        var text = value?.ToString();
        return text is null ? null : WhitespaceRuns().Replace(text, " ").Trim();
    }

    private static Regex WhitespaceRuns() => new Regex(@"\s+", RegexOptions.Compiled);
}

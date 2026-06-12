namespace EtlTool.Core.Transform;

/// <summary>Resolves a built-in transform by the name used in the mapping config.</summary>
public static class TransformRegistry
{
    private static readonly IReadOnlyDictionary<string, IValueTransform> TransformsByName =
        new IValueTransform[]
        {
            new TrimTransform(),
            new NormalizeSpacesTransform(),
            new UpperCaseTransform(),
            new LowerCaseTransform(),
        }.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the transform for <paramref name="transformName"/>, or null when none is requested.</summary>
    public static IValueTransform? ResolveTransform(string? transformName)
    {
        if (string.IsNullOrWhiteSpace(transformName))
            return null;
        if (TransformsByName.TryGetValue(transformName, out var transform))
            return transform;
        throw new InvalidOperationException(
            $"Unknown transform '{transformName}'. Available: {string.Join(", ", TransformsByName.Keys)}.");
    }
}

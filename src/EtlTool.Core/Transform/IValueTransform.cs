namespace EtlTool.Core.Transform;

/// <summary>A named, reusable transform applied to a single cell value.</summary>
public interface IValueTransform
{
    /// <summary>Config name used to reference this transform (e.g. "trim").</summary>
    string Name { get; }

    object? Apply(object? value);
}

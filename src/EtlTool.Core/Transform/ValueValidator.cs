using System.Globalization;
using System.Text.RegularExpressions;
using EtlTool.Core.Mapping;

namespace EtlTool.Core.Transform;

/// <summary>
/// Validates a cell's converted value against the column's <see cref="ColumnValidation"/> rules.
/// Throws <see cref="ValueValidationException"/> on the first failure; the message becomes the
/// row's InvalidReason. Empty optional values pass (only <c>required</c> rejects empties),
/// mirroring <see cref="ValueTypeConverter"/>'s empty-passthrough behavior.
/// </summary>
public static class ValueValidator
{
    public static void Validate(object? value, ColumnMapping column)
    {
        var rules = column.Validation;
        if (rules is null)
            return;

        var target = column.Target;
        var text = value?.ToString();
        var isEmpty = string.IsNullOrEmpty(text);

        if (rules.Required && isEmpty)
            throw new ValueValidationException($"column '{target}' is required but was empty.");

        // An empty optional value has nothing further to check.
        if (isEmpty)
            return;

        if (rules.MinLength is { } minLen && text!.Length < minLen)
            throw new ValueValidationException(
                $"column '{target}' must be at least {minLen} characters but was {text.Length}.");

        if (rules.MaxLength is { } maxLen && text!.Length > maxLen)
            throw new ValueValidationException(
                $"column '{target}' must be at most {maxLen} characters but was {text.Length}.");

        if (rules.Pattern is { } pattern && !Regex.IsMatch(text!, pattern))
            throw new ValueValidationException(
                $"column '{target}' value '{text}' does not match pattern '{pattern}'.");

        if (rules.AllowedValues is { Count: > 0 } allowed && !allowed.Contains(text!, StringComparer.Ordinal))
            throw new ValueValidationException(
                $"column '{target}' value '{text}' is not one of the allowed values: {string.Join(", ", allowed)}.");

        if (rules.Min is not null || rules.Max is not null)
        {
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                throw new ValueValidationException(
                    $"column '{target}' min/max requires a numeric value but got '{text}'.");

            if (rules.Min is { } min && number < min)
                throw new ValueValidationException($"column '{target}' must be >= {min} but was {number}.");

            if (rules.Max is { } max && number > max)
                throw new ValueValidationException($"column '{target}' must be <= {max} but was {number}.");
        }
    }
}

/// <summary>Raised when a value fails a column's validation rules.</summary>
public sealed class ValueValidationException : Exception
{
    public ValueValidationException(string message) : base(message) { }
}

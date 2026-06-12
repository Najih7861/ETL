using EtlTool.Core.Mapping;
using EtlTool.Core.Transform;
using FluentAssertions;
using Xunit;

namespace EtlTool.Tests;

public class ValidatorTests
{
    [Fact]
    public void Validate_no_rules_passes()
    {
        var column = new ColumnMapping { Target = "X" };
        var act = () => ValueValidator.Validate("anything", column);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_required_empty_throws()
    {
        var column = new ColumnMapping { Target = "X", Validation = new() { Required = true } };
        var act = () => ValueValidator.Validate("", column);
        act.Should().Throw<ValueValidationException>().WithMessage("*required*");
    }

    [Fact]
    public void Validate_optional_empty_passes()
    {
        // Only 'required' rejects empties; an empty optional value skips the other checks.
        var column = new ColumnMapping { Target = "X", Validation = new() { MinLength = 3, Min = 5 } };
        var act = () => ValueValidator.Validate("", column);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("bad[at]example.com", false)]
    public void Validate_pattern_matches_or_throws(string value, bool valid)
    {
        var column = new ColumnMapping
        {
            Target = "Email",
            Validation = new() { Pattern = "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$" },
        };
        var act = () => ValueValidator.Validate(value, column);
        if (valid) act.Should().NotThrow();
        else act.Should().Throw<ValueValidationException>().WithMessage("*does not match pattern*");
    }

    [Theory]
    [InlineData("ab", true)]
    [InlineData("a", false)]
    public void Validate_minLength_enforced(string value, bool valid)
    {
        var column = new ColumnMapping { Target = "X", Validation = new() { MinLength = 2 } };
        var act = () => ValueValidator.Validate(value, column);
        if (valid) act.Should().NotThrow();
        else act.Should().Throw<ValueValidationException>().WithMessage("*at least 2*");
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("abcd", false)]
    public void Validate_maxLength_enforced(string value, bool valid)
    {
        var column = new ColumnMapping { Target = "X", Validation = new() { MaxLength = 3 } };
        var act = () => ValueValidator.Validate(value, column);
        if (valid) act.Should().NotThrow();
        else act.Should().Throw<ValueValidationException>().WithMessage("*at most 3*");
    }

    [Theory]
    [InlineData("male", true)]
    [InlineData("other", false)]
    public void Validate_allowedValues_enforced(string value, bool valid)
    {
        var column = new ColumnMapping
        {
            Target = "Gender",
            Validation = new() { AllowedValues = new() { "male", "female" } },
        };
        var act = () => ValueValidator.Validate(value, column);
        if (valid) act.Should().NotThrow();
        else act.Should().Throw<ValueValidationException>().WithMessage("*not one of the allowed values*");
    }

    [Theory]
    [InlineData(50L, true)]
    [InlineData(-1L, false)]
    [InlineData(200L, false)]
    public void Validate_min_max_numeric_enforced(long value, bool valid)
    {
        var column = new ColumnMapping { Target = "Age", Type = "int", Validation = new() { Min = 0, Max = 120 } };
        var act = () => ValueValidator.Validate(value, column);
        if (valid) act.Should().NotThrow();
        else act.Should().Throw<ValueValidationException>();
    }

    [Fact]
    public void Validate_min_max_on_non_numeric_throws()
    {
        var column = new ColumnMapping { Target = "X", Validation = new() { Min = 0 } };
        var act = () => ValueValidator.Validate("notanumber", column);
        act.Should().Throw<ValueValidationException>().WithMessage("*numeric*");
    }
}

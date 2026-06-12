using EtlTool.Core.Mapping;
using EtlTool.Core.Transform;
using FluentAssertions;
using Xunit;

namespace EtlTool.Tests;

public class TransformTests
{
    [Theory]
    [InlineData("trim", "  hi  ", "hi")]
    [InlineData("upper", "abc", "ABC")]
    [InlineData("lower", "ABC", "abc")]
    [InlineData("normalizeSpaces", "  Alice   Johnson ", "Alice Johnson")]
    [InlineData("normalizeSpaces", "Bob  ", "Bob")]
    public void ResolveTransform_applies_expected(string name, string input, string expected)
    {
        var transform = TransformRegistry.ResolveTransform(name)!;
        transform.Apply(input).Should().Be(expected);
    }

    [Fact]
    public void ResolveTransform_unknown_throws()
    {
        var act = () => TransformRegistry.ResolveTransform("nope");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveTransform_null_returns_null()
    {
        TransformRegistry.ResolveTransform(null).Should().BeNull();
    }

    [Fact]
    public void ConvertToType_int_parses_to_long()
    {
        var column = new ColumnMapping { Target = "X", Type = "int" };
        ValueTypeConverter.ConvertToType("42", column).Should().Be(42L);
    }

    [Fact]
    public void ConvertToType_decimal_parses_invariant()
    {
        var column = new ColumnMapping { Target = "X", Type = "decimal" };
        ValueTypeConverter.ConvertToType("3.14", column).Should().Be(3.14m);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    public void ConvertToType_bool_parses_common_forms(string input, bool expected)
    {
        var column = new ColumnMapping { Target = "X", Type = "bool" };
        ValueTypeConverter.ConvertToType(input, column).Should().Be(expected);
    }

    [Fact]
    public void ConvertToType_date_reformats_using_formats()
    {
        var column = new ColumnMapping
        {
            Target = "D", Type = "date", SourceFormat = "MM/dd/yyyy", Format = "yyyy-MM-dd",
        };
        ValueTypeConverter.ConvertToType("03/14/1990", column).Should().Be("1990-03-14");
    }

    [Fact]
    public void ConvertToType_empty_value_is_passthrough()
    {
        var column = new ColumnMapping { Target = "X", Type = "int" };
        ValueTypeConverter.ConvertToType("", column).Should().Be("");
    }

    [Fact]
    public void ConvertToType_bad_int_throws()
    {
        var column = new ColumnMapping { Target = "X", Type = "int" };
        var act = () => ValueTypeConverter.ConvertToType("abc", column);
        act.Should().Throw<FormatException>();
    }
}

using EtlTool.Core.Mapping;
using EtlTool.Core.Models;
using EtlTool.Core.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EtlTool.Tests;

public class ValidationEtlTests : IDisposable
{
    private readonly string _root;
    private readonly string _inputDir;
    private readonly string _outputDir;
    private readonly string _invalidDir;
    private readonly string _mappingFilePath;

    // Mapping that exercises every validation rule: required + min (CustomerId), required +
    // minLength (Fullname), required + pattern (Email), min/max (Age), allowedValues (Gender).
    private const string ValidatingMapping =
        """
        {
          "source": { "delimiter": ",", "hasHeader": true, "normalizeWhitespace": true },
          "target": { "delimiter": ",", "writeHeader": true },
          "columns": [
            { "source": "customer_id", "target": "CustomerId", "type": "int",
              "validation": { "required": true, "min": 1 } },
            { "sources": ["firstname", "lastname"], "separator": " ", "target": "Fullname",
              "transform": "normalizeSpaces", "validation": { "required": true, "minLength": 2 } },
            { "source": "email", "target": "Email", "transform": "lower",
              "validation": { "required": true, "pattern": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$" } },
            { "source": "age", "target": "Age", "type": "int", "validation": { "min": 0, "max": 120 } },
            { "source": "gender", "target": "Gender", "validation": { "allowedValues": ["male", "female"] } }
          ],
          "outputs": [
            { "suffix": "male",   "filter": { "column": "gender", "op": "equals", "value": "male" } },
            { "suffix": "female", "filter": { "column": "gender", "op": "equals", "value": "female" } }
          ]
        }
        """;

    public ValidationEtlTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "etl-test-" + Guid.NewGuid().ToString("N"));
        _inputDir = Path.Combine(_root, "input");
        _outputDir = Path.Combine(_root, "output");
        _invalidDir = Path.Combine(_root, "invalid");
        _mappingFilePath = Path.Combine(_root, "mapping.json");
        Directory.CreateDirectory(_inputDir);
        File.WriteAllText(_mappingFilePath, ValidatingMapping);
    }

    private BatchEtlResult Run(ErrorPolicy policy = ErrorPolicy.Skip) =>
        new FolderEtlProcessor(NullLogger.Instance)
            .ProcessFolder(_inputDir, _outputDir, _invalidDir, _mappingFilePath, policy);

    [Fact]
    public void Run_routes_each_validation_failure_to_the_invalid_folder()
    {
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            """
            customer_id,firstname,lastname,gender,email,age
            1001,Alice,Johnson,female,alice@example.com,34
            1002,Bob,Smith,male,bob@example.com,29
            ,Eve,Adams,female,eve@example.com,27
            1006,Frank,Miller,male,frank[at]example.com,38
            1007,Grace,Lee,female,grace@example.com,200
            1008,Henry,Ford,male,henry@example.com,-3
            1009,Ivy,Nguyen,other,ivy@example.com,30
            """);

        var result = Run();

        result.FilesFailed.Should().Be(0);
        result.TotalRowsRead.Should().Be(7);
        result.TotalRowsWritten.Should().Be(2);    // Alice + Bob
        result.TotalRowsInvalid.Should().Be(5);

        var invalidLines = File.ReadAllLines(Path.Combine(_invalidDir, "customers_invalid.csv"));
        invalidLines[0].Should().Be("customer_id,firstname,lastname,gender,email,age,InvalidReason");
        // The original input columns are preserved and the reason names the failing rule.
        invalidLines.Should().Contain(l => l.StartsWith(",Eve,") && l.Contains("required"));
        invalidLines.Should().Contain(l => l.Contains("Frank") && l.Contains("does not match pattern"));
        invalidLines.Should().Contain(l => l.Contains("Grace") && l.Contains("must be <= 120"));
        invalidLines.Should().Contain(l => l.Contains("Henry") && l.Contains("must be >= 0"));
        invalidLines.Should().Contain(l => l.Contains("Ivy") && l.Contains("allowed values"));
    }

    [Fact]
    public void Run_with_all_valid_rows_creates_no_invalid_file()
    {
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            """
            customer_id,firstname,lastname,gender,email,age
            1001,Alice,Johnson,female,alice@example.com,34
            1002,Bob,Smith,male,bob@example.com,29
            """);

        var result = Run();

        result.TotalRowsWritten.Should().Be(2);
        result.TotalRowsInvalid.Should().Be(0);
        Directory.Exists(_invalidDir).Should().BeFalse();
    }

    [Fact]
    public void Run_with_validation_failure_under_on_error_fail_aborts_the_file()
    {
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            """
            customer_id,firstname,lastname,gender,email,age
            1006,Frank,Miller,male,frank[at]example.com,38
            """);

        var result = Run(ErrorPolicy.Fail);

        result.FilesFailed.Should().Be(1);   // validation failure honors --on-error fail
        result.TotalRowsInvalid.Should().Be(1);
    }

    [Fact]
    public void LoadFromFile_with_invalid_regex_pattern_throws()
    {
        File.WriteAllText(_mappingFilePath,
            """
            {
              "source": { "delimiter": ",", "hasHeader": true },
              "target": { "delimiter": ",", "writeHeader": true },
              "columns": [
                { "source": "email", "target": "Email", "validation": { "pattern": "[unterminated" } }
              ]
            }
            """);

        var act = () => EtlMappingLoader.LoadFromFile(_mappingFilePath);
        act.Should().Throw<InvalidOperationException>().WithMessage("*invalid validation pattern*");
    }

    [Fact]
    public void LoadFromFile_with_minLength_greater_than_maxLength_throws()
    {
        File.WriteAllText(_mappingFilePath,
            """
            {
              "source": { "delimiter": ",", "hasHeader": true },
              "target": { "delimiter": ",", "writeHeader": true },
              "columns": [
                { "source": "name", "target": "Name", "validation": { "minLength": 5, "maxLength": 2 } }
              ]
            }
            """);

        var act = () => EtlMappingLoader.LoadFromFile(_mappingFilePath);
        act.Should().Throw<InvalidOperationException>().WithMessage("*greater than 'maxLength'*");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }
}

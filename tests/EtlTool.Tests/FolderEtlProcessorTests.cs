using EtlTool.Core.Models;
using EtlTool.Core.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EtlTool.Tests;

public class FolderEtlProcessorTests : IDisposable
{
    private readonly string _root;
    private readonly string _inputDir;
    private readonly string _outputDir;
    private readonly string _invalidDir;
    private readonly string _mappingFilePath;

    // Mapping: combine firstname+lastname -> Fullname, and split output by gender (male / female).
    private const string GenderSplitMapping =
        """
        {
          "source": { "delimiter": ",", "hasHeader": true },
          "target": { "delimiter": ",", "writeHeader": true },
          "columns": [
            { "source": "customer_id", "target": "CustomerId", "type": "int" },
            { "sources": ["firstname", "lastname"], "separator": " ", "target": "Fullname", "transform": "normalizeSpaces" },
            { "source": "email", "target": "Email", "transform": "lower" }
          ],
          "outputs": [
            { "suffix": "male",   "filter": { "column": "gender", "op": "equals", "value": "male" } },
            { "suffix": "female", "filter": { "column": "gender", "op": "equals", "value": "female" } }
          ]
        }
        """;

    public FolderEtlProcessorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "etl-test-" + Guid.NewGuid().ToString("N"));
        _inputDir = Path.Combine(_root, "input");
        _outputDir = Path.Combine(_root, "output");
        _invalidDir = Path.Combine(_root, "invalid");
        _mappingFilePath = Path.Combine(_root, "mapping.json");
        Directory.CreateDirectory(_inputDir);
        File.WriteAllText(_mappingFilePath, GenderSplitMapping);
    }

    private BatchEtlResult Run(ErrorPolicy policy = ErrorPolicy.Skip) =>
        new FolderEtlProcessor(NullLogger.Instance)
            .ProcessFolder(_inputDir, _outputDir, _invalidDir, _mappingFilePath, policy);

    private void WriteSampleCustomers(string fileName = "customers.csv") =>
        File.WriteAllText(Path.Combine(_inputDir, fileName),
            """
            customer_id,firstname,lastname,gender,email
            1001,Alice,Johnson,female,Alice.Johnson@Example.com
            1002,Bob,Smith,male,bob@example.com
            1003,Carol,White,female,carol.white@example.com
            1004,David,Brown,male,david@example.com
            """);

    [Fact]
    public void Run_writes_separate_male_and_female_files()
    {
        WriteSampleCustomers();

        var result = Run();

        result.FilesProcessed.Should().Be(1);
        result.TotalRowsRead.Should().Be(4);
        result.TotalRowsWritten.Should().Be(4);
        result.TotalRowsInvalid.Should().Be(0);

        var maleLines = File.ReadAllLines(Path.Combine(_outputDir, "customers_male.csv"));
        var femaleLines = File.ReadAllLines(Path.Combine(_outputDir, "customers_female.csv"));

        maleLines[0].Should().Be("CustomerId,Fullname,Email");
        maleLines.Skip(1).Should().BeEquivalentTo(
            "1002,Bob Smith,bob@example.com",
            "1004,David Brown,david@example.com");
        femaleLines.Skip(1).Should().BeEquivalentTo(
            "1001,Alice Johnson,alice.johnson@example.com",
            "1003,Carol White,carol.white@example.com");
    }

    [Fact]
    public void Run_combines_firstname_and_lastname_with_a_space()
    {
        WriteSampleCustomers();

        Run();

        var maleLines = File.ReadAllLines(Path.Combine(_outputDir, "customers_male.csv"));
        maleLines.Should().Contain(line => line.Contains("Bob Smith"));
        maleLines.Should().NotContain(line => line.Contains("BobSmith"));
    }

    [Fact]
    public void Run_removes_extra_spaces_from_combined_fullname()
    {
        // Padded first name + extra spaces would otherwise yield "Bob   Smith".
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            "customer_id,firstname,lastname,gender,email\n1002,  Bob ,  Smith ,male,b@example.com\n");

        Run();

        var maleLines = File.ReadAllLines(Path.Combine(_outputDir, "customers_male.csv"));
        maleLines[1].Should().Be("1002,Bob Smith,b@example.com");   // single space, no padding
    }

    [Fact]
    public void Run_with_normalizeWhitespace_cleans_every_column_and_still_routes_by_gender()
    {
        // normalizeWhitespace cleans EVERY field on read: trims ends and collapses internal
        // runs. So padded "  male " matches the split, and all columns come out clean.
        File.WriteAllText(_mappingFilePath,
            """
            {
              "source": { "delimiter": ",", "hasHeader": true, "normalizeWhitespace": true },
              "target": { "delimiter": ",", "writeHeader": true },
              "columns": [
                { "sources": ["firstname", "lastname"], "separator": " ", "target": "Fullname" },
                { "source": "city", "target": "City" },
                { "source": "email", "target": "Email", "transform": "lower" }
              ],
              "outputs": [
                { "suffix": "male",   "filter": { "column": "gender", "op": "equals", "value": "male" } },
                { "suffix": "female", "filter": { "column": "gender", "op": "equals", "value": "female" } }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            "customer_id,firstname,lastname,gender,city,email\n" +
            "100001,  Rahul , Perez ,  male , New   York , Rahul.Perez@TEST.io \n");

        var result = Run();

        result.TotalRowsWritten.Should().Be(1);
        result.TotalRowsInvalid.Should().Be(0);
        var maleLines = File.ReadAllLines(Path.Combine(_outputDir, "customers_male.csv"));
        // Fullname combined cleanly, City internal double-space collapsed, Email trimmed+lowercased.
        maleLines[1].Should().Be("Rahul Perez,New York,rahul.perez@test.io");
    }

    [Fact]
    public void Run_with_valid_input_creates_no_invalid_file()
    {
        WriteSampleCustomers();

        Run();

        Directory.Exists(_invalidDir).Should().BeFalse();   // nothing invalid => folder not created
    }

    [Fact]
    public void Run_sends_unroutable_rows_to_the_invalid_folder()
    {
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            """
            customer_id,firstname,lastname,gender,email
            1001,Alice,Johnson,female,a@example.com
            1002,Bob,Smith,malef,b@example.com
            """);

        var result = Run();

        result.TotalRowsWritten.Should().Be(1);   // only Alice routed
        result.TotalRowsInvalid.Should().Be(1);   // Bob: gender "malef" matches no split

        var invalidLines = File.ReadAllLines(Path.Combine(_invalidDir, "customers_invalid.csv"));
        invalidLines[0].Should().Be("customer_id,firstname,lastname,gender,email,InvalidReason");
        invalidLines[1].Should().Be("1002,Bob,Smith,malef,b@example.com,no matching output split");
    }

    [Fact]
    public void Run_sends_conversion_errors_to_the_invalid_folder()
    {
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            """
            customer_id,firstname,lastname,gender,email
            1001,Alice,Johnson,female,a@example.com
            notanumber,Bob,Smith,male,b@example.com
            """);

        var result = Run();

        result.FilesFailed.Should().Be(0);
        result.TotalRowsWritten.Should().Be(1);
        result.TotalRowsInvalid.Should().Be(1);

        var invalidLines = File.ReadAllLines(Path.Combine(_invalidDir, "customers_invalid.csv"));
        invalidLines.Should().HaveCount(2);                 // header + 1 bad row
        invalidLines[1].Should().StartWith("notanumber,Bob,Smith,male,b@example.com,");
    }

    [Fact]
    public void Run_produces_two_output_files_per_input_file()
    {
        WriteSampleCustomers("a.csv");
        WriteSampleCustomers("b.csv");

        var result = Run();

        result.FilesProcessed.Should().Be(2);
        File.Exists(Path.Combine(_outputDir, "a_male.csv")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "a_female.csv")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "b_male.csv")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "b_female.csv")).Should().BeTrue();
    }

    [Fact]
    public void Run_with_empty_folder_is_clean_noop()
    {
        var result = Run();

        result.FilesProcessed.Should().Be(0);
        result.FilesFailed.Should().Be(0);
    }

    [Fact]
    public void Run_without_outputs_writes_single_out_file()
    {
        File.WriteAllText(_mappingFilePath,
            """
            {
              "source": { "delimiter": ",", "hasHeader": true },
              "target": { "delimiter": ",", "writeHeader": true },
              "columns": [
                { "sources": ["firstname", "lastname"], "target": "Fullname" }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(_inputDir, "customers.csv"),
            "firstname,lastname\nAlice,Johnson\n");

        var result = Run();

        result.TotalRowsWritten.Should().Be(1);
        var lines = File.ReadAllLines(Path.Combine(_outputDir, "customers.out.csv"));
        lines[0].Should().Be("Fullname");
        lines[1].Should().Be("Alice Johnson");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }
}

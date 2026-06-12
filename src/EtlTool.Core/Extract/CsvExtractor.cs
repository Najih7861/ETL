using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using EtlTool.Core.Mapping;
using EtlTool.Core.Models;

namespace EtlTool.Core.Extract;

/// <summary>EXTRACT stage: streams an input CSV file into <see cref="CsvRow"/> objects lazily.</summary>
public sealed class CsvExtractor
{
    /// <summary>
    /// Reads <paramref name="csvFilePath"/> one record at a time. The file handle stays open
    /// only while the returned sequence is being enumerated, keeping memory flat for large files.
    /// </summary>
    public IEnumerable<CsvRow> ExtractRows(string csvFilePath, SourceFormat source)
    {
        var encoding = Encoding.GetEncoding(source.Encoding);
        var csvReaderConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = source.Delimiter,
            HasHeaderRecord = source.HasHeader,
            // Trim whitespace around each field (and header) when requested.
            TrimOptions = source.TrimFields ? TrimOptions.Trim : TrimOptions.None,
        };

        using var streamReader = new StreamReader(csvFilePath, encoding);
        using var csvReader = new CsvReader(streamReader, csvReaderConfig);

        string[] headerColumns;
        if (source.HasHeader)
        {
            csvReader.Read();
            csvReader.ReadHeader();
            headerColumns = csvReader.HeaderRecord ?? Array.Empty<string>();
        }
        else
        {
            headerColumns = Array.Empty<string>();
        }

        while (csvReader.Read())
        {
            var row = new CsvRow();
            if (source.HasHeader)
            {
                foreach (var columnName in headerColumns)
                    row[columnName] = csvReader.GetField(columnName);
            }
            else
            {
                // Headerless files expose columns by zero-based ordinal: "0", "1", ...
                for (var i = 0; i < csvReader.Parser.Count; i++)
                    row[i.ToString(CultureInfo.InvariantCulture)] = csvReader.GetField(i);
            }
            yield return row;
        }
    }
}

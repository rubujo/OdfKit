using System.Text;
using BenchmarkDotNet.Attributes;
using OdfKit.Collaboration;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// ODT JSON collaboration operation parsing, serialization, and replay benchmarks.
/// ODT JSON collaboration operation 剖析、序列化與重播效能基準。
/// </summary>
[MemoryDiagnoser]
public class CollaborationOperationBenchmarks
{
    private string _tenThousandOperationLog = null!;
    private string _longParagraphFormatLog = null!;
    private string _largeTableLog = null!;
    private OdtOperationLog _parsedTenThousandOperationLog = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tenThousandOperationLog = CreateTextOperationLog(5_000);
        _longParagraphFormatLog = CreateLongParagraphFormatLog(64_000);
        _largeTableLog = CreateLargeTableLog(rows: 1_000, columns: 20);
        _parsedTenThousandOperationLog = OdtOperationLog.Parse(
            _tenThousandOperationLog,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility());
    }

    [Benchmark]
    public int Parse_10kOperations()
    {
        OdtOperationLog log = OdtOperationLog.Parse(
            _tenThousandOperationLog,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility());
        return log.Operations.Count;
    }

    [Benchmark]
    public int Serialize_10kOperations()
    {
        string json = _parsedTenThousandOperationLog.Serialize(OdtOperationCompatibilityOptions.CreateTdfCompatibility());
        return json.Length;
    }

    [Benchmark]
    public int Replay_10kTextOperations()
    {
        using TextDocument document = TextDocument.Create();
        OdtOperationImportReport report = _parsedTenThousandOperationLog.Apply(
            document,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility());
        return report.ReplayedCount;
    }

    [Benchmark]
    public int Replay_LongParagraphRangeFormatting()
    {
        using TextDocument document = OdtOperationsImporter.Merge(
            _longParagraphFormatLog,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);
        return report.ReplayedCount + document.Body.Paragraphs.First().Runs.Count();
    }

    [Benchmark]
    public int Replay_FixedSizeLargeTable()
    {
        using TextDocument document = OdtOperationsImporter.Merge(
            _largeTableLog,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);
        return report.ReplayedCount + document.Body.Tables.Count();
    }

    private static string CreateTextOperationLog(int paragraphCount)
    {
        var builder = new StringBuilder();
        builder.Append("{\"changes\":[");
        for (int i = 0; i < paragraphCount; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"name\":\"addParagraph\",\"start\":[");
            builder.Append(i);
            builder.Append("]},");
            builder.Append("{\"name\":\"addText\",\"start\":[");
            builder.Append(i);
            builder.Append(",0],\"text\":\"Value ");
            builder.Append(i);
            builder.Append("\"}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string CreateLongParagraphFormatLog(int textLength)
    {
        string text = new('A', textLength);
        return "{\"changes\":[{\"name\":\"addParagraph\",\"start\":[0]},{\"name\":\"addText\",\"start\":[0,0],\"text\":\"" +
            text +
            "\"},{\"name\":\"format\",\"start\":[0,1024],\"end\":[0," +
            (textLength - 1024).ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "],\"attrs\":{\"bold\":true,\"color\":\"#0066CC\"}}]}";
    }

    private static string CreateLargeTableLog(int rows, int columns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"changes\":[{\"name\":\"addTable\",\"attrs\":{\"rows\":");
        builder.Append(rows);
        builder.Append(",\"columns\":");
        builder.Append(columns);
        builder.Append("}}");
        for (int row = 0; row < rows; row++)
        {
            builder.Append(",{\"name\":\"addRows\",\"attrs\":{\"row\":");
            builder.Append(row);
            builder.Append("}},{\"name\":\"addCells\",\"attrs\":{\"values\":[");
            for (int column = 0; column < columns; column++)
            {
                if (column > 0)
                {
                    builder.Append(',');
                }

                builder.Append('"');
                builder.Append(row);
                builder.Append(':');
                builder.Append(column);
                builder.Append('"');
            }

            builder.Append("]}}");
        }

        builder.Append("]}");
        return builder.ToString();
    }
}

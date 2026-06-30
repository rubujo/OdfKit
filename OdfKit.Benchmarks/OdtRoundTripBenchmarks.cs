using BenchmarkDotNet.Attributes;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// Large ODT create, save, and load benchmarks.
/// 大型 ODT 建立、儲存與載入效能基準。
/// </summary>
[MemoryDiagnoser]
public class OdtRoundTripBenchmarks
{
    private byte[] _largeDocumentBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        using TextDocument document = CreateLargeDocument();
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        _largeDocumentBytes = stream.ToArray();
    }

    [Benchmark]
    public int CreateLargeTextDocument()
    {
        using TextDocument document = CreateLargeDocument();
        return document.Body.Paragraphs.Count();
    }

    [Benchmark]
    public long SaveLargeTextDocument()
    {
        using TextDocument document = CreateLargeDocument();
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        return stream.Length;
    }

    [Benchmark]
    public int LoadLargeTextDocument()
    {
        using var stream = new MemoryStream(_largeDocumentBytes, writable: false);
        using TextDocument document = TextDocument.Load(stream);
        return document.Body.Paragraphs.Count();
    }

    private static TextDocument CreateLargeDocument()
    {
        TextDocument document = TextDocument.Create();
        document.GetDefaultPageSetup().Header.Text = "Benchmark";
        for (int i = 0; i < 2_000; i++)
        {
            OdfParagraph paragraph = document.AddParagraph("Benchmark paragraph " + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (i % 100 == 0)
            {
                paragraph.AddComment(new OdfComment("benchmark", "Comment " + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        OdfTable table = document.AddTable(100, 10);
        for (int row = 0; row < 100; row++)
        {
            for (int column = 0; column < 10; column++)
            {
                table.GetCell(row, column).AddParagraph(row.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + column.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return document;
    }
}

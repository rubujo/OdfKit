using System.Globalization;
using System.IO.Compression;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

/// <summary>
/// Provides deterministic ODS, ODT, and ODP workloads shared by benchmarks and correctness tests.
/// 提供效能基準與正確性測試共用的決定性 ODS、ODT 與 ODP 工作負載。
/// </summary>
internal static class StandardPerformanceWorkloads
{
    private static readonly string[] CellTextValues = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon"];
    /// <summary>
    /// Gets the standard ODS row count.
    /// 取得標準 ODS 列數。
    /// </summary>
    internal const int StandardOdsRowCount = 1_000_000;
    internal const int StandardOdsReadRowCount = 50_000;
    /// <summary>
    /// Gets the standard ODT text-node count.
    /// 取得標準 ODT 文字節點數。
    /// </summary>
    internal const int StandardOdtNodeCount = 100_000;
    /// <summary>
    /// Gets the standard structure-dense ODP slide count.
    /// 取得標準結構密集 ODP 投影片數。
    /// </summary>
    internal const int StandardOdpStructureSlideCount = 500;
    /// <summary>
    /// Gets the standard media-dense ODP slide count.
    /// 取得標準媒體密集 ODP 投影片數。
    /// </summary>
    internal const int StandardOdpMediaSlideCount = 100;

    private static readonly byte[] s_png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>
    /// Creates a deterministic streaming ODS dataset.
    /// 建立決定性的串流 ODS 資料集。
    /// </summary>
    internal static byte[] CreateStreamingOds(int rowCount)
    {
        using var output = new MemoryStream();
        using (var writer = new OdsStreamWriter(output))
        {
            writer.WriteStartSheet("Data");
            for (int row = 0; row < rowCount; row++)
            {
                writer.WriteStartRow();
                WriteOdsRow(writer, row);
                writer.WriteEndRow();
            }

            writer.WriteEndSheet();
        }

        return output.ToArray();
    }

    /// <summary>
    /// Creates a deterministic complex ODS DOM dataset.
    /// 建立決定性的複雜 ODS DOM 資料集。
    /// </summary>
    internal static byte[] CreateComplexOds(int rowsPerSheet)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        for (int sheetIndex = 0; sheetIndex < 3; sheetIndex++)
        {
            OdfTableSheet sheet = document.Worksheets.Add($"Sheet{sheetIndex + 1}");
            for (int row = 0; row < rowsPerSheet; row++)
            {
                for (int column = 0; column < 10; column++)
                {
                    sheet.GetCell(row, column).CellValue = column == 1
                        ? $"值 {sheetIndex}:{row}"
                        : row * 10d + column;
                }

                sheet.GetCell(row, 9).Formula = "of:=SUM([.A1:.I1])";
            }

            sheet.MergeCells(new OdfCellRange(0, 0, 0, 1));
            sheet.AddNamedRange($"Range{sheetIndex + 1}", new OdfCellRange(0, 0, Math.Max(0, rowsPerSheet - 1), 9));
            sheet.AddConditionalFormat(new OdfCellRange(1, 0, Math.Max(1, rowsPerSheet - 1), 9), "cell-content()>=100", "GoodStyle");
        }

        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.ToArray();
    }

    /// <summary>
    /// Calculates the streaming ODS semantic checksum.
    /// 計算串流 ODS 語意檢查碼。
    /// </summary>
    internal static ulong ChecksumStreamingOds(byte[] bytes)
    {
        ulong hash = FnvOffset;
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = new OdsStreamReader(input);
        while (reader.Read())
        {
            hash = Add(hash, reader.RowIndex);
            for (int column = 0; column < reader.FieldCount; column++)
            {
                hash = Add(hash, Convert.ToString(reader.GetValue(column), CultureInfo.InvariantCulture));
            }
        }

        return hash;
    }

    internal static ulong ChecksumOdsXml(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        ZipArchiveEntry entry = archive.GetEntry("content.xml") ?? throw new InvalidDataException("content.xml is missing.");
        using Stream content = entry.Open();
        using var reader = System.Xml.XmlReader.Create(content, new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 0,
        });
        ulong hash = FnvOffset;
        while (reader.Read())
        {
            if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.NamespaceURI == OdfNamespaces.Table &&
                reader.LocalName is "table-row" or "table-cell")
            {
                hash = Add(hash, reader.LocalName);
                hash = Add(hash, reader.GetAttribute("value-type", OdfNamespaces.Office));
                hash = Add(hash, reader.GetAttribute("value", OdfNamespaces.Office));
                hash = Add(hash, reader.GetAttribute("date-value", OdfNamespaces.Office));
                hash = Add(hash, reader.GetAttribute("boolean-value", OdfNamespaces.Office));
                hash = Add(hash, reader.GetAttribute("formula", OdfNamespaces.Table));
            }
            else if (reader.NodeType is System.Xml.XmlNodeType.Text or System.Xml.XmlNodeType.CDATA)
            {
                hash = Add(hash, reader.Value);
            }
        }

        return hash;
    }

    internal static ulong ChecksumComplexOds(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(input, "complex.ods");
        ulong hash = Add(FnvOffset, document.Worksheets.Count);
        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            hash = Add(hash, sheet.Name);
            foreach (OdfCell cell in sheet.UsedCells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column))
            {
                hash = Add(hash, cell.Row);
                hash = Add(hash, cell.Column);
                hash = Add(hash, Convert.ToString(cell.CellValue, CultureInfo.InvariantCulture));
                hash = Add(hash, cell.Formula);
            }

            hash = Add(hash, sheet.NamedRanges.Count);
            hash = Add(hash, sheet.ConditionalFormats.Count);
        }

        return hash;
    }

    /// <summary>
    /// Creates a deterministic streaming ODT dataset.
    /// 建立決定性的串流 ODT 資料集。
    /// </summary>
    internal static byte[] CreateStreamingOdt(int nodeCount)
    {
        using var output = new MemoryStream();
        using (var writer = new OdtStreamWriter(output))
        {
            for (int index = 0; index < nodeCount; index++)
            {
                if (index % 100 == 0)
                {
                    writer.AddHeading($"章節 {index}", index % 6 + 1);
                }
                else if (index % 10 == 0)
                {
                    writer.BeginList();
                    writer.AddListItem($"清單 {index}");
                    writer.EndList();
                }
                else
                {
                    writer.AddParagraph($"Benchmark paragraph {index}", "BodyStyle");
                }
            }
        }

        return output.ToArray();
    }

    /// <summary>
    /// Creates a deterministic complex ODT DOM dataset.
    /// 建立決定性的複雜 ODT DOM 資料集。
    /// </summary>
    internal static byte[] CreateComplexOdt(int paragraphCount)
    {
        using TextDocument document = TextDocument.Create();
        document.GetDefaultPageSetup().Header.Text = "OdfKit benchmark";
        for (int index = 0; index < paragraphCount; index++)
        {
            OdfParagraph paragraph = document.AddParagraph($"複雜段落 {index}");
            if (index % 100 == 0)
            {
                paragraph.AddComment(new OdfComment("benchmark", $"註解 {index}"));
            }
        }

        OdfTable table = document.AddTable(100, 10);
        for (int row = 0; row < 100; row++)
        {
            for (int column = 0; column < 10; column++)
            {
                table.GetCell(row, column).AddParagraph($"{row}:{column}");
            }
        }

        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.ToArray();
    }

    /// <summary>
    /// Calculates the streaming ODT semantic checksum.
    /// 計算串流 ODT 語意檢查碼。
    /// </summary>
    internal static ulong ChecksumStreamingOdt(byte[] bytes)
    {
        ulong hash = FnvOffset;
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = new OdtStreamReader(input);
        while (reader.Read())
        {
            hash = Add(hash, (int)reader.NodeType);
            hash = Add(hash, reader.HeadingLevel);
            hash = Add(hash, reader.StyleName);
            hash = Add(hash, reader.Text);
        }

        return hash;
    }

    /// <summary>
    /// Creates a deterministic ODP dataset.
    /// 建立決定性的 ODP 資料集。
    /// </summary>
    internal static byte[] CreateOdp(int slideCount, bool includeMedia)
    {
        PresentationDocumentBuilder builder = PresentationDocument.Builder().WithMasterPage("BenchmarkMaster", "#F4F6F8");
        for (int index = 0; index < slideCount; index++)
        {
            int captured = index;
            builder.AddSlide($"Slide {index + 1}", slide =>
            {
                slide.AddTitle($"效能投影片 {captured}")
                    .AddTextBox(["第一段", $"內容 {captured}", "第三段"], 1, 3, 10, 4)
                    .AddShape(OdfShapeType.Rectangle, 12, 3, 4, 3)
                    .WithSpeakerNotes(["講者備忘", $"投影片 {captured}"])
                    .WithTransition(OdfTransitionType.Fade);
                if (includeMedia)
                {
                    slide.AddImage(s_png, 12, 7, 2, 2)
                        .AddChartPlaceholder(1, 8, 8, 5);
                }
            });
        }

        using PresentationDocument document = builder.Build();
        if (includeMedia)
        {
            for (int index = 0; index < document.Slides.Count; index++)
            {
                OdfSlide slide = document.Slides[index];
                OdfShape shape = slide.Shapes[0];
                shape.Id = $"benchmark-shape-{index}";
                shape.AddEmbeddedTable(3, 4).SetCellText(0, 0, $"表格 {index}");
                slide.AddEntranceEffect(shape.Id, OdfAnimationEffect.Fade, OdfAnimationTrigger.AfterPrevious);
            }
        }

        using var output = new MemoryStream();
        document.SaveToStream(output);
        return output.ToArray();
    }

    /// <summary>
    /// Calculates the ODP semantic checksum.
    /// 計算 ODP 語意檢查碼。
    /// </summary>
    internal static ulong ChecksumOdp(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using PresentationDocument document = PresentationDocument.Load(input, "benchmark.odp");
        ulong hash = Add(FnvOffset, document.Slides.Count);
        hash = Add(hash, document.GetMasterPages().Count);
        for (int index = 0; index < document.Slides.Count; index++)
        {
            OdfSlide slide = document.Slides[index];
            hash = Add(hash, slide.Name);
            hash = Add(hash, slide.TextBoxes.Count);
            hash = Add(hash, slide.Shapes.Count);
            hash = Add(hash, slide.Pictures.Count);
            hash = Add(hash, slide.Placeholders.Count);
            hash = Add(hash, slide.GetAnimations().Count);
            hash = Add(hash, slide.SpeakerNotes);
            foreach (OdfTextBox textBox in slide.TextBoxes)
            {
                hash = Add(hash, textBox.Text);
            }
        }

        hash = Add(hash, CountContentElements(bytes, "table", OdfNamespaces.Table));
        return hash;
    }

    private static int CountContentElements(byte[] bytes, string localName, string namespaceUri)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        ZipArchiveEntry entry = archive.GetEntry("content.xml") ?? throw new InvalidDataException("content.xml is missing.");
        using Stream content = entry.Open();
        using var reader = System.Xml.XmlReader.Create(content, new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
        });
        int count = 0;
        while (reader.Read())
        {
            if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.LocalName == localName && reader.NamespaceURI == namespaceUri)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Gets compressed size and total XML entry size.
    /// 取得壓縮大小與 XML 項目總大小。
    /// </summary>
    internal static (long PackageBytes, long XmlBytes) GetPackageSizes(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        long xmlBytes = archive.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).Sum(entry => entry.Length);
        return (bytes.LongLength, xmlBytes);
    }

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static ulong Add(ulong hash, int value) => Add(hash, value.ToString(CultureInfo.InvariantCulture));

    private static ulong Add(ulong hash, string? value)
    {
        foreach (char character in value ?? "<null>")
        {
            hash ^= character;
            hash *= FnvPrime;
        }

        hash ^= 0xFF;
        return hash * FnvPrime;
    }

    private static void WriteOdsRow(OdsStreamWriter writer, int row)
    {
        writer.WriteCell((double)row);
        writer.WriteCell($"Item-{row:D7}");
        writer.WriteCell(Math.Round((row * 17.31) % 10_000, 2));
        writer.WriteCell((double)(row % 500 + 1));
        writer.WriteCell(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(row));
        writer.WriteCell(row % 3 == 0);
        writer.WriteCell(Math.Round((row * 0.61803398875) % 100, 4));
        writer.WriteCell(CellTextValues[row % CellTextValues.Length]);
        writer.WriteCell(row * 7d);
        writer.WriteCell($"備註 {row}");
    }
}

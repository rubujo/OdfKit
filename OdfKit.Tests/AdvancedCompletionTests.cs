using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證大型局部編輯、run-aware AutoFit 與樞紐物化的有界契約。
/// </summary>
public class AdvancedCompletionTests
{
    /// <summary>
    /// 驗證串流局部編輯可拆分重複列與欄，且保留未修改的封裝項目。
    /// </summary>
    [Fact]
    public async Task SparseEditorSplitsRepeatedRowsAndCells()
    {
        using MemoryStream source = CreateRepeatedOds();
        using var destination = new MemoryStream();
        await OdsSparseEditor.ApplyAsync(
            source,
            destination,
            [new OdsCellPatch { SheetName = "Data", Row = 2, Column = 3, Text = "patched" }],
            new OdsSparseEditorOptions(),
            TestContext.Current.CancellationToken);

        destination.Position = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        string xml = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        _ = XDocument.Parse(xml);
        Assert.Contains(">patched<", xml);
        Assert.Contains("table:number-rows-repeated=\"2\"", xml);
        Assert.Contains("table:number-columns-repeated=\"3\"", xml);
        Assert.NotNull(archive.GetEntry("custom.bin"));
    }

    /// <summary>
    /// 驗證串流局部編輯會拒絕重複座標與超出預算的輸入。
    /// </summary>
    [Fact]
    public async Task SparseEditorRejectsDuplicateAndOverBudgetPatches()
    {
        using MemoryStream source = CreateRepeatedOds();
        using var destination = new MemoryStream();
        OdsCellPatch patch = new() { SheetName = "Data", Row = 0, Column = 0, Text = "x" };
        await Assert.ThrowsAsync<ArgumentException>(
            () => OdsSparseEditor.ApplyAsync(
                source,
                destination,
                [patch, patch],
                new OdsSparseEditorOptions(),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 驗證串流局部編輯禁止 DTD 與實體展開。
    /// </summary>
    [Fact]
    public async Task SparseEditorRejectsDtd()
    {
        using MemoryStream source = CreateRepeatedOds(
            "<!DOCTYPE office:document-content [<!ENTITY x \"expanded\">]>");
        using var destination = new MemoryStream();
        await Assert.ThrowsAnyAsync<Exception>(
            () => OdsSparseEditor.ApplyAsync(
                source,
                destination,
                [new OdsCellPatch { SheetName = "Data", Row = 0, Column = 0, Text = "x" }],
                new OdsSparseEditorOptions(),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 驗證串流局部編輯能安全設定公式、引用既有樣式及建立跨列欄合併。
    /// </summary>
    [Fact]
    public async Task SparseEditorPatchesFormulaStyleAndMerge()
    {
        using MemoryStream source = CreateSparseFeatureOds();
        using var destination = new MemoryStream();
        await OdsSparseEditor.ApplyAsync(
            source,
            destination,
            [
                new OdsCellPatch
                {
                    SheetName = "Data",
                    Row = 0,
                    Column = 0,
                    Formula = "of:=1+2",
                    StyleName = "ce1",
                },
                new OdsCellPatch
                {
                    SheetName = "Data",
                    Row = 1,
                    Column = 0,
                    Text = "merged",
                    RowSpan = 2,
                    ColumnSpan = 2,
                },
            ],
            new OdsSparseEditorOptions(),
            TestContext.Current.CancellationToken);

        destination.Position = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Read, leaveOpen: true);
        XDocument content = XDocument.Load(archive.GetEntry("content.xml")!.Open());
        XNamespace table = OdfNamespaces.Table;
        XNamespace office = OdfNamespaces.Office;
        XElement formulaCell = content.Descendants(table + "table-cell").First();
        Assert.Equal("of:=1+2", (string?)formulaCell.Attribute(table + "formula"));
        Assert.Equal("ce1", (string?)formulaCell.Attribute(table + "style-name"));
        Assert.Null(formulaCell.Attribute(office + "value-type"));
        XElement merged = content.Descendants(table + "table-cell")
            .Single(cell => (string?)cell.Attribute(table + "number-columns-spanned") == "2");
        Assert.Equal("2", (string?)merged.Attribute(table + "number-rows-spanned"));
        Assert.Equal(3, content.Descendants(table + "covered-table-cell").Count());
    }

    /// <summary>
    /// 驗證未知樣式與覆蓋非空白儲存格的合併會被拒絕。
    /// </summary>
    [Fact]
    public async Task SparseEditorRejectsUnknownStyleAndDestructiveMerge()
    {
        using (MemoryStream source = CreateSparseFeatureOds())
        using (var destination = new MemoryStream())
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => OdsSparseEditor.ApplyAsync(
                    source,
                    destination,
                    [new OdsCellPatch { SheetName = "Data", Row = 0, Column = 0, StyleName = "missing" }],
                    new OdsSparseEditorOptions(),
                    TestContext.Current.CancellationToken));
        }

        using (MemoryStream source = CreateSparseFeatureOds())
        using (var destination = new MemoryStream())
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => OdsSparseEditor.ApplyAsync(
                    source,
                    destination,
                    [new OdsCellPatch { SheetName = "Data", Row = 0, Column = 0, Formula = "of:=(((1)))" }],
                    new OdsSparseEditorOptions { MaximumFormulaDepth = 2 },
                    TestContext.Current.CancellationToken));
        }

        using (MemoryStream source = CreateSparseFeatureOds())
        using (var destination = new MemoryStream())
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => OdsSparseEditor.ApplyAsync(
                    source,
                    destination,
                    [
                        new OdsCellPatch
                        {
                            SheetName = "Data",
                            Row = 0,
                            Column = 0,
                            RowSpan = 1,
                            ColumnSpan = 2,
                        },
                    ],
                    new OdsSparseEditorOptions(),
                    TestContext.Current.CancellationToken));
        }
    }

    /// <summary>
    /// 驗證串流局部編輯可建立有界 automatic style，並新增、覆寫與移除批注。
    /// </summary>
    [Fact]
    public async Task SparseEditorCreatesAutomaticStyleAndMutatesAnnotation()
    {
        using MemoryStream source = CreateRepeatedOds();
        using var styled = new MemoryStream();
        DateTime annotationDate = new(2026, 7, 29, 8, 30, 0, DateTimeKind.Utc);
        await OdsSparseEditor.ApplyAsync(
            source,
            styled,
            [
                new OdsCellPatch
                {
                    SheetName = "Data",
                    Row = 0,
                    Column = 0,
                    AutomaticStyle = new OdsSparseAutomaticCellStyle
                    {
                        Name = "ceSparse",
                        FontFamily = "Noto Sans",
                        FontSizePoints = 12,
                        Bold = true,
                        TextColor = "#112233",
                        BackgroundColor = "#FFEECC",
                        WrapText = true,
                        HorizontalAlignment = OdsSparseHorizontalAlignment.Center,
                        VerticalAlignment = OdsSparseVerticalAlignment.Middle,
                    },
                    Annotation = new OdfCellAnnotation
                    {
                        Text = "review",
                        Author = "Ada",
                        Date = annotationDate,
                        Visible = true,
                    },
                },
            ],
            new OdsSparseEditorOptions(),
            TestContext.Current.CancellationToken);

        styled.Position = 0;
        using (var archive = new ZipArchive(styled, ZipArchiveMode.Read, leaveOpen: true))
        {
            XDocument content = XDocument.Load(archive.GetEntry("content.xml")!.Open());
            XNamespace table = OdfNamespaces.Table;
            XNamespace office = OdfNamespaces.Office;
            XNamespace style = OdfNamespaces.Style;
            XNamespace text = OdfNamespaces.Text;
            XNamespace dc = OdfNamespaces.Dc;
            XElement automaticStyle = content.Descendants(style + "style")
                .Single(node => (string?)node.Attribute(style + "name") == "ceSparse");
            Assert.Equal("table-cell", (string?)automaticStyle.Attribute(style + "family"));
            XElement cell = content.Descendants(table + "table-cell").First();
            Assert.Equal("ceSparse", (string?)cell.Attribute(table + "style-name"));
            XElement annotation = Assert.Single(cell.Elements(office + "annotation"));
            Assert.Equal("Ada", annotation.Element(dc + "creator")?.Value);
            Assert.Equal("2026-07-29T08:30:00Z", annotation.Element(dc + "date")?.Value);
            Assert.Equal("review", annotation.Element(text + "p")?.Value);
        }

        styled.Position = 0;
        using var removed = new MemoryStream();
        await OdsSparseEditor.ApplyAsync(
            styled,
            removed,
            [new OdsCellPatch { SheetName = "Data", Row = 0, Column = 0, RemoveAnnotation = true }],
            new OdsSparseEditorOptions(),
            TestContext.Current.CancellationToken);
        removed.Position = 0;
        using var removedArchive = new ZipArchive(removed, ZipArchiveMode.Read, leaveOpen: true);
        XDocument removedContent = XDocument.Load(removedArchive.GetEntry("content.xml")!.Open());
        XNamespace removedOffice = OdfNamespaces.Office;
        Assert.Empty(removedContent.Descendants(removedOffice + "annotation"));
    }

    /// <summary>
    /// 驗證串流局部編輯可調整既有合併範圍並在不遺留 covered cell 的情況下解除合併。
    /// </summary>
    [Fact]
    public async Task SparseEditorResizesAndRemovesExistingMerge()
    {
        using MemoryStream source = CreateExistingMergeOds();
        using var resized = new MemoryStream();
        await OdsSparseEditor.ApplyAsync(
            source,
            resized,
            [
                new OdsCellPatch
                {
                    SheetName = "Data",
                    Row = 0,
                    Column = 0,
                    MergeMode = OdsSparseMergeMode.Set,
                    RowSpan = 3,
                    ColumnSpan = 3,
                },
            ],
            new OdsSparseEditorOptions(),
            TestContext.Current.CancellationToken);

        resized.Position = 0;
        using (var archive = new ZipArchive(resized, ZipArchiveMode.Read, leaveOpen: true))
        {
            XDocument content = XDocument.Load(archive.GetEntry("content.xml")!.Open());
            XNamespace table = OdfNamespaces.Table;
            XElement anchor = content.Descendants(table + "table-cell").First();
            Assert.Equal("3", (string?)anchor.Attribute(table + "number-rows-spanned"));
            Assert.Equal("3", (string?)anchor.Attribute(table + "number-columns-spanned"));
            Assert.Equal(8, content.Descendants(table + "covered-table-cell").Count());
        }

        resized.Position = 0;
        using var unmerged = new MemoryStream();
        await OdsSparseEditor.ApplyAsync(
            resized,
            unmerged,
            [
                new OdsCellPatch
                {
                    SheetName = "Data",
                    Row = 0,
                    Column = 0,
                    MergeMode = OdsSparseMergeMode.Remove,
                },
            ],
            new OdsSparseEditorOptions(),
            TestContext.Current.CancellationToken);
        unmerged.Position = 0;
        using var unmergedArchive = new ZipArchive(unmerged, ZipArchiveMode.Read, leaveOpen: true);
        XDocument unmergedContent = XDocument.Load(unmergedArchive.GetEntry("content.xml")!.Open());
        XNamespace unmergedTable = OdfNamespaces.Table;
        XElement unmergedAnchor = unmergedContent.Descendants(unmergedTable + "table-cell").First();
        Assert.Null(unmergedAnchor.Attribute(unmergedTable + "number-rows-spanned"));
        Assert.Null(unmergedAnchor.Attribute(unmergedTable + "number-columns-spanned"));
        Assert.Empty(unmergedContent.Descendants(unmergedTable + "covered-table-cell"));
    }

    /// <summary>
    /// 驗證 automatic style 與批注總文字預算會拒絕不受控輸入。
    /// </summary>
    [Fact]
    public async Task SparseEditorRejectsInvalidAutomaticStyleAndAggregateText()
    {
        using (MemoryStream source = CreateRepeatedOds())
        using (var destination = new MemoryStream())
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => OdsSparseEditor.ApplyAsync(
                    source,
                    destination,
                    [
                        new OdsCellPatch
                        {
                            SheetName = "Data",
                            Row = 0,
                            Column = 0,
                            AutomaticStyle = new OdsSparseAutomaticCellStyle
                            {
                                Name = "ceInvalid",
                                TextColor = "red",
                            },
                        },
                    ],
                    new OdsSparseEditorOptions(),
                    TestContext.Current.CancellationToken));
        }

        using (MemoryStream source = CreateRepeatedOds())
        using (var destination = new MemoryStream())
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => OdsSparseEditor.ApplyAsync(
                    source,
                    destination,
                    [
                        new OdsCellPatch
                        {
                            SheetName = "Data",
                            Row = 0,
                            Column = 0,
                            Text = "1234",
                            Annotation = new OdfCellAnnotation { Text = "5678" },
                        },
                    ],
                    new OdsSparseEditorOptions { MaximumTotalReplacementCharacters = 7 },
                    TestContext.Current.CancellationToken));
        }
    }

    /// <summary>
    /// 驗證富文字 run 的字級會影響欄寬，且 run 預算會停止不受控輸入。
    /// </summary>
    [Fact]
    public void AutoFitMeasuresRichTextRunsAndEnforcesRunBudget()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Runs");
        sheet.Cells[0, 0].SetRichText(
            new OdfRichText().AddRun("Mixed", new OdfRichTextRunOptions { FontSizePoints = 8 }));
        sheet.Cells[0, 1].SetRichText(
            new OdfRichText()
                .AddRun("Mix", new OdfRichTextRunOptions { FontSizePoints = 24, Bold = true })
                .AddRun("ed", new OdfRichTextRunOptions { FontSizePoints = 24, Italic = true }));

        IReadOnlyDictionary<int, OdfLength> widths = sheet.AutoFitColumnWidths(
            [0, 1],
            new OdfAutoFitOptions(),
            TestContext.Current.CancellationToken);

        Assert.True(widths[1].ToCentimeters() > widths[0].ToCentimeters());
        Assert.Throws<InvalidOperationException>(
            () => sheet.AutoFitColumnWidth(
                1,
                new OdfAutoFitOptions { MaximumRichTextRuns = 1 },
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 驗證樞紐刷新會依列欄群組物化加總結果。
    /// </summary>
    [Fact]
    public void PivotRefreshMaterializesBoundedAggregate()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.Cells[0, 0].SetValue("Category");
        sheet.Cells[0, 1].SetValue("Region");
        sheet.Cells[0, 2].SetValue("Sales");
        sheet.Cells[1, 0].SetValue("A");
        sheet.Cells[1, 1].SetValue("North");
        sheet.Cells[1, 2].SetValue(10);
        sheet.Cells[2, 0].SetValue("A");
        sheet.Cells[2, 1].SetValue("North");
        sheet.Cells[2, 2].SetValue(15);
        sheet.Cells[3, 0].SetValue("B");
        sheet.Cells[3, 1].SetValue("South");
        sheet.Cells[3, 2].SetValue(7);

        OdfPivotRefreshResult result = new OdfPivotTableBuilder(
                "P",
                new OdfCellRange(0, 0, 3, 2, "Data"),
                new OdfCellAddress(6, 0, "Data"),
                sheet)
            .AddRowField("Category")
            .AddColumnField("Region")
            .AddDataField("Sales")
            .Refresh();

        Assert.Equal(2, result.GroupCount);
        Assert.Equal(25d, sheet.Cells[7, 1].CellValue);
        Assert.Equal(7d, sheet.Cells[8, 2].CellValue);
    }

    /// <summary>
    /// 驗證樞紐刷新會逐來源列計算計算欄位，且支援計算欄位相依性。
    /// </summary>
    [Fact]
    public void PivotRefreshCalculatesDependentFormulaFields()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.Cells[0, 0].SetValue("Category");
        sheet.Cells[0, 1].SetValue("Revenue");
        sheet.Cells[0, 2].SetValue("Cost");
        sheet.Cells[1, 0].SetValue("A");
        sheet.Cells[1, 1].SetValue(100);
        sheet.Cells[1, 2].SetValue(40);
        sheet.Cells[2, 0].SetValue("A");
        sheet.Cells[2, 1].SetValue(80);
        sheet.Cells[2, 2].SetValue(40);

        OdfPivotRefreshResult result = new OdfPivotTableBuilder(
                "P",
                new OdfCellRange(0, 0, 2, 2, "Data"),
                new OdfCellAddress(5, 0, "Data"),
                sheet)
            .AddRowField("Category")
            .AddCalculatedField("Margin", "of:=ROUND([.Profit]/[.Revenue]*100; 2)")
            .AddCalculatedField("Profit", "of:=[.Revenue]-[.Cost]")
            .Refresh();

        Assert.Equal(1, result.GroupCount);
        Assert.Equal(110d, sheet.Cells[6, 1].CellValue);
        Assert.Equal(100d, sheet.Cells[6, 2].CellValue);
    }

    /// <summary>
    /// 驗證樞紐計算欄位會拒絕循環相依及超出求值預算的工作。
    /// </summary>
    [Fact]
    public void PivotRefreshRejectsFormulaCyclesAndEvaluationOverBudget()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.Cells[0, 0].SetValue("Value");
        sheet.Cells[1, 0].SetValue(1);
        sheet.Cells[2, 0].SetValue(2);
        var cyclic = new OdfPivotTableBuilder(
                "P",
                new OdfCellRange(0, 0, 1, 0, "Data"),
                new OdfCellAddress(4, 0, "Data"),
                sheet)
            .AddCalculatedField("A", "of:=[.B]+1")
            .AddCalculatedField("B", "of:=[.A]+1");
        Assert.Throws<InvalidDataException>(() => cyclic.Refresh());

        var bounded = new OdfPivotTableBuilder(
                "P2",
                new OdfCellRange(0, 0, 2, 0, "Data"),
                new OdfCellAddress(8, 0, "Data"),
                sheet)
            .AddCalculatedField("A", "of:=[.Value]+1");
        Assert.Throws<InvalidOperationException>(
            () => bounded.Refresh(
                new OdfPivotRefreshOptions { MaximumFormulaEvaluations = 1 },
                TestContext.Current.CancellationToken));

        Assert.Throws<InvalidOperationException>(
            () => bounded.Refresh(
                new OdfPivotRefreshOptions { MaximumOutputCells = 2 },
                TestContext.Current.CancellationToken));
    }

    private static MemoryStream CreateRepeatedOds(string documentType = "")
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "mimetype", "application/vnd.oasis.opendocument.spreadsheet", CompressionLevel.NoCompression);
            WriteEntry(
                archive,
                "content.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                """ + documentType +
                """
                <office:document-content
                  xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                  xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                  xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                  <office:body><office:spreadsheet>
                    <table:table table:name="Data">
                      <table:table-row table:number-rows-repeated="5">
                        <table:table-cell table:number-columns-repeated="6" office:value-type="string"><text:p>old</text:p></table:table-cell>
                      </table:table-row>
                    </table:table>
                  </office:spreadsheet></office:body>
                </office:document-content>
                """,
                CompressionLevel.Optimal);
            WriteEntry(archive, "custom.bin", "preserved", CompressionLevel.NoCompression);
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateSparseFeatureOds()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "mimetype", "application/vnd.oasis.opendocument.spreadsheet", CompressionLevel.NoCompression);
            WriteEntry(
                archive,
                "styles.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <office:document-styles
                  xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                  xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0">
                  <office:styles>
                    <style:style style:name="ce1" style:family="table-cell"/>
                  </office:styles>
                </office:document-styles>
                """,
                CompressionLevel.Optimal);
            WriteEntry(
                archive,
                "content.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <office:document-content
                  xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                  xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                  xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                  <office:body><office:spreadsheet>
                    <table:table table:name="Data">
                      <table:table-row>
                        <table:table-cell office:value-type="string"><text:p>old</text:p></table:table-cell>
                        <table:table-cell office:value-type="string"><text:p>occupied</text:p></table:table-cell>
                        <table:table-cell/>
                      </table:table-row>
                      <table:table-row table:number-rows-repeated="2">
                        <table:table-cell table:number-columns-repeated="3"/>
                      </table:table-row>
                    </table:table>
                  </office:spreadsheet></office:body>
                </office:document-content>
                """,
                CompressionLevel.Optimal);
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateExistingMergeOds()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "mimetype", "application/vnd.oasis.opendocument.spreadsheet", CompressionLevel.NoCompression);
            WriteEntry(
                archive,
                "content.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <office:document-content
                  xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                  xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                  xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                  <office:body><office:spreadsheet>
                    <table:table table:name="Data">
                      <table:table-row>
                        <table:table-cell table:number-rows-spanned="2" table:number-columns-spanned="2"
                          office:value-type="string"><text:p>merged</text:p></table:table-cell>
                        <table:covered-table-cell/>
                        <table:table-cell table:number-columns-repeated="2"/>
                      </table:table-row>
                      <table:table-row>
                        <table:covered-table-cell table:number-columns-repeated="2"/>
                        <table:table-cell table:number-columns-repeated="2"/>
                      </table:table-row>
                      <table:table-row>
                        <table:table-cell table:number-columns-repeated="4"/>
                      </table:table-row>
                    </table:table>
                  </office:spreadsheet></office:body>
                </office:document-content>
                """,
                CompressionLevel.Optimal);
        }
        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        string value,
        CompressionLevel compression)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, compression);
        using Stream output = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        output.Write(bytes, 0, bytes.Length);
    }
}

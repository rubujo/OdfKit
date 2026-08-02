using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

public partial class OptimizedRefactoringTests
{
    /// <summary>
    /// 驗證一般 ODS Stream 載入會直接重用 entry UTF-8 緩衝區，並將大型工作表維持為 lazy slice。
    /// </summary>
    [Fact]
    public void SpreadsheetStreamLoadKeepsLargeSheetLazyUntilCellTraversal()
    {
        using MemoryStream package = CreateLargeLazyOdsPackage();
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large.ods");

        TableTableElement table = GetOnlyTable(document);
        Assert.True(table._isLazy);
        Assert.True(table._lazyXmlMemory.Length > 8192);
        Assert.Equal(0, table.Children.LoadedCount);

        Assert.Equal("Sheet 0 Row 0", document.Worksheets[0].GetCell(0, 0).CellValue);

        Assert.False(table._isLazy);
        Assert.True(table._lazyXmlMemory.IsEmpty);
        Assert.Equal(260, table.TableTableRowChildElements.Count());
    }

    /// <summary>
    /// 驗證未預先存取的大型工作表仍可由完整儲存管線正確具現化並完成 round-trip。
    /// </summary>
    [Fact]
    public void SpreadsheetStreamLoadRoundTripsPreviouslyUntouchedLazySheet()
    {
        using MemoryStream package = CreateLargeLazyOdsPackage();
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large.ods");
        TableTableElement table = GetOnlyTable(document);

        using var output = new MemoryStream();
        document.SaveToStream(output);

        Assert.False(table._isLazy);
        Assert.Equal(260, table.Children.Count);
        Assert.True(output.Length > 0);
    }

    /// <summary>
    /// 驗證讀取最後一張工作表時不會連帶具現化前面的工作表。
    /// </summary>
    [Fact]
    public void ReadingLastSheetDoesNotMaterializeEarlierSheets()
    {
        using MemoryStream package = CreateLargeLazyOdsPackage(sheetCount: 3);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large.ods");
        TableTableElement[] tables = GetTables(document);

        Assert.All(tables, table => Assert.True(table._isLazy));

        Assert.Equal("Sheet 2 Row 0", document.Worksheets[2].GetCell(0, 0).CellValue);

        Assert.True(tables[0]._isLazy);
        Assert.True(tables[1]._isLazy);
        Assert.False(tables[2]._isLazy);
    }

    /// <summary>
    /// 驗證多執行緒同時首次存取工作表時只會具現化一次，且不會公開半完成的子節點清單。
    /// </summary>
    [Fact]
    public void LazySheetConcurrentFirstAccessMaterializesExactlyOnce()
    {
        using MemoryStream package = CreateLargeLazyOdsPackage();
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large.ods");
        TableTableElement table = GetOnlyTable(document);

        Parallel.For(0, 8, _ => Assert.Equal(260, table.Children.Count));

        Assert.False(table._isLazy);
        Assert.Equal(260, table.Children.Count);
        Assert.Equal(260, table.TableTableRowChildElements.Count());
    }

    private static TableTableElement GetOnlyTable(SpreadsheetDocument document)
        => GetTables(document).Single();

    private static TableTableElement[] GetTables(SpreadsheetDocument document)
    {
        OfficeDocumentContentElement content = Assert.IsType<OfficeDocumentContentElement>(document.ContentDom);
        return content.OfficeBodyChildElements
            .Single()
            .OfficeSpreadsheetChildElements
            .Single()
            .TableTableChildElements
            .ToArray();
    }

    private static MemoryStream CreateLargeLazyOdsPackage(int sheetCount = 1)
    {
        string tables = string.Concat(Enumerable.Range(0, sheetCount).Select(sheetIndex =>
        {
            string rows = string.Concat(Enumerable.Range(0, 260).Select(rowIndex =>
                $"<table:table-row><table:table-cell office:value-type=\"string\"><text:p>Sheet {sheetIndex} Row {rowIndex}</text:p></table:table-cell></table:table-row>"));
            return $"<table:table table:name=\"Sheet{sheetIndex + 1}\">{rows}</table:table>";
        }));
        string content = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                office:version="1.4">
              <office:body><office:spreadsheet>{{tables}}</office:spreadsheet></office:body>
            </office:document-content>
            """;
        string manifest = """
            <?xml version="1.0" encoding="UTF-8"?>
            <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.4">
              <manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet" manifest:version="1.4" />
              <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml" />
            </manifest:manifest>
            """;

        return CreateZipPackage(
            ("mimetype", Encoding.ASCII.GetBytes("application/vnd.oasis.opendocument.spreadsheet")),
            ("content.xml", Encoding.UTF8.GetBytes(content)),
            ("META-INF/manifest.xml", Encoding.UTF8.GetBytes(manifest)));
    }
}

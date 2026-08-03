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
    /// 驗證未預先存取的大型工作表可直接複製 UTF-8 payload，無須具現化即可完成 round-trip。
    /// </summary>
    [Fact]
    public void SpreadsheetStreamLoadRoundTripsUntouchedLazySheetWithoutMaterialization()
    {
        using MemoryStream package = CreateLargeLazyOdsPackage();
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large.ods");
        TableTableElement table = GetOnlyTable(document);

        using var output = new MemoryStream();
        document.SaveToStream(output);

        Assert.True(table._isLazy);
        Assert.Equal(0, table.Children.LoadedCount);
        Assert.True(output.Length > 0);

        output.Position = 0;
        using SpreadsheetDocument reopened = SpreadsheetDocument.Load(output, "roundtrip.ods");
        Assert.Equal("Sheet 0 Row 0", reopened.Worksheets[0].GetCell(0, 0).CellValue);
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

    /// <summary>
    /// 驗證多執行緒同時建立同一儲存格時，列／格索引快取與 DOM 只發布同一節點。
    /// </summary>
    [Fact]
    public void ConcurrentRandomCellAccessPublishesSingleNode()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        OdfNode?[] nodes = new OdfNode[32];

        Parallel.For(0, nodes.Length, index => nodes[index] = sheet.GetCell(128, 64).Node);

        Assert.All(nodes, node => Assert.Same(nodes[0], node));
    }

    /// <summary>
    /// 驗證大型壓縮 XML entry 使用匿名 MMF 作為 lazy backing，而非保留大型 LOH 陣列。
    /// </summary>
    [Fact]
    public void LargeCompressedContentUsesMemoryMappedLazyBacking()
    {
        string body = $"<office:spreadsheet><table:table table:name=\"Sheet1\"><table:table-row><table:table-cell><text:p>{new string('x', 5 * 1024 * 1024)}</text:p></table:table-cell></table:table-row></table:table></office:spreadsheet>";
        string mimeType = "application/vnd.oasis.opendocument.spreadsheet";
        string content = BuildDocumentContent(string.Empty, body);
        using var package = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(package, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            System.IO.Compression.ZipArchiveEntry mimeEntry = archive.CreateEntry("mimetype", System.IO.Compression.CompressionLevel.NoCompression);
            using (Stream stream = mimeEntry.Open())
                stream.Write(Encoding.ASCII.GetBytes(mimeType));

            System.IO.Compression.ZipArchiveEntry contentEntry = archive.CreateEntry("content.xml", System.IO.Compression.CompressionLevel.Optimal);
            using (Stream stream = contentEntry.Open())
                stream.Write(Encoding.UTF8.GetBytes(content));

            const string manifest = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.spreadsheet\" manifest:version=\"1.4\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>";
            System.IO.Compression.ZipArchiveEntry manifestEntry = archive.CreateEntry("META-INF/manifest.xml", System.IO.Compression.CompressionLevel.Optimal);
            using (Stream stream = manifestEntry.Open())
                stream.Write(Encoding.UTF8.GetBytes(manifest));
        }

        package.Position = 0;
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large-mmf.ods");
        OdfPackageEntry entry = Assert.IsType<OdfPackageEntry>(document.Package.GetEntry("content.xml"));
        Assert.True(entry.CanExposeMmfPointer);
        Assert.NotEqual(IntPtr.Zero, entry.GetMmfPointer(out int entryLength));
        Assert.True(entryLength > 5 * 1024 * 1024);
        TableTableElement table = GetOnlyTable(document);

        Assert.True(table._isLazy);
        Assert.NotEqual(IntPtr.Zero, table._lazyXmlPtr);
        Assert.True(table._lazyXmlMemory.IsEmpty);
        string text = document.Worksheets[0].GetCell(0, 0).DisplayText;
        Assert.Equal(5 * 1024 * 1024, text.Length);
        Assert.Equal('x', text[0]);
        Assert.Equal('x', text[^1]);
    }

    /// <summary>
    /// 驗證同一文件的並行非同步儲存會序列化封裝存取，且兩份輸出都可重新載入。
    /// </summary>
    [Fact]
    public async Task ConcurrentAsyncSavesAreSerializedAndValid()
    {
        using MemoryStream package = CreateLargeLazyOdsPackage();
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large.ods");
        using var first = new MemoryStream();
        using var second = new MemoryStream();

        await Task.WhenAll(
            document.SaveToStreamAsync(first, TestContext.Current.CancellationToken),
            document.SaveToStreamAsync(second, TestContext.Current.CancellationToken));

        first.Position = 0;
        second.Position = 0;
        using SpreadsheetDocument firstDocument = SpreadsheetDocument.Load(first, "first.ods");
        using SpreadsheetDocument secondDocument = SpreadsheetDocument.Load(second, "second.ods");
        Assert.Equal("Sheet 0 Row 0", firstDocument.Worksheets[0].GetCell(0, 0).CellValue);
        Assert.Equal("Sheet 0 Row 0", secondDocument.Worksheets[0].GetCell(0, 0).CellValue);
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

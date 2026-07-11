using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies symmetric high-level CRUD workflows for the primary ODF formats.
/// 驗證主要 ODF 格式的對稱高階 CRUD 工作流程。
/// </summary>
public class PrimaryFormatCrudCompletionTests
{
    /// <summary>
    /// Gets the legacy-version primary-format compatibility cases.
    /// 取得舊版本主要格式相容性案例。
    /// </summary>
    public static IEnumerable<object[]> LegacyVersionCases()
    {
        OdfVersion[] versions = [OdfVersion.Odf11, OdfVersion.Odf12, OdfVersion.Odf13];
        OdfDocumentKind[] kinds =
            [OdfDocumentKind.Text, OdfDocumentKind.Spreadsheet, OdfDocumentKind.Presentation, OdfDocumentKind.Graphics];
        foreach (OdfVersion version in versions)
        {
            foreach (OdfDocumentKind kind in kinds)
            {
                yield return [version, kind];
            }
        }
    }

    /// <summary>
    /// Verifies top-level ODT collections support find, remove, clear, and round-trip operations.
    /// 驗證最上層 ODT 集合支援查找、移除、清除與 round-trip。
    /// </summary>
    [Fact]
    public void OdtCollections_FindRemoveClearAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph firstParagraph = document.Body.Paragraphs.Add("保留段落");
        OdfParagraph removedParagraph = document.Body.Paragraphs.Add("移除段落");
        OdfHeading heading = document.Body.Headings.Add("章節", 1);
        OdfList list = document.Body.Lists.Add("ListStyle");
        list.AddItem("項目");
        OdfTable table = document.Body.Tables.Add(1, 1);
        table.Name = "SummaryTable";

        Assert.Same(removedParagraph.Node, document.Body.Paragraphs.Find(p => p.TextContent == "移除段落")!.Node);
        Assert.True(document.Body.Paragraphs.Remove(removedParagraph));
        Assert.False(document.Body.Paragraphs.Remove(removedParagraph));
        Assert.Same(heading.Node, document.Body.Headings.Find(h => h.TextContent == "章節")!.Node);
        document.Body.Headings.Clear();
        Assert.Empty(document.Body.Headings);
        Assert.Same(list.Node, document.Body.Lists.Find(candidate => candidate.StyleName == "ListStyle")!.Node);
        Assert.True(document.Body.Lists.Remove(list));
        OdfTextTableInfo tableInfo = document.Body.Tables.Find("SummaryTable")!;
        Assert.True(document.Body.Tables.Remove(tableInfo));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "crud.odt");
        Assert.Single(reloaded.Body.Paragraphs);
        Assert.Equal(firstParagraph.TextContent, reloaded.Body.Paragraphs.Items[0].TextContent);
        Assert.Empty(reloaded.Body.Headings);
        Assert.Empty(reloaded.Body.Lists);
        Assert.Empty(reloaded.Body.Tables);
    }

    /// <summary>
    /// Verifies ODS worksheet removal refuses dangling formula references and round-trips after references are cleared.
    /// 驗證 ODS 工作表移除會拒絕懸空公式參照，並在清除參照後可 round-trip。
    /// </summary>
    [Fact]
    public void OdsWorksheets_RemovePreservesFormulaIntegrityAndRoundTrips()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet data = document.Worksheets.Add("Data");
        data.Cells["A1"].CellValue = 10d;
        OdfTableSheet summary = document.Worksheets.Add("Summary");
        summary.Cells["A1"].Formula = "of:=[Data.A1]";

        Assert.False(document.Worksheets.TryRemove("Data", out IReadOnlyList<OdfFormulaCellInfo> blockers));
        OdfFormulaCellInfo blocker = Assert.Single(blockers);
        Assert.Equal("Summary", blocker.SheetName);
        Assert.Same(data, document.Worksheets.Find("Data"));

        summary.Cells["A1"].Formula = string.Empty;
        Assert.True(document.Worksheets.Remove(data));
        Assert.False(document.Worksheets.Remove(data));
        Assert.Null(document.Worksheets.Find("Data"));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "crud.ods");
        Assert.Single(reloaded.Worksheets);
        Assert.Equal("Summary", reloaded.Worksheets[0].Name);
    }

    /// <summary>
    /// Verifies worksheet-scoped named ranges and expressions support symmetric find, remove, clear, and round-trip operations.
    /// 驗證工作表範圍的命名範圍與具名運算式支援對稱的查找、移除、清除及 round-trip 操作。
    /// </summary>
    [Fact]
    public void OdsWorksheetNames_FindRemoveClearAndRoundTrip()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        var keepRange = new OdfCellRange(0, 0, 1, 0, "Data");
        var removeRange = new OdfCellRange(0, 1, 1, 1, "Data");
        sheet.AddNamedRange("KeepRange", keepRange);
        sheet.AddNamedRange("RemoveRange", removeRange);
        sheet.AddNamedExpression("KeepExpression", "of:=[.A1]+1");
        sheet.AddNamedExpression("RemoveExpression", "of:=[.B1]+1");

        Assert.Equal(removeRange.ToOdfString(false), sheet.FindNamedRange("RemoveRange")!.CellRangeAddress);
        Assert.True(sheet.RemoveNamedRange("RemoveRange"));
        Assert.False(sheet.RemoveNamedRange("RemoveRange"));
        Assert.Equal("of:=[.B1]+1", sheet.FindNamedExpression("RemoveExpression")!.Expression);
        Assert.True(sheet.RemoveNamedExpression("RemoveExpression"));
        Assert.False(sheet.RemoveNamedExpression("RemoveExpression"));
        Assert.Equal(1, sheet.ClearNamedRanges());
        Assert.Empty(sheet.NamedRanges);
        Assert.Single(sheet.NamedExpressions);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "names.ods");
        OdfTableSheet reloadedSheet = reloaded.Worksheets[0];
        Assert.Empty(reloadedSheet.NamedRanges);
        Assert.Equal("KeepExpression", Assert.Single(reloadedSheet.NamedExpressions).Name);
        Assert.Equal(1, reloadedSheet.ClearNamedExpressions());
        Assert.Empty(reloadedSheet.NamedExpressions);
    }

    /// <summary>
    /// Verifies database ranges support editable lookup, identity-safe removal, selective clear, and round-trip preservation.
    /// 驗證資料庫範圍支援可編輯查找、識別安全移除、選擇性清除及 round-trip 保留。
    /// </summary>
    [Fact]
    public void OdsDatabaseRanges_FindRemoveClearAndRoundTrip()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.Worksheets.Add("Data");
        OdfDatabaseRange first = document.AddDatabaseRange("First", new OdfCellRange(0, 0, 2, 1, "Data"));
        OdfDatabaseRange second = document.AddDatabaseRange("Second", new OdfCellRange(0, 2, 2, 3, "Data"));
        document.AddNamedRange("First", new OdfCellRange(0, 0, 2, 1, "Data"));
        const string foreignNamespace = "urn:odfkit:test:database-range-foreign";
        var foreign = new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign");
        first.Node.Parent!.AppendChild(foreign);

        OdfDatabaseRange found = document.FindDatabaseRange("Second")!;
        found.DisplayFilterButtons = true;
        Assert.True(document.RemoveDatabaseRange(second));
        Assert.False(document.RemoveDatabaseRange(second));
        Assert.Null(document.FindDatabaseRange("Second"));
        Assert.Equal(1, document.ClearDatabaseRanges());
        Assert.Empty(document.GetDatabaseRanges());
        Assert.Single(document.GetNamedRanges());

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "database-ranges.ods");
        Assert.Empty(reloaded.GetDatabaseRanges());
        Assert.Equal("First", Assert.Single(reloaded.GetNamedRanges()).Name);
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies ODF 1.1 through 1.3 primary documents use the same high-level model and preserve foreign content.
    /// 驗證 ODF 1.1～1.3 主要文件使用相同高階模型，並保留 foreign content。
    /// </summary>
    /// <param name="version">The source and target ODF version. / 來源與目標 ODF 版本。</param>
    /// <param name="kind">The primary document kind. / 主要文件種類。</param>
    [Theory]
    [MemberData(nameof(LegacyVersionCases))]
    public void LegacyVersions_HighLevelMutationPreservesVersionAndForeignContent(
        OdfVersion version,
        OdfDocumentKind kind)
    {
        using var stream = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(stream, kind, version, leaveOpen: true);
        using OdfDocument document = CreatePrimaryDocument(package, kind);
        document.TargetVersion = version;
        AddPrimaryContent(document, kind);

        const string foreignNamespace = "urn:odfkit:test:semantic-foreign";
        var foreign = new OdfNode(OdfNodeType.Element, "semantic-marker", foreignNamespace, "foreign");
        foreign.SetAttribute("value", foreignNamespace, "preserved", "foreign");
        document.ContentRoot.AppendChild(foreign);
        document.Save();

        Assert.Equal(version, package.Version);
        stream.Position = 0;
        using OdfDocument reloaded = LoadPrimaryDocument(stream, kind);
        Assert.Equal(version, reloaded.Package.Version);
        OdfNode? marker = FindDescendant(reloaded.ContentRoot, "semantic-marker", foreignNamespace);
        Assert.NotNull(marker);
        Assert.Equal("preserved", marker.GetAttribute("value", foreignNamespace));
    }

    private static OdfDocument CreatePrimaryDocument(OdfPackage package, OdfDocumentKind kind) => kind switch
    {
        OdfDocumentKind.Text => new TextDocument(package),
        OdfDocumentKind.Spreadsheet => new SpreadsheetDocument(package),
        OdfDocumentKind.Presentation => new PresentationDocument(package),
        OdfDocumentKind.Graphics => new DrawingDocument(package),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static OdfDocument LoadPrimaryDocument(Stream stream, OdfDocumentKind kind) => kind switch
    {
        OdfDocumentKind.Text => TextDocument.Load(stream, "legacy.odt"),
        OdfDocumentKind.Spreadsheet => SpreadsheetDocument.Load(stream, "legacy.ods"),
        OdfDocumentKind.Presentation => PresentationDocument.Load(stream, "legacy.odp"),
        OdfDocumentKind.Graphics => DrawingDocument.Load(stream, "legacy.odg"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void AddPrimaryContent(OdfDocument document, OdfDocumentKind kind)
    {
        switch (kind)
        {
            case OdfDocumentKind.Text:
                ((TextDocument)document).Body.Paragraphs.Add("legacy-text");
                break;
            case OdfDocumentKind.Spreadsheet:
                ((SpreadsheetDocument)document).Worksheets.Add("Legacy").Cells["A1"].CellValue = "legacy-sheet";
                break;
            case OdfDocumentKind.Presentation:
                ((PresentationDocument)document).Slides.Add("Legacy").AddTextBox(
                    OdfKit.Styles.OdfLength.Parse("1cm"),
                    OdfKit.Styles.OdfLength.Parse("1cm"),
                    OdfKit.Styles.OdfLength.Parse("4cm"),
                    OdfKit.Styles.OdfLength.Parse("2cm"),
                    "legacy-slide");
                break;
            case OdfDocumentKind.Graphics:
                ((DrawingDocument)document).Pages.Add("Legacy").AddTextBox(
                    OdfKit.Styles.OdfLength.Parse("1cm"),
                    OdfKit.Styles.OdfLength.Parse("1cm"),
                    OdfKit.Styles.OdfLength.Parse("4cm"),
                    OdfKit.Styles.OdfLength.Parse("2cm"),
                    "legacy-drawing");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static OdfNode? FindDescendant(OdfNode root, string localName, string namespaceUri)
    {
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == namespaceUri)
            {
                return child;
            }

            OdfNode? descendant = FindDescendant(child, localName, namespaceUri);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}

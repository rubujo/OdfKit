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
    /// Verifies database filters and sorts support replacement, clearing, and round-trip preservation.
    /// 驗證資料庫篩選與排序支援取代、清除及 round-trip 保留。
    /// </summary>
    [Fact]
    public void OdsFiltersAndSorts_CompleteLifecycleAndRoundTrip()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[,]
            {
                { "Name", "Amount" },
                { "A", 10d },
                { "B", 20d },
            });
        OdfSpreadsheetTable table = document.CreateTable(
            "Sales",
            new OdfCellRange(0, 0, 2, 1, "Data"));
        table.ApplyFilter(new OdfDatabaseFilterConditionInfo(1, ">", "10"));
        table.ApplySort(new OdfDatabaseSortRuleInfo(0, ascending: true));
        table.ApplyFilter(new OdfDatabaseFilterConditionInfo(1, "<=", "20"));
        table.ApplySort(new OdfDatabaseSortRuleInfo(1, ascending: false));

        OdfDatabaseRangeInfo range = Assert.Single(document.GetDatabaseRanges());
        Assert.Contains(range.FilterConditions, condition => condition.Operator == "<=" && condition.Value == "20");
        Assert.Contains(range.SortRules, rule => rule.FieldNumber == 1 && !rule.Ascending);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "filters.ods");
        OdfSpreadsheetTable reloadedTable = reloaded.FindTable("Sales")!;
        OdfDatabaseRangeInfo reloadedRange = Assert.Single(reloaded.GetDatabaseRanges());
        Assert.Single(reloadedRange.FilterConditions);
        Assert.Single(reloadedRange.SortRules);
        reloadedTable.ClearFilter();
        reloadedTable.ClearSort();
        Assert.Empty(Assert.Single(reloaded.GetDatabaseRanges()).FilterConditions);
        Assert.Empty(Assert.Single(reloaded.GetDatabaseRanges()).SortRules);
        Assert.True(reloaded.RemoveDatabaseRange("Sales"));
        Assert.False(reloaded.RemoveDatabaseRange("Sales"));
        Assert.Equal(0, reloaded.ClearDatabaseRanges());
        Assert.Empty(reloaded.GetDatabaseRanges());

        using var clearedStream = new MemoryStream();
        reloaded.SaveToStream(clearedStream);
        clearedStream.Position = 0;
        using SpreadsheetDocument cleared = SpreadsheetDocument.Load(clearedStream, "filters-cleared.ods");
        Assert.Empty(cleared.GetDatabaseRanges());
    }

    /// <summary>
    /// Verifies frozen and split panes support find, replacement, clearing, and round-trip.
    /// 驗證凍結與分割窗格支援查找、取代、清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdsFrozenAndSplitPanes_CompleteLifecycleAndRoundTrip()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet frozenSheet = document.Worksheets.Add("Frozen");
        OdfTableSheet splitSheet = document.Worksheets.Add("Split");
        frozenSheet.FreezePanes(1, 2);
        splitSheet.SplitPanes(3, 4);
        frozenSheet.FreezePanes(2, 1);
        splitSheet.SplitPanes(4, 2);
        const string foreignNamespace = "urn:odfkit:test:view-foreign";
        document.SettingsDom.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        OdfSheetFrozenPanesInfo frozen = document.FindFrozenPanes("Frozen")!;
        OdfSheetSplitPanesInfo split = document.FindSplitPanes("Split")!;
        Assert.Equal(2, frozen.FrozenPanes.Rows);
        Assert.Equal(1, frozen.FrozenPanes.Columns);
        Assert.Equal(4, split.SplitPanes.Rows);
        Assert.Equal(2, split.SplitPanes.Columns);
        Assert.Null(document.FindFrozenPanes("Missing"));
        Assert.Null(document.FindSplitPanes("Missing"));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "views.ods");
        Assert.NotNull(reloaded.FindFrozenPanes("Frozen"));
        Assert.NotNull(reloaded.FindSplitPanes("Split"));
        Assert.True(reloaded.FindSheet("Frozen")!.ClearFrozenPanes());
        Assert.False(reloaded.FindSheet("Frozen")!.ClearFrozenPanes());
        Assert.True(reloaded.FindSheet("Split")!.ClearSplitPanes());
        Assert.False(reloaded.FindSheet("Split")!.ClearSplitPanes());
        Assert.Empty(reloaded.GetFrozenPanes());
        Assert.Empty(reloaded.GetSplitPanes());
        Assert.NotNull(FindDescendant(reloaded.SettingsDom, "extension", foreignNamespace));

        using var clearedStream = new MemoryStream();
        reloaded.SaveToStream(clearedStream);
        clearedStream.Position = 0;
        using SpreadsheetDocument cleared = SpreadsheetDocument.Load(clearedStream, "views-cleared.ods");
        Assert.Empty(cleared.GetFrozenPanes());
        Assert.Empty(cleared.GetSplitPanes());
        Assert.NotNull(FindDescendant(cleared.SettingsDom, "extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies data validation removal detaches cell references and preserves unknown container content through round-trip.
    /// 驗證移除資料驗證時會解除儲存格引用，並在 round-trip 後保留容器中的未知內容。
    /// </summary>
    [Fact]
    public void OdsDataValidations_FindRemoveClearAndRoundTrip()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        document.AddDataValidation("Data", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 0, 0, 0, "Data"),
            Condition = OdfValidationCondition.IntegerBetween,
            Formula1 = "1",
            Formula2 = "10",
            ErrorMessage = "Out of range",
        });
        document.AddDataValidation("Data", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 1, 0, 1, "Data"),
            Condition = OdfValidationCondition.DecimalBetween,
            Formula1 = "0",
            Formula2 = "1",
        });

        const string foreignNamespace = "urn:odfkit:test:validation-foreign";
        OdfNode validations = FindDescendant(document.ContentRoot, "content-validations", OdfNamespaces.Table)!;
        validations.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        OdfDataValidationInfo first = document.FindDataValidation("val_1")!;
        Assert.Single(first.AppliedRanges);
        Assert.True(document.UpdateDataValidation("val_2", "Data", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 2, 0, 2, "Data"),
            Condition = OdfValidationCondition.TextLengthBetween,
            Formula1 = "2",
            Formula2 = "20",
            ErrorMessage = "Invalid length",
            ErrorTitle = "Length",
            AlertStyle = OdfValidationAlertStyle.Warning,
        }));
        Assert.False(document.UpdateDataValidation("missing", "Data", new OdfDataValidation()));
        Assert.Null(sheet.Cells[0, 1].Node.GetAttribute("content-validation-name", OdfNamespaces.Table));
        Assert.Equal("val_2", sheet.Cells[0, 2].Node.GetAttribute("content-validation-name", OdfNamespaces.Table));
        OdfDataValidationInfo updated = document.FindDataValidation("val_2")!;
        Assert.Equal("Length", updated.ErrorTitle);
        Assert.Equal("warning", updated.AlertStyle);
        Assert.True(updated.TryGetCondition(out OdfValidationCondition updatedCondition));
        Assert.Equal(OdfValidationCondition.TextLengthBetween, updatedCondition);
        Assert.True(document.RemoveDataValidation("val_1"));
        Assert.False(document.RemoveDataValidation("val_1"));
        Assert.Null(sheet.Cells[0, 0].Node.GetAttribute("content-validation-name", OdfNamespaces.Table));
        Assert.Equal("val_2", sheet.Cells[0, 2].Node.GetAttribute("content-validation-name", OdfNamespaces.Table));
        Assert.Equal(1, document.ClearDataValidations());
        Assert.Empty(document.GetDataValidations());
        Assert.Null(sheet.Cells[0, 2].Node.GetAttribute("content-validation-name", OdfNamespaces.Table));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "validations.ods");
        Assert.Empty(reloaded.GetDataValidations());
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies conditional formats and sparkline groups support semantic lookup, removal, selective clear, and round-trip.
    /// 驗證條件格式與走勢圖群組支援語意查找、移除、選擇性清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdsConditionalFormatsAndSparklines_FindRemoveClearAndRoundTrip()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.AddConditionalFormat(new OdfCellRange(0, 0, 2, 0), "cell-content()>5", "Good");
        sheet.AddConditionalFormat(new OdfCellRange(0, 0, 2, 0), "cell-content()>5", "Good");
        sheet.AddDataBarFormat(new OdfCellRange(0, 1, 2, 1), new OdfColor("#4472C4"));
        sheet.AddSparklineGroup(
            new OdfCellRange(0, 0, 0, 2, "Data"),
            new OdfCellAddress(0, 3, "Data"),
            SparklineType.Line);
        sheet.AddSparklineGroup(
            new OdfCellRange(1, 0, 1, 2, "Data"),
            new OdfCellAddress(1, 3, "Data"),
            SparklineType.Column);

        const string foreignNamespace = "urn:odfkit:test:calcext-foreign";
        OdfNode formats = FindDescendant(sheet.TableNode, "conditional-formats", OdfNamespaces.CalcExt)!;
        OdfNode groups = FindDescendant(sheet.TableNode, "sparkline-groups", OdfNamespaces.CalcExt)!;
        formats.AppendChild(new OdfNode(OdfNodeType.Element, "format-extension", foreignNamespace, "foreign"));
        groups.AppendChild(new OdfNode(OdfNodeType.Element, "sparkline-extension", foreignNamespace, "foreign"));

        OdfConditionalFormatInfo condition = sheet.FindConditionalFormat(
            candidate => candidate.Kind == OdfConditionalFormatKind.Condition)!;
        Assert.True(sheet.UpdateConditionalFormatRange(condition, new OdfCellRange(3, 0, 4, 0, "Data")));
        Assert.Contains(sheet.ConditionalFormats, candidate =>
            candidate.TargetRangeAddress == condition.TargetRangeAddress);
        OdfConditionalFormatInfo updatedCondition = sheet.FindConditionalFormat(
            candidate => candidate.Kind == OdfConditionalFormatKind.Condition)!;
        Assert.NotEqual(condition.TargetRangeAddress, updatedCondition.TargetRangeAddress);
        Assert.True(sheet.RemoveConditionalFormat(updatedCondition));
        Assert.False(sheet.RemoveConditionalFormat(updatedCondition));
        Assert.Equal(2, sheet.ClearConditionalFormats());
        Assert.Empty(sheet.ConditionalFormats);

        OdfSparklineGroupInfo lineGroup = sheet.FindSparklineGroup(
            candidate => candidate.Type == SparklineType.Line)!;
        Assert.True(sheet.UpdateSparklineGroupType(lineGroup, SparklineType.WinLoss));
        Assert.False(sheet.UpdateSparklineGroupType(lineGroup, SparklineType.Column));
        OdfSparklineGroupInfo updatedGroup = sheet.FindSparklineGroup(
            candidate => candidate.Type == SparklineType.WinLoss)!;
        Assert.True(sheet.RemoveSparklineGroup(updatedGroup));
        Assert.False(sheet.RemoveSparklineGroup(updatedGroup));
        Assert.Equal(1, sheet.ClearSparklineGroups());
        Assert.Empty(sheet.SparklineGroups);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "calcext.ods");
        Assert.Empty(reloaded.Worksheets[0].ConditionalFormats);
        Assert.Empty(reloaded.Worksheets[0].SparklineGroups);
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "format-extension", foreignNamespace));
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "sparkline-extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies pivot definitions support lookup, range updates, selective removal, and round-trip without recalculation.
    /// 驗證樞紐分析表定義支援查找、範圍更新、選擇性移除及不重算的 round-trip。
    /// </summary>
    [Fact]
    public void OdsPivotDefinitions_FindUpdateRemoveClearAndRoundTrip()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.Cells["D1"].CellValue = "cached-output";
        sheet.CreatePivotTable(
            "PivotOne",
            new OdfCellRange(0, 0, 4, 1, "Data"),
            new OdfCellAddress(0, 3, "Data"),
            pivot => pivot.AddRowField("Category").AddDataField("Amount"));
        sheet.CreatePivotTable(
            "PivotTwo",
            new OdfCellRange(0, 0, 4, 1, "Data"),
            new OdfCellAddress(6, 3, "Data"),
            pivot => pivot.AddRowField("Category"));

        const string foreignNamespace = "urn:odfkit:test:pivot-foreign";
        OdfNode pivots = FindDescendant(document.ContentRoot, "data-pilot-tables", OdfNamespaces.Table)!;
        pivots.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.Equal("Data", document.FindPivotTable("PivotOne")!.SheetName);
        Assert.True(document.UpdatePivotTableRanges(
            "PivotOne",
            new OdfCellRange(1, 0, 5, 1, "Data"),
            new OdfCellRange(1, 3, 3, 4, "Data")));
        Assert.False(document.UpdatePivotTableRanges(
            "Missing",
            new OdfCellRange(0, 0, 0, 0, "Data"),
            new OdfCellRange(0, 0, 0, 0, "Data")));
        Assert.Equal(
            new OdfCellRange(1, 0, 5, 1, "Data").ToOdfString(false),
            document.FindPivotTable("PivotOne")!.SourceRangeAddress);
        Assert.True(document.RemovePivotTable("PivotTwo"));
        Assert.False(document.RemovePivotTable("PivotTwo"));
        Assert.Equal(1, document.ClearPivotTables());
        Assert.Empty(document.GetPivotTables());
        Assert.Equal("cached-output", sheet.Cells["D1"].DisplayText);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "pivots.ods");
        Assert.Empty(reloaded.GetPivotTables());
        Assert.Equal("cached-output", reloaded.Worksheets[0].Cells["D1"].DisplayText);
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies embedded charts support find, removal, clearing, package cleanup, and round-trip.
    /// 驗證嵌入圖表支援查找、移除、清除、封裝清理及 round-trip。
    /// </summary>
    [Fact]
    public void OdsEmbeddedCharts_FindRemoveClearAndPackageCleanup()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.Worksheets.Add("Data");
        var firstDefinition = new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "First",
            DataRange = new OdfCellRange(0, 0, 4, 1, "Data"),
        };
        var secondDefinition = new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "Second",
            DataRange = new OdfCellRange(0, 0, 4, 1, "Data"),
        };
        document.AddChart("Data", new OdfCellAddress(0, 3, "Data"), firstDefinition);
        document.AddChart("Data", new OdfCellAddress(8, 3, "Data"), secondDefinition);

        OdfEmbeddedChartInfo first = document.FindEmbeddedChart("./Object 1/")!;
        Assert.Equal("First", first.Title);
        document.GetEmbeddedChartDocument(first).ChartTitle = "must-not-return";
        Assert.True(document.RemoveEmbeddedChart(first));
        Assert.False(document.RemoveEmbeddedChart(first.ObjectPath));
        Assert.False(document.Package.HasEntry("Object 1/content.xml"));
        Assert.NotNull(document.FindEmbeddedChart("Object 2"));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "charts.ods");
        OdfEmbeddedChartInfo remaining = Assert.Single(reloaded.GetEmbeddedCharts());
        Assert.Equal("Second", remaining.Title);
        Assert.False(reloaded.Package.HasEntry("Object 1/content.xml"));
        Assert.Equal(1, reloaded.ClearEmbeddedCharts());
        Assert.Equal(0, reloaded.ClearEmbeddedCharts());
        Assert.Empty(reloaded.GetEmbeddedCharts());
        Assert.False(reloaded.Package.HasEntry("Object 2/content.xml"));

        using var clearedStream = new MemoryStream();
        reloaded.SaveToStream(clearedStream);
        clearedStream.Position = 0;
        using SpreadsheetDocument cleared = SpreadsheetDocument.Load(clearedStream, "charts-cleared.ods");
        Assert.Empty(cleared.GetEmbeddedCharts());
        Assert.False(cleared.Package.HasEntry("Object 2/content.xml"));
    }

    /// <summary>
    /// Verifies slide drawing-object removal cleans dependent animations and preserves non-drawing content through round-trip.
    /// 驗證移除投影片繪圖物件時會清理相依動畫，並在 round-trip 後保留非繪圖內容。
    /// </summary>
    [Fact]
    public void OdpDrawingObjects_FindRemoveClearAndRoundTrip()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Objects");
        OdfShape animated = slide.AddShape(
            OdfShapeType.Rectangle,
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("3cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"));
        slide.AddEntranceEffect(animated.Id, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick);
        slide.AddTextBox(
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("4cm"),
            OdfKit.Styles.OdfLength.Parse("5cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"),
            "remove later");
        slide.SetSpeakerNotes(["preserved note"]);

        Assert.NotNull(slide.FindDrawingObject(animated.Id));
        Assert.True(slide.RemoveDrawingObject(animated.Id));
        Assert.False(slide.RemoveDrawingObject(animated.Id));
        Assert.Empty(slide.GetAnimations());
        Assert.Equal(1, slide.ClearDrawingObjects());
        Assert.Empty(slide.TextBoxes);
        Assert.Contains("preserved note", slide.SpeakerNoteParagraphs);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "objects.odp");
        OdfSlide reloadedSlide = reloaded.Slides[0];
        Assert.Empty(reloadedSlide.Shapes);
        Assert.Empty(reloadedSlide.TextBoxes);
        Assert.Empty(reloadedSlide.GetAnimations());
        Assert.Contains("preserved note", reloadedSlide.SpeakerNoteParagraphs);
    }

    /// <summary>
    /// Verifies slide placeholders support lookup, identity-safe removal, clear, and dependent animation cleanup.
    /// 驗證投影片預留位置支援查找、識別安全移除、清除及相依動畫清理。
    /// </summary>
    [Fact]
    public void OdpPlaceholders_FindRemoveClearAndRoundTrip()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Placeholders");
        OdfPlaceholder title = slide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("8cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"));
        slide.AddPlaceholder(
            OdfPlaceholderType.Subtitle,
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("4cm"),
            OdfKit.Styles.OdfLength.Parse("8cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"));
        slide.AddEntranceEffect(title.Id, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick);

        Assert.Same(title.Node, slide.FindPlaceholder(OdfPlaceholderType.Title)!.Node);
        Assert.True(slide.RemovePlaceholder(title));
        Assert.False(slide.RemovePlaceholder(title));
        Assert.Empty(slide.GetAnimations());
        Assert.Equal(1, slide.ClearPlaceholders());
        Assert.Empty(slide.Placeholders);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "placeholders.odp");
        Assert.Empty(reloaded.Slides[0].Placeholders);
        Assert.Empty(reloaded.Slides[0].GetAnimations());
    }

    /// <summary>
    /// Verifies animation timelines support lookup, target-scoped removal, selective clear, and round-trip.
    /// 驗證動畫時間軸支援查找、目標範圍移除、選擇性清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdpAnimations_FindRemoveClearAndRoundTrip()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Animations");
        OdfShape first = slide.AddShape(
            OdfShapeType.Rectangle,
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("3cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"));
        OdfShape second = slide.AddShape(
            OdfShapeType.Ellipse,
            OdfKit.Styles.OdfLength.Parse("5cm"),
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("3cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"));
        slide.AddEntranceEffect(first.Id, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick);
        slide.AddExitEffect(first.Id, OdfAnimationEffect.FlyIn, OdfAnimationTrigger.AfterPrevious);
        slide.AddEmphasisEffect(second.Id, OdfAnimationEffect.Zoom);

        const string foreignNamespace = "urn:odfkit:test:animation-foreign";
        slide.AnimationRoot.Node.AppendChild(
            new OdfNode(OdfNodeType.Element, "timeline-extension", foreignNamespace, "foreign"));

        Assert.Equal(first.Id, slide.FindAnimation(candidate => candidate.Kind == OdfAnimationKind.Entrance)!.TargetElementId);
        Assert.Equal(2, slide.RemoveAnimations(first.Id));
        Assert.Equal(0, slide.RemoveAnimations(first.Id));
        Assert.Single(slide.GetAnimations());
        Assert.Equal(1, slide.ClearAnimations());
        Assert.Empty(slide.GetAnimations());

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "animations.odp");
        Assert.Empty(reloaded.Slides[0].GetAnimations());
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "timeline-extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies master-page rename and removal automatically maintain slide references through round-trip.
    /// 驗證母片重新命名與移除會自動維護投影片引用，並可通過 round-trip。
    /// </summary>
    [Fact]
    public void OdpMasterPages_RenameRemoveAndMaintainReferences()
    {
        using PresentationDocument document = PresentationDocument.Create();
        document.AddMasterPage("PrimaryMaster", new OdfMasterPageDefinition());
        document.AddMasterPage("ReplacementMaster", new OdfMasterPageDefinition());
        OdfSlide slide = document.Slides.Add("MasterRef");
        slide.MasterPageName = "PrimaryMaster";

        Assert.Single(document.GetMasterPageReferences("PrimaryMaster"));
        Assert.False(document.TryRemoveMasterPage("PrimaryMaster", out IReadOnlyList<OdfSlide> blockers));
        Assert.Same(slide, Assert.Single(blockers));
        Assert.True(document.RenameMasterPage("PrimaryMaster", "RenamedMaster"));
        Assert.False(document.RenameMasterPage("RenamedMaster", "ReplacementMaster"));
        Assert.Equal("RenamedMaster", slide.MasterPageName);
        Assert.NotNull(document.FindMasterPage("RenamedMaster"));
        Assert.True(document.RemoveMasterPage("RenamedMaster", "ReplacementMaster"));
        Assert.False(document.RemoveMasterPage("RenamedMaster", "ReplacementMaster"));
        Assert.Equal("ReplacementMaster", slide.MasterPageName);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "masters.odp");
        Assert.Null(reloaded.FindMasterPage("RenamedMaster"));
        Assert.NotNull(reloaded.FindMasterPage("ReplacementMaster"));
        Assert.Equal("ReplacementMaster", reloaded.Slides[0].MasterPageName);
    }

    /// <summary>
    /// Verifies layout rename and removal automatically maintain slide references through round-trip.
    /// 驗證版面配置重新命名與移除會自動維護投影片引用，並可通過 round-trip。
    /// </summary>
    [Fact]
    public void OdpLayouts_RenameRemoveAndMaintainReferences()
    {
        using PresentationDocument document = PresentationDocument.Create();
        document.CreatePresentationPageLayout("PrimaryLayout");
        document.CreatePresentationPageLayout("ReplacementLayout");
        OdfSlide slide = document.Slides.Add("LayoutRef");
        slide.PresentationPageLayoutName = "PrimaryLayout";

        Assert.Single(document.GetPresentationPageLayoutReferences("PrimaryLayout"));
        Assert.False(document.TryRemovePresentationPageLayout(
            "PrimaryLayout", out IReadOnlyList<OdfSlide> blockers));
        Assert.Same(slide, Assert.Single(blockers));
        Assert.True(document.RenamePresentationPageLayout("PrimaryLayout", "RenamedLayout"));
        Assert.False(document.RenamePresentationPageLayout("RenamedLayout", "ReplacementLayout"));
        Assert.Equal("RenamedLayout", slide.PresentationPageLayoutName);
        Assert.True(document.RemovePresentationPageLayout("RenamedLayout", "ReplacementLayout"));
        Assert.False(document.RemovePresentationPageLayout("RenamedLayout", "ReplacementLayout"));
        Assert.Equal("ReplacementLayout", slide.PresentationPageLayoutName);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "layouts.odp");
        Assert.Null(reloaded.FindPresentationPageLayout("RenamedLayout"));
        Assert.NotNull(reloaded.FindPresentationPageLayout("ReplacementLayout"));
        Assert.Equal("ReplacementLayout", reloaded.Slides[0].PresentationPageLayoutName);
    }

    /// <summary>
    /// Verifies ODP and ODG shapes resolve inherited graphic styles after round-trip.
    /// 驗證 ODP 與 ODG 圖形在 round-trip 後可解析繼承的圖形樣式。
    /// </summary>
    [Fact]
    public void OdpAndOdgGraphicStyleInheritance_RoundTrips()
    {
        using PresentationDocument presentation = PresentationDocument.Create();
        OdfSlide slide = presentation.Slides.Add("Styles");
        OdfShape presentationShape = slide.AddShape(
            OdfShapeType.Rectangle,
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"));
        AddInheritedGraphicStyle(presentation, "PresentationParent", "PresentationChild", "#336699");
        presentationShape.Node.SetAttribute("style-name", OdfNamespaces.Draw, "PresentationChild", "draw");
        Assert.Equal("#336699", presentationShape.FillColor);

        using DrawingDocument drawing = DrawingDocument.Create();
        OdfDrawPage page = drawing.Pages.Add("Styles");
        OdfShape drawingShape = page.AddShape(
            OdfShapeType.Rectangle,
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("1cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"),
            OdfKit.Styles.OdfLength.Parse("2cm"));
        AddInheritedGraphicStyle(drawing, "DrawingParent", "DrawingChild", "#993366");
        drawingShape.Node.SetAttribute("style-name", OdfNamespaces.Draw, "DrawingChild", "draw");
        Assert.Equal("#993366", drawingShape.FillColor);

        using var presentationStream = new MemoryStream();
        presentation.SaveToStream(presentationStream);
        presentationStream.Position = 0;
        using PresentationDocument reloadedPresentation = PresentationDocument.Load(presentationStream, "styles.odp");
        Assert.Equal("#336699", Assert.Single(reloadedPresentation.Slides[0].Shapes).FillColor);

        using var drawingStream = new MemoryStream();
        drawing.SaveToStream(drawingStream);
        drawingStream.Position = 0;
        using DrawingDocument reloadedDrawing = DrawingDocument.Load(drawingStream, "styles.odg");
        Assert.Equal("#993366", Assert.Single(reloadedDrawing.Pages[0].Shapes).FillColor);
    }

    /// <summary>
    /// Verifies ODG drawing objects support identifier-based z-order changes, selective clear, and round-trip.
    /// 驗證 ODG 繪圖物件支援依識別碼調整堆疊順序、選擇性清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdgShapes_ZOrderClearAndRoundTrip()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Objects");
        OdfShape first = page.AddShape(OdfShapeType.Rectangle, OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("2cm"), OdfKit.Styles.OdfLength.Parse("2cm"));
        page.AddShape(OdfShapeType.Ellipse, OdfKit.Styles.OdfLength.Parse("4cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("2cm"), OdfKit.Styles.OdfLength.Parse("2cm"));
        OdfShape third = page.AddShape(OdfShapeType.Rectangle, OdfKit.Styles.OdfLength.Parse("7cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("2cm"), OdfKit.Styles.OdfLength.Parse("2cm"));
        const string foreignNamespace = "urn:odfkit:test:odg-foreign";
        page.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.True(page.SendToBack(third.Id));
        Assert.Equal(third.Id, GetDrawingObjectIds(page)[0]);
        Assert.True(page.BringToFront(first.Id));
        Assert.Equal(first.Id, GetDrawingObjectIds(page)[^1]);
        Assert.False(page.BringToFront("missing"));
        Assert.Equal(3, page.ClearShapes());
        Assert.Empty(page.Shapes);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using DrawingDocument reloaded = DrawingDocument.Load(stream, "z-order.odg");
        Assert.Empty(reloaded.Pages[0].Shapes);
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies ODG layer rename and removal maintain shape assignments through round-trip.
    /// 驗證 ODG 圖層重新命名與移除會維護圖形指派，並可通過 round-trip。
    /// </summary>
    [Fact]
    public void OdgLayers_RenameRemoveAndMaintainAssignments()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Layers");
        OdfNode layerSet = new(OdfNodeType.Element, "layer-set", OdfNamespaces.Draw, "draw");
        OdfNode primary = new(OdfNodeType.Element, "layer", OdfNamespaces.Draw, "draw");
        primary.SetAttribute("name", OdfNamespaces.Draw, "Primary", "draw");
        OdfNode replacement = new(OdfNodeType.Element, "layer", OdfNamespaces.Draw, "draw");
        replacement.SetAttribute("name", OdfNamespaces.Draw, "Replacement", "draw");
        layerSet.AppendChild(primary);
        layerSet.AppendChild(replacement);
        page.Node.AppendChild(layerSet);
        OdfShape shape = page.AddShape(OdfShapeType.Rectangle, OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("2cm"), OdfKit.Styles.OdfLength.Parse("2cm"));
        shape.Node.SetAttribute("layer", OdfNamespaces.Draw, "Primary", "draw");

        Assert.NotNull(page.FindLayer("Primary"));
        Assert.True(page.RenameLayer("Primary", "Renamed"));
        Assert.Equal("Renamed", shape.Node.GetAttribute("layer", OdfNamespaces.Draw));
        Assert.True(page.RemoveLayer("Renamed", "Replacement"));
        Assert.False(page.RemoveLayer("Renamed", "Replacement"));
        Assert.Equal("Replacement", shape.Node.GetAttribute("layer", OdfNamespaces.Draw));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using DrawingDocument reloaded = DrawingDocument.Load(stream, "layers.odg");
        Assert.Null(reloaded.Pages[0].FindLayer("Renamed"));
        Assert.NotNull(reloaded.Pages[0].FindLayer("Replacement"));
        Assert.Equal("Replacement", Assert.Single(reloaded.Pages[0].GetShapeLayerAssignments()).LayerName);
    }

    /// <summary>
    /// Verifies loaded and newly created ODG groups share symmetric child CRUD behavior.
    /// 驗證載入既有與新建的 ODG 群組共用對稱的子物件 CRUD 行為。
    /// </summary>
    [Fact]
    public void OdgGroups_FindRemoveClearAndRoundTrip()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Groups");
        OdfDrawGroup group = page.AddGroup("Flow");
        OdfShape start = group.AddShape(OdfShapeType.Rectangle, OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("2cm"), OdfKit.Styles.OdfLength.Parse("2cm"));
        OdfShape end = group.AddShape(OdfShapeType.Ellipse, OdfKit.Styles.OdfLength.Parse("5cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("2cm"), OdfKit.Styles.OdfLength.Parse("2cm"));
        group.AddConnector(start.Id, end.Id);

        Assert.NotNull(group.FindShape(start.Id));
        Assert.True(group.RemoveShape(start.Id));
        Assert.False(group.RemoveShape(start.Id));
        Assert.DoesNotContain(group.Children, child => child.LocalName == "connector");
        Assert.Equal(1, group.Clear());
        Assert.Empty(group.Children);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using DrawingDocument reloaded = DrawingDocument.Load(stream, "groups.odg");
        OdfGroupInfo info = Assert.Single(reloaded.Pages[0].GetGroups());
        OdfShape loadedGroupShape = reloaded.Pages[0].FindShape(info.Id)!;
        var loadedGroup = new OdfDrawGroup(loadedGroupShape.Node, reloaded);
        Assert.Empty(loadedGroup.Children);
    }

    /// <summary>
    /// Verifies existing ODG path, polygon, custom geometry, and transform data can be updated and round-tripped.
    /// 驗證既有 ODG 路徑、多邊形、自訂幾何與 transform 資料可更新並通過 round-trip。
    /// </summary>
    [Fact]
    public void OdgGeometry_UpdateAndRoundTrip()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Geometry");
        OdfShape path = page.AddPath("M 0 0 L 10 10", OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("3cm"), OdfKit.Styles.OdfLength.Parse("3cm"));
        OdfShape polygon = page.AddPolygon([
            (OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("1cm")),
            (OdfKit.Styles.OdfLength.Parse("3cm"), OdfKit.Styles.OdfLength.Parse("1cm")),
            (OdfKit.Styles.OdfLength.Parse("2cm"), OdfKit.Styles.OdfLength.Parse("3cm")),
        ]);
        OdfShape custom = page.AddCustomShape("smiley", OdfKit.Styles.OdfLength.Parse("5cm"), OdfKit.Styles.OdfLength.Parse("1cm"), OdfKit.Styles.OdfLength.Parse("3cm"), OdfKit.Styles.OdfLength.Parse("3cm"));

        Assert.True(page.UpdatePathData(path.Id, "M 0 0 C 10 0 10 10 20 20"));
        Assert.True(page.UpdatePolygonPoints(polygon.Id, "0,0 1000,0 1000,1000 0,1000"));
        Assert.True(page.UpdateCustomGeometryType(custom.Id, "diamond"));
        Assert.True(page.SetTransform(custom.Id, "rotate (0.5)"));
        Assert.False(page.UpdatePathData(custom.Id, "M 0 0"));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using DrawingDocument reloaded = DrawingDocument.Load(stream, "geometry.odg");
        OdfDrawPage loaded = reloaded.Pages[0];
        Assert.Equal("M 0 0 C 10 0 10 10 20 20", Assert.Single(loaded.GetPaths()).SvgPathData);
        Assert.Equal("0,0 1000,0 1000,1000 0,1000", Assert.Single(loaded.GetPolygons()).Points);
        Assert.Equal("diamond", Assert.Single(loaded.GetCustomShapes()).GeometryType);
        Assert.Equal("rotate (0.5)", loaded.FindShape(custom.Id)!.Node.GetAttribute("transform", OdfNamespaces.Draw));
    }

    /// <summary>
    /// Verifies bookmark and reference-mark rename and removal maintain dependent fields through round-trip.
    /// 驗證書籤與參考標記重新命名及移除會維護相依欄位，並可通過 round-trip。
    /// </summary>
    [Fact]
    public void OdtBookmarksAndReferenceMarks_MaintainReferencesAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.Body.Paragraphs.Add("References");
        paragraph.AddBookmark("BookmarkOne");
        paragraph.AddBookmarkReferenceField("BookmarkOne");
        paragraph.AddReferenceMark("ReferenceOne");
        paragraph.AddReferenceField("ReferenceOne");
        const string foreignNamespace = "urn:odfkit:test:text-reference-foreign";
        paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.NotNull(document.FindBookmark("BookmarkOne"));
        Assert.Equal(0, document.RenameBookmark("MissingBookmark", "UnusedName"));
        Assert.Equal(0, document.RenameBookmark("BookmarkOne", " "));
        Assert.Equal(0, document.RenameBookmark("BookmarkOne", "BookmarkOne"));
        Assert.Equal(2, document.RenameBookmark("BookmarkOne", "BookmarkRenamed"));
        Assert.Null(document.FindBookmark("BookmarkOne"));
        Assert.NotNull(document.FindBookmark("BookmarkRenamed"));
        Assert.NotNull(document.FindReferenceMark("ReferenceOne"));
        Assert.Equal(2, document.RenameReferenceMark("ReferenceOne", "ReferenceRenamed"));
        Assert.Equal(2, document.RemoveBookmark("BookmarkRenamed"));
        Assert.Equal(2, document.RemoveReferenceMark("ReferenceRenamed"));
        Assert.Empty(document.GetBookmarks());
        Assert.Empty(document.GetReferenceMarks());

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "references.odt");
        Assert.Empty(reloaded.GetBookmarks());
        Assert.Empty(reloaded.GetReferenceMarks());
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies ODT comments support lookup, in-place update, reply-aware removal, and round-trip.
    /// 驗證 ODT 註解支援查找、就地更新、理解回覆的移除及 round-trip。
    /// </summary>
    [Fact]
    public void OdtComments_FindUpdateRemoveAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.Body.Paragraphs.Add("Commented");
        var comment = new OdfComment("Alice", "Original", DateTime.UtcNow, "comment-one");
        comment.AddReply(new OdfComment("Bob", "Reply", DateTime.UtcNow, "comment-reply"));
        paragraph.AddComment(comment);
        const string foreignNamespace = "urn:odfkit:test:comment-foreign";
        OdfNode annotation = FindDescendant(document.ContentRoot, "annotation", OdfNamespaces.Office)!;
        annotation.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.Equal("Alice", document.FindComment("comment-one")!.Author);
        Assert.True(document.UpdateComment("comment-one", "Carol", "Updated\nSecond line"));
        Assert.False(document.UpdateComment("missing", "Nobody", "Missing"));

        using var firstStream = new MemoryStream();
        document.Save();
        document.Package.Save(firstStream);
        firstStream.Position = 0;
        using TextDocument updated = TextDocument.Load(firstStream, "comments.odt");
        OdfComment loadedComment = updated.FindComment("comment-one")!;
        Assert.Equal("Carol", loadedComment.Author);
        Assert.Equal("Updated\nSecond line", loadedComment.Text);
        Assert.Single(loadedComment.Replies);
        Assert.NotNull(FindDescendant(updated.ContentRoot, "extension", foreignNamespace));
        Assert.True(updated.RemoveComment("comment-one") >= 2);
        Assert.Null(updated.FindComment("comment-one"));

        using var secondStream = new MemoryStream();
        updated.Save();
        updated.Package.Save(secondStream);
        secondStream.Position = 0;
        using TextDocument removed = TextDocument.Load(secondStream, "comments-removed.odt");
        Assert.Empty(removed.GetComments());
    }

    /// <summary>
    /// Verifies footnotes and endnotes support lookup, in-place update, selective clear, removal, and round-trip.
    /// 驗證腳注與尾注支援查找、就地更新、選擇性清除、移除及 round-trip。
    /// </summary>
    [Fact]
    public void OdtNotes_FindUpdateRemoveClearAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.Body.Paragraphs.Add("Notes");
        paragraph.AddFootnote("1", "Footnote body");
        paragraph.AddEndnote("i", "Endnote body");
        OdfFootnoteInfo footnote = Assert.Single(document.GetFootnotes());
        OdfFootnoteInfo endnote = Assert.Single(document.GetEndnotes());
        const string foreignNamespace = "urn:odfkit:test:note-foreign";
        OdfNode noteNode = FindDescendant(document.ContentRoot, "note", OdfNamespaces.Text)!;
        noteNode.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.NotNull(document.FindFootnote(footnote.Id));
        Assert.NotNull(document.FindEndnote(endnote.Id));
        Assert.True(document.UpdateFootnote(footnote.Id, "2", "Updated footnote"));
        Assert.True(document.UpdateEndnote(endnote.Id, "ii", "Updated endnote"));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "notes.odt");
        Assert.Equal("Updated footnote", reloaded.FindFootnote(footnote.Id)!.BodyText);
        Assert.Equal("Updated endnote", reloaded.FindEndnote(endnote.Id)!.BodyText);
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
        Assert.Equal(1, reloaded.ClearEndnotes());
        Assert.True(reloaded.RemoveFootnote(footnote.Id));
        Assert.False(reloaded.RemoveFootnote(footnote.Id));
        Assert.Empty(reloaded.GetFootnotes());
        Assert.Empty(reloaded.GetEndnotes());
    }

    /// <summary>
    /// Verifies sections and indexes support lookup, rename, identity-safe removal, clear, and round-trip.
    /// 驗證區段與索引支援查找、重新命名、依身分安全移除、清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdtSectionsAndIndexes_CompleteLifecycleAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        OdfSection firstSection = document.AddSection("First", 2, OdfKit.Styles.OdfLength.Parse("0.5cm"));
        _ = document.AddSection("Second", 1, OdfKit.Styles.OdfLength.Parse("0cm"));
        OdfAlphabeticalIndex firstIndex = document.AddAlphabeticalIndex("Terms");
        _ = document.AddTableOfContents("Contents");
        _ = document.AddBibliography("Sources");
        const string foreignNamespace = "urn:odfkit:test:lifecycle-foreign";
        firstSection.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        firstIndex.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.Same(firstSection.Node, document.Body.FindSection("First")!.Node);
        firstSection.Name = "Renamed";
        firstIndex.Name = "Renamed Terms";
        Assert.Null(document.Body.FindSection("First"));
        Assert.NotNull(document.Body.FindSection("Renamed"));
        Assert.Null(document.FindIndex("Terms"));
        Assert.NotNull(document.FindIndex("Renamed Terms"));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "sections-indexes.odt");
        OdfSection loadedSection = reloaded.Body.FindSection("Renamed")!;
        OdfIndex loadedIndex = reloaded.FindIndex("Renamed Terms")!;
        Assert.NotNull(FindDescendant(loadedSection.Node, "extension", foreignNamespace));
        Assert.NotNull(FindDescendant(loadedIndex.Node, "extension", foreignNamespace));
        Assert.True(reloaded.Body.RemoveSection(loadedSection));
        Assert.False(reloaded.Body.RemoveSection(loadedSection));
        Assert.True(reloaded.RemoveIndex(loadedIndex));
        Assert.False(reloaded.RemoveIndex(loadedIndex));
        Assert.Equal(1, reloaded.Body.ClearSections());
        Assert.Equal(2, reloaded.ClearIndexes());
        Assert.Empty(reloaded.Body.Sections);
        Assert.Empty(reloaded.GetIndexes());
    }

    /// <summary>
    /// Verifies notes and handout lookups do not mutate the document and support non-destructive lifecycle operations.
    /// 驗證備忘錄與講義查找不會修改文件，並支援非破壞性生命週期操作。
    /// </summary>
    [Fact]
    public void OdpNotesAndHandout_FindClearRemoveAndRoundTrip()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Lifecycle");
        Assert.Null(slide.FindSpeakerNotesPage());
        Assert.Null(document.FindHandoutPage());
        Assert.Null(FindDescendant(slide.Node, "notes", OdfNamespaces.Presentation));

        slide.SetSpeakerNotes(["First", "Second"]);
        OdfNotesPage notes = slide.FindSpeakerNotesPage()!;
        OdfNode textBox = FindDescendant(notes.Node, "text-box", OdfNamespaces.Draw)!;
        const string foreignNamespace = "urn:odfkit:test:notes-foreign";
        textBox.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        slide.SetSpeakerNotes(["Updated"]);
        Assert.Single(slide.SpeakerNoteParagraphs);
        Assert.NotNull(FindDescendant(notes.Node, "extension", foreignNamespace));

        OdfHandoutPage handout = document.HandoutPage;
        handout.Name = "LifecycleHandout";
        handout.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "notes-handout.odp");
        OdfSlide loadedSlide = reloaded.Slides.Find("Lifecycle")!;
        Assert.Equal("Updated", loadedSlide.SpeakerNotes);
        Assert.NotNull(FindDescendant(loadedSlide.FindSpeakerNotesPage()!.Node, "extension", foreignNamespace));
        Assert.Equal("LifecycleHandout", reloaded.FindHandoutPage()!.Name);
        Assert.Equal(1, loadedSlide.ClearSpeakerNotes());
        Assert.Equal(0, loadedSlide.ClearSpeakerNotes());
        Assert.NotNull(FindDescendant(loadedSlide.FindSpeakerNotesPage()!.Node, "extension", foreignNamespace));
        Assert.True(loadedSlide.RemoveSpeakerNotesPage());
        Assert.False(loadedSlide.RemoveSpeakerNotesPage());
        Assert.True(reloaded.RemoveHandoutPage());
        Assert.False(reloaded.RemoveHandoutPage());
    }

    /// <summary>
    /// Verifies existing slide media can be enumerated, found, updated, removed, cleared, and round-tripped.
    /// 驗證現有投影片媒體可列舉、查找、更新、移除、清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdpMediaObjects_CompleteLifecycleAndRoundTrip()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Media");
        OdfKit.Styles.OdfLength x = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength y = OdfKit.Styles.OdfLength.Parse("2cm");
        OdfKit.Styles.OdfLength width = OdfKit.Styles.OdfLength.Parse("8cm");
        OdfKit.Styles.OdfLength height = OdfKit.Styles.OdfLength.Parse("4cm");
        OdfMediaObject video = slide.AddVideo("Media/video.mp4", x, y, width, height);
        _ = slide.AddAudio("Media/audio.mp3", x, y, width, height);
        const string foreignNamespace = "urn:odfkit:test:media-foreign";
        video.FrameNode.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.Equal(2, slide.MediaObjects.Count);
        Assert.Same(video.FrameNode, slide.FindMediaObject(video.Id)!.FrameNode);
        video.PackagePath = "Media/replaced.webm";
        video.MimeType = "video/webm";

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "media.odp");
        OdfSlide loadedSlide = reloaded.Slides.Find("Media")!;
        OdfMediaObject loadedVideo = loadedSlide.FindMediaObject(video.Id)!;
        Assert.Equal("Media/replaced.webm", loadedVideo.PackagePath);
        Assert.Equal("video/webm", loadedVideo.MimeType);
        Assert.NotNull(FindDescendant(loadedVideo.FrameNode, "extension", foreignNamespace));
        Assert.True(loadedSlide.RemoveMediaObject(loadedVideo.Id));
        Assert.False(loadedSlide.RemoveMediaObject(loadedVideo.Id));
        Assert.Equal(1, loadedSlide.ClearMediaObjects());
        Assert.Equal(0, loadedSlide.ClearMediaObjects());
        Assert.Empty(loadedSlide.MediaObjects);
    }

    /// <summary>
    /// Verifies embedded tables support direct creation, lookup, reading, updating, removal, clear, and round-trip.
    /// 驗證嵌入表格支援直接建立、查找、讀取、更新、移除、清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdpEmbeddedTables_CompleteLifecycleAndRoundTrip()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Tables");
        OdfKit.Styles.OdfLength x = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength y = OdfKit.Styles.OdfLength.Parse("2cm");
        OdfKit.Styles.OdfLength width = OdfKit.Styles.OdfLength.Parse("12cm");
        OdfKit.Styles.OdfLength height = OdfKit.Styles.OdfLength.Parse("6cm");
        OdfEmbeddedTable table = slide.AddTable(2, 2, x, y, width, height)
            .SetCellText(0, 0, "A1")
            .SetCellText(1, 1, "B2");
        _ = slide.AddTable(1, 1, x, y, width, height).SetCellText(0, 0, "Second");
        const string foreignNamespace = "urn:odfkit:test:table-foreign";
        var extension = new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign");
        table.TableNode.InsertBefore(extension, table.TableNode.Children[0]);

        Assert.Equal(2, slide.EmbeddedTables.Count);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal("A1", table.GetCellText(0, 0));
        Assert.Same(table.TableNode, slide.FindEmbeddedTable(table.Id)!.TableNode);
        table.SetCellText(0, 0, "Updated");

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "tables.odp");
        OdfSlide loadedSlide = reloaded.Slides.Find("Tables")!;
        OdfEmbeddedTable loadedTable = loadedSlide.FindEmbeddedTable(table.Id)!;
        Assert.Equal("Updated", loadedTable.GetCellText(0, 0));
        Assert.Equal("B2", loadedTable.GetCellText(1, 1));
        Assert.NotNull(FindDescendant(loadedTable.TableNode, "extension", foreignNamespace));
        Assert.True(loadedSlide.RemoveEmbeddedTable(loadedTable.Id));
        Assert.False(loadedSlide.RemoveEmbeddedTable(loadedTable.Id));
        Assert.Equal(1, loadedSlide.ClearEmbeddedTables());
        Assert.Equal(0, loadedSlide.ClearEmbeddedTables());
        Assert.Empty(loadedSlide.EmbeddedTables);
    }

    /// <summary>
    /// Verifies connectors support direct creation, lookup, removal, clear, dependency cleanup, and round-trip.
    /// 驗證連接線支援直接建立、查找、移除、清除、相依清理及 round-trip。
    /// </summary>
    [Fact]
    public void OdpConnectors_CompleteLifecycleAndDependencyCleanup()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Connectors");
        OdfKit.Styles.OdfLength x = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength y = OdfKit.Styles.OdfLength.Parse("2cm");
        OdfKit.Styles.OdfLength size = OdfKit.Styles.OdfLength.Parse("3cm");
        OdfShape first = slide.AddShape(OdfShapeType.Rectangle, x, y, size, size);
        OdfShape second = slide.AddShape(OdfShapeType.Ellipse, x, y, size, size);
        OdfShape third = slide.AddShape(OdfShapeType.Rectangle, x, y, size, size);
        OdfShape removable = slide.AddConnector(first.Id, second.Id);
        OdfShape dependent = slide.AddConnector(first.Id, third.Id);
        OdfShape clearable = slide.AddConnector(second.Id, third.Id);
        const string foreignNamespace = "urn:odfkit:test:connector-foreign";
        dependent.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.Equal(3, slide.Connectors.Count);
        Assert.Same(removable.Node, slide.FindConnector(removable.Id)!.Node);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "connectors.odp");
        OdfSlide loadedSlide = reloaded.Slides.Find("Connectors")!;
        Assert.NotNull(FindDescendant(loadedSlide.FindConnector(dependent.Id)!.Node, "extension", foreignNamespace));
        Assert.True(loadedSlide.RemoveConnector(removable.Id));
        Assert.False(loadedSlide.RemoveConnector(removable.Id));
        Assert.True(loadedSlide.RemoveDrawingObject(first.Id));
        Assert.Null(loadedSlide.FindConnector(dependent.Id));
        Assert.NotNull(loadedSlide.FindConnector(clearable.Id));
        Assert.Equal(1, loadedSlide.ClearConnectors());
        Assert.Equal(0, loadedSlide.ClearConnectors());
        Assert.Empty(loadedSlide.Connectors);
    }

    /// <summary>
    /// Verifies slide groups and their existing child objects support symmetric lifecycle operations.
    /// 驗證投影片群組及其現有子物件支援對稱生命週期操作。
    /// </summary>
    [Fact]
    public void OdpGroups_ChildLifecycleAndRoundTrip()
    {
        using PresentationDocument document = PresentationDocument.Create();
        OdfSlide slide = document.Slides.Add("Groups");
        OdfDrawGroup group = slide.AddGroup("Primary");
        _ = slide.AddGroup("Secondary");
        OdfKit.Styles.OdfLength x = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength y = OdfKit.Styles.OdfLength.Parse("2cm");
        OdfKit.Styles.OdfLength size = OdfKit.Styles.OdfLength.Parse("3cm");
        OdfShape first = group.AddShape(OdfShapeType.Rectangle, x, y, size, size);
        OdfShape second = group.AddShape(OdfShapeType.Ellipse, x, y, size, size);
        OdfShape connector = group.AddConnector(first.Id, second.Id);
        const string foreignNamespace = "urn:odfkit:test:group-foreign";
        group.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.Equal(2, slide.Groups.Count);
        Assert.Same(group.Node, slide.FindGroup(group.Id)!.Node);
        Assert.Equal(3, group.Children.Count);
        Assert.NotNull(group.FindShape(connector.Id));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using PresentationDocument reloaded = PresentationDocument.Load(stream, "groups.odp");
        OdfSlide loadedSlide = reloaded.Slides.Find("Groups")!;
        OdfDrawGroup loadedGroup = loadedSlide.FindGroup(group.Id)!;
        Assert.Equal("Primary", loadedGroup.Name);
        Assert.NotNull(FindDescendant(loadedGroup.Node, "extension", foreignNamespace));
        Assert.True(loadedGroup.RemoveShape(first.Id));
        Assert.Null(loadedGroup.FindShape(first.Id));
        Assert.Null(loadedGroup.FindShape(connector.Id));
        Assert.Equal(1, loadedGroup.Clear());
        Assert.Equal(0, loadedGroup.Clear());
        Assert.NotNull(FindDescendant(loadedGroup.Node, "extension", foreignNamespace));
        Assert.True(loadedSlide.RemoveGroup(loadedGroup.Id));
        Assert.False(loadedSlide.RemoveGroup(loadedGroup.Id));
        Assert.Equal(1, loadedSlide.ClearGroups());
        Assert.Equal(0, loadedSlide.ClearGroups());
        Assert.Empty(loadedSlide.Groups);
    }

    /// <summary>
    /// Verifies named gradients support CRUD, shape assignment, reference-safe rename and removal, and round-trip.
    /// 驗證具名稱漸層支援 CRUD、圖形指派、參照安全重新命名與移除，以及 round-trip。
    /// </summary>
    [Fact]
    public void OdgGradients_CompleteLifecycleAndReferenceSafety()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Gradients");
        OdfKit.Styles.OdfLength x = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength y = OdfKit.Styles.OdfLength.Parse("2cm");
        OdfKit.Styles.OdfLength size = OdfKit.Styles.OdfLength.Parse("4cm");
        OdfShape shape = page.AddShape(OdfShapeType.Rectangle, x, y, size, size);
        OdfGradient gradient = document.SetGradient("Sky", "linear", "#112233", "#AABBCC", 900);
        _ = document.SetGradient("Unused", "radial", "#000000", "#FFFFFF", 0);
        const string foreignNamespace = "urn:odfkit:test:gradient-foreign";
        gradient.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        shape.FillGradientName = gradient.Name;

        Assert.Equal(2, document.Gradients.Count);
        Assert.Equal("Sky", document.FindGradient("Sky")!.Name);
        Assert.True(document.RenameGradient("Sky", "RenamedSky"));
        Assert.False(document.RenameGradient("RenamedSky", "Unused"));
        Assert.Equal("RenamedSky", shape.FillGradientName);
        Assert.False(document.RemoveGradient("RenamedSky"));
        Assert.Equal(1, document.ClearGradients());

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using DrawingDocument reloaded = DrawingDocument.Load(stream, "gradients.odg");
        OdfGradient loadedGradient = reloaded.FindGradient("RenamedSky")!;
        OdfShape loadedShape = reloaded.Pages.Find("Gradients")!.FindShape(shape.Id)!;
        Assert.Equal("linear", loadedGradient.Style);
        Assert.Equal("#112233", loadedGradient.StartColor);
        Assert.Equal("#AABBCC", loadedGradient.EndColor);
        Assert.Equal(900, loadedGradient.Angle);
        Assert.Equal("RenamedSky", loadedShape.FillGradientName);
        Assert.NotNull(FindDescendant(loadedGradient.Node, "extension", foreignNamespace));
        loadedShape.FillGradientName = null;
        Assert.True(reloaded.RemoveGradient("RenamedSky"));
        Assert.False(reloaded.RemoveGradient("RenamedSky"));
        Assert.Empty(reloaded.Gradients);
    }

    /// <summary>
    /// Verifies named markers support CRUD, line assignment, reference-safe rename and removal, and round-trip.
    /// 驗證具名稱標記支援 CRUD、線段指派、參照安全重新命名與移除，以及 round-trip。
    /// </summary>
    [Fact]
    public void OdgMarkers_CompleteLifecycleAndReferenceSafety()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Markers");
        OdfKit.Styles.OdfLength start = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength end = OdfKit.Styles.OdfLength.Parse("8cm");
        OdfShape line = page.AddLine(start, start, end, end);
        OdfMarker marker = document.SetMarker("Arrow", "0 0 10 10", "M0 0 L10 5 L0 10 Z");
        marker.DisplayName = "Arrow marker";
        _ = document.SetMarker("UnusedMarker", "0 0 5 5", "M0 0 L5 2 L0 5 Z");
        const string foreignNamespace = "urn:odfkit:test:marker-foreign";
        marker.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        line.MarkerStartName = marker.Name;
        line.MarkerEndName = marker.Name;

        Assert.Equal(2, document.Markers.Count);
        Assert.True(document.RenameMarker("Arrow", "RenamedArrow"));
        Assert.False(document.RenameMarker("RenamedArrow", "UnusedMarker"));
        Assert.Equal("RenamedArrow", line.MarkerStartName);
        Assert.Equal("RenamedArrow", line.MarkerEndName);
        Assert.False(document.RemoveMarker("RenamedArrow"));
        Assert.Equal(1, document.ClearMarkers());

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using DrawingDocument reloaded = DrawingDocument.Load(stream, "markers.odg");
        OdfMarker loadedMarker = reloaded.FindMarker("RenamedArrow")!;
        OdfShape loadedLine = reloaded.Pages.Find("Markers")!.FindShape(line.Id)!;
        Assert.Equal("Arrow marker", loadedMarker.DisplayName);
        Assert.Equal("0 0 10 10", loadedMarker.ViewBox);
        Assert.Equal("M0 0 L10 5 L0 10 Z", loadedMarker.PathData);
        Assert.Equal("RenamedArrow", loadedLine.MarkerStartName);
        Assert.Equal("RenamedArrow", loadedLine.MarkerEndName);
        Assert.NotNull(FindDescendant(loadedMarker.Node, "extension", foreignNamespace));
        loadedLine.MarkerStartName = null;
        loadedLine.MarkerEndName = null;
        Assert.True(reloaded.RemoveMarker("RenamedArrow"));
        Assert.False(reloaded.RemoveMarker("RenamedArrow"));
        Assert.Empty(reloaded.Markers);
    }

    /// <summary>
    /// Verifies rectangular and contour-path clips support update, clear, unknown-content preservation, and round-trip.
    /// 驗證矩形與輪廓路徑裁切支援更新、清除、未知內容保留及 round-trip。
    /// </summary>
    [Fact]
    public void OdgClips_UpdateClearAndRoundTrip()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.Pages.Add("Clips");
        OdfKit.Styles.OdfLength x = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength y = OdfKit.Styles.OdfLength.Parse("2cm");
        OdfKit.Styles.OdfLength size = OdfKit.Styles.OdfLength.Parse("5cm");
        OdfShape shape = page.AddShape(OdfShapeType.Rectangle, x, y, size, size);
        shape.ClipRectangle = "rect(0cm, 5cm, 5cm, 0cm)";
        shape.SetClipPath("M0 0 L100 0 L100 100 Z", "0 0 100 100");
        const string foreignNamespace = "urn:odfkit:test:clip-foreign";
        shape.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        shape.SetClipPath("M0 0 L80 0 L80 80 Z", "0 0 80 80");

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using DrawingDocument reloaded = DrawingDocument.Load(stream, "clips.odg");
        OdfShape loadedShape = reloaded.Pages.Find("Clips")!.FindShape(shape.Id)!;
        Assert.Equal("rect(0cm, 5cm, 5cm, 0cm)", loadedShape.ClipRectangle);
        Assert.Equal("M0 0 L80 0 L80 80 Z", loadedShape.ClipPathData);
        Assert.NotNull(FindDescendant(loadedShape.Node, "extension", foreignNamespace));
        loadedShape.ClipRectangle = null;
        Assert.True(loadedShape.ClearClipPath());
        Assert.False(loadedShape.ClearClipPath());
        Assert.Null(loadedShape.ClipRectangle);
        Assert.Null(loadedShape.ClipPathData);
        Assert.NotNull(FindDescendant(loadedShape.Node, "extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies ODT form controls support lookup, update, geometry reads, reference-safe removal, clear, and round-trip.
    /// 驗證 ODT 表單控制項支援查找、更新、幾何讀取、參照安全移除、清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdtFormControls_CompleteLifecycleAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        OdfKit.Styles.OdfLength x = OdfKit.Styles.OdfLength.Parse("1cm");
        OdfKit.Styles.OdfLength y = OdfKit.Styles.OdfLength.Parse("2cm");
        OdfKit.Styles.OdfLength width = OdfKit.Styles.OdfLength.Parse("5cm");
        OdfKit.Styles.OdfLength height = OdfKit.Styles.OdfLength.Parse("1cm");
        _ = document.AddFormControl(OdfKit.Forms.OdfControlType.CheckBox, "Accepted", x, y, width, height, "Accept");
        _ = document.AddFormControl(
            OdfKit.Forms.OdfControlType.ListBox,
            "Priority",
            x,
            y,
            width,
            height,
            "Priority",
            ["Low", "High"]);
        const string foreignNamespace = "urn:odfkit:test:form-foreign";
        OdfNode listBox = FindDescendant(document.ContentRoot, "listbox", OdfNamespaces.Form)!;
        listBox.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        OdfNode form = FindDescendant(document.ContentRoot, "form", OdfNamespaces.Form)!;
        form.AppendChild(new OdfNode(OdfNodeType.Element, "form-extension", foreignNamespace, "foreign"));

        Assert.Equal(2, document.GetFormControls().Count);
        Assert.True(document.UpdateFormControl("Accepted", "Accepted terms", null, true, null));
        Assert.True(document.UpdateFormControl("Priority", "Updated priority", "High", false, ["Medium", "Urgent"]));
        Assert.False(document.UpdateFormControl("Missing", "Missing", null, false, null));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "forms.odt");
        OdfKit.Forms.OdfFormControl checkbox = reloaded.FindFormControl("Accepted")!;
        OdfKit.Forms.OdfFormControl loadedList = reloaded.FindFormControl("Priority")!;
        Assert.Equal("Accepted terms", checkbox.Label);
        Assert.True(checkbox.IsChecked);
        Assert.Equal("1cm", checkbox.X.ToString());
        Assert.Equal("5cm", checkbox.Width.ToString());
        Assert.Equal("High", loadedList.Value);
        Assert.Equal(["Medium", "Urgent"], loadedList.ListItems);
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
        Assert.True(reloaded.RemoveFormControl("Priority"));
        Assert.False(reloaded.RemoveFormControl("Priority"));
        Assert.Equal(1, reloaded.ClearFormControls());
        Assert.Equal(0, reloaded.ClearFormControls());
        Assert.Empty(reloaded.GetFormControls());
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "form-extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies user-field declarations support lookup, update, removal, clear, unknown-content preservation, and round-trip.
    /// 驗證使用者欄位宣告支援查找、更新、移除、清除、未知內容保留及 round-trip。
    /// </summary>
    [Fact]
    public void OdtUserFields_CompleteLifecycleAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        document.AddUserFieldDeclaration("Customer", "string", "Alice");
        document.AddUserFieldDeclaration("Count", "float", "2");
        const string foreignNamespace = "urn:odfkit:test:user-field-foreign";
        OdfNode declarations = FindDescendant(document.ContentRoot, "user-field-decls", OdfNamespaces.Text)!;
        OdfNode customer = FindDescendant(declarations, "user-field-decl", OdfNamespaces.Text)!;
        customer.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        declarations.AppendChild(new OdfNode(OdfNodeType.Element, "container-extension", foreignNamespace, "foreign"));

        Assert.Equal("Alice", document.FindUserFieldDeclaration("Customer")!.Value);
        Assert.True(document.SetUserFieldValue("Customer", "Bob"));
        Assert.False(document.SetUserFieldValue("Missing", "Nobody"));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "user-fields.odt");
        Assert.Equal("Bob", reloaded.FindUserFieldDeclaration("Customer")!.Value);
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "extension", foreignNamespace));
        Assert.True(reloaded.RemoveUserFieldDeclaration("Customer"));
        Assert.False(reloaded.RemoveUserFieldDeclaration("Customer"));
        Assert.Equal(1, reloaded.ClearUserFieldDeclarations());
        Assert.Equal(0, reloaded.ClearUserFieldDeclarations());
        Assert.Empty(reloaded.GetUserFieldDeclarations());
        Assert.NotNull(FindDescendant(reloaded.ContentRoot, "container-extension", foreignNamespace));
    }

    /// <summary>
    /// Verifies formula objects support lookup, update, removal, clear, package cleanup, and round-trip.
    /// 驗證公式物件支援查找、更新、移除、清除、封裝包清理及 round-trip。
    /// </summary>
    [Fact]
    public void OdtFormulaObjects_CompleteLifecycleAndPackageCleanup()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.Body.Paragraphs.Add("Formulas");
        paragraph.AddFormula("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>");
        paragraph.AddFormula("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>y</mi></math>");
        List<OdfFormulaObject> formulas = [.. document.GetFormulaObjects()];
        formulas[0].Name = "EquationX";
        formulas[1].Name = "EquationY";
        string firstFolder = formulas[0].FormulaFolder!;
        string secondFolder = formulas[1].FormulaFolder!;
        const string foreignNamespace = "urn:odfkit:test:formula-foreign";
        formulas[0].FrameNode.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));
        document.Package.WriteEntry(firstFolder + "/custom.bin", [1, 2, 3], "application/octet-stream");
        formulas[0].MathMlXmlString = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><msup><mi>x</mi><mn>2</mn></msup></math>";

        Assert.Equal(2, formulas.Count);
        Assert.Equal(firstFolder, document.FindFormulaObject("EquationX")!.FormulaFolder);
        Assert.Equal("EquationY", document.FindFormulaObject(secondFolder)!.Name);

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "formulas.odt");
        OdfFormulaObject loaded = reloaded.FindFormulaObject("EquationX")!;
        Assert.Contains("<msup>", loaded.MathMlXmlString, StringComparison.Ordinal);
        Assert.NotNull(FindDescendant(loaded.FrameNode, "extension", foreignNamespace));
        Assert.True(reloaded.Package.HasEntry(firstFolder + "/custom.bin"));
        Assert.True(reloaded.RemoveFormulaObject("EquationX"));
        Assert.False(reloaded.RemoveFormulaObject("EquationX"));
        Assert.False(reloaded.Package.HasEntry(firstFolder + "/content.xml"));
        Assert.False(reloaded.Package.HasEntry(firstFolder + "/custom.bin"));
        Assert.True(reloaded.Package.HasEntry(secondFolder + "/content.xml"));
        Assert.Equal(1, reloaded.ClearFormulaObjects());
        Assert.Equal(0, reloaded.ClearFormulaObjects());
        Assert.Empty(reloaded.GetFormulaObjects());
        Assert.False(reloaded.Package.HasEntry(secondFolder + "/content.xml"));
    }

    /// <summary>
    /// Verifies supported inline text fields provide typed lookup, non-destructive updates, removal, clear, and round-trip.
    /// 驗證支援的內嵌文字欄位提供 typed 查找、非破壞性更新、移除、清除及 round-trip。
    /// </summary>
    [Fact]
    public void OdtInlineFields_CompleteTypedLifecycleAndRoundTrip()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.Body.Paragraphs.Add("Fields: ");
        paragraph.AddDateField();
        paragraph.AddTimeField();
        paragraph.AddAuthorField();
        paragraph.AddChapterField();
        paragraph.AddSequenceField("Figure", "1");
        paragraph.AddReferenceField("ReferenceOne");
        paragraph.AddSequenceRefField("Figure", "value");
        paragraph.AddBookmarkReferenceField("BookmarkOne", "text");
        paragraph.AddVariableSetField("Customer", "Alice");
        paragraph.AddVariableGetField("Customer");
        paragraph.AddDatabaseDisplayField("Customers", "Name", "table", "MainDb");
        paragraph.AddDatabaseNextField("Customers", "table", "MainDb", "true()");
        const string foreignNamespace = "urn:odfkit:test:inline-field-foreign";
        OdfTextField variable = document.FindTextField(OdfTextFieldKind.VariableSet, "Customer")!;
        variable.Node.AppendChild(new OdfNode(OdfNodeType.Element, "extension", foreignNamespace, "foreign"));

        Assert.Equal(12, document.GetTextFields().Count);
        OdfTextField sequence = document.FindTextField(OdfTextFieldKind.Sequence, "Figure")!;
        sequence.Name = "Illustration";
        sequence.NumberFormat = "I";
        variable.DisplayText = "Bob";
        OdfTextField database = document.FindTextField(OdfTextFieldKind.DatabaseDisplay, "Customers")!;
        database.ColumnName = "DisplayName";
        database.DatabaseName = "ReportingDb";

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream, "inline-fields.odt");
        Assert.Equal(12, reloaded.GetTextFields().Count);
        Assert.Equal("I", reloaded.FindTextField(OdfTextFieldKind.Sequence, "Illustration")!.NumberFormat);
        OdfTextField loadedVariable = reloaded.FindTextField(OdfTextFieldKind.VariableSet, "Customer")!;
        Assert.Contains("Bob", loadedVariable.DisplayText, StringComparison.Ordinal);
        Assert.NotNull(FindDescendant(loadedVariable.Node, "extension", foreignNamespace));
        OdfTextField loadedDatabase = reloaded.FindTextField(OdfTextFieldKind.DatabaseDisplay, "Customers")!;
        Assert.Equal("DisplayName", loadedDatabase.ColumnName);
        Assert.Equal("ReportingDb", loadedDatabase.DatabaseName);
        OdfTextField date = reloaded.FindTextField(OdfTextFieldKind.Date)!;
        Assert.True(reloaded.RemoveTextField(date));
        Assert.False(reloaded.RemoveTextField(date));
        Assert.Equal(11, reloaded.ClearTextFields());
        Assert.Equal(0, reloaded.ClearTextFields());
        Assert.Empty(reloaded.GetTextFields());
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

    private static void AddInheritedGraphicStyle(
        OdfDocument document,
        string parentName,
        string childName,
        string fillColor)
    {
        OdfNode? automaticStyles = FindDescendant(
            document.ContentRoot,
            "automatic-styles",
            OdfNamespaces.Office);
        if (automaticStyles is null)
        {
            automaticStyles = new OdfNode(
                OdfNodeType.Element,
                "automatic-styles",
                OdfNamespaces.Office,
                "office");
            OdfNode? body = FindDescendant(document.ContentRoot, "body", OdfNamespaces.Office);
            if (body is not null && body.Parent == document.ContentRoot)
                document.ContentRoot.InsertBefore(automaticStyles, body);
            else
                document.ContentRoot.AppendChild(automaticStyles);
        }
        var parent = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        parent.SetAttribute("name", OdfNamespaces.Style, parentName, "style");
        parent.SetAttribute("family", OdfNamespaces.Style, "graphic", "style");
        var properties = new OdfNode(OdfNodeType.Element, "graphic-properties", OdfNamespaces.Style, "style");
        properties.SetAttribute("fill-color", OdfNamespaces.Draw, fillColor, "draw");
        parent.AppendChild(properties);
        var child = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        child.SetAttribute("name", OdfNamespaces.Style, childName, "style");
        child.SetAttribute("family", OdfNamespaces.Style, "graphic", "style");
        child.SetAttribute("parent-style-name", OdfNamespaces.Style, parentName, "style");
        automaticStyles.AppendChild(parent);
        automaticStyles.AppendChild(child);
        document.StyleEngine.RebuildStyleIndex();
    }

    private static IReadOnlyList<string> GetDrawingObjectIds(OdfDrawPage page)
    {
        List<string> ids = [];
        foreach (OdfNode child in page.Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.NamespaceUri == OdfNamespaces.Draw &&
                child.LocalName != "layer-set")
            {
                string? id = child.GetAttribute("id", OdfNamespaces.Draw) ??
                    child.GetAttribute("id", OdfNamespaces.Xml);
                if (!string.IsNullOrEmpty(id))
                    ids.Add(id!);
            }
        }
        return ids.AsReadOnly();
    }
}

using System.Text;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定試算表高階 API 的整合測試。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Scenario)]
public class SpreadsheetHighLevelApiTests
{
    /// <summary>
    /// 驗證嵌入圖表建立 (AddChart) API 的正確性與封裝子文件結構。
    /// </summary>
    [Fact]
    public void AddChartCreatesCorrectSubDocuments()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");

        var chart = new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "銷售折線圖",
            DataRange = new OdfCellRange(0, 0, 9, 1, "Sheet1"), // A1:B10
            HasLegend = true
        };

        // 錨定在 D1
        document.AddChart("Sheet1", new OdfCellAddress(0, 3, "Sheet1"), chart);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);

        // 1. 驗證 content.xml 內的主框架參照
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string contentXml = reader.ReadToEnd();

        Assert.Contains("table:shapes", contentXml);
        Assert.Contains("draw:frame", contentXml);
        Assert.Contains("draw:object xlink:href=\"./Object 1\"", contentXml);
        Assert.DoesNotContain("table:start-cell-address", contentXml);

        // 2. 驗證 Object 1/mimetype 媒體類型
        byte[] mimeBytes = package.ReadEntry("Object 1/mimetype");
        string mimeText = Encoding.UTF8.GetString(mimeBytes).Trim();
        Assert.Equal("application/vnd.oasis.opendocument.chart", mimeText);

        // 3. 驗證 Object 1/content.xml 內容
        byte[] objContentBytes = package.ReadEntry("Object 1/content.xml");
        string objContentXml = Encoding.UTF8.GetString(objContentBytes);

        Assert.Contains("chart:chart", objContentXml);
        Assert.Contains("chart:class=\"chart:line\"", objContentXml);
        Assert.Contains("table:cell-range-address=", objContentXml);
        Assert.Contains("Sheet1", objContentXml);
        Assert.Contains("A1", objContentXml);
        Assert.Contains("B10", objContentXml);
        Assert.Contains("銷售折線圖", objContentXml);
        Assert.Contains("chart:legend-position=\"end\"", objContentXml);
        Assert.Contains("chart:plot-area", objContentXml);
        Assert.Contains("chart:axis", objContentXml);
    }

    /// <summary>
    /// 驗證資料驗證 (AddDataValidation) API 的全域宣告與儲存格關聯。
    /// </summary>
    [Fact]
    public void AddDataValidationAppliesCorrectRules()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");

        var validation = new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 0, 9, 0, "Sheet1"), // A1:A10
            Condition = OdfValidationCondition.IntegerBetween,
            Formula1 = "1",
            Formula2 = "100",
            ErrorMessage = "請輸入 1 至 100 的整數！",
            ErrorTitle = "無效輸入",
            AlertStyle = OdfValidationAlertStyle.Stop
        };

        document.AddDataValidation("Sheet1", validation);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string contentXml = reader.ReadToEnd();

        // 1. 驗證全域的 content-validations 宣告與條件
        Assert.Contains("table:content-validations", contentXml);
        Assert.Contains("table:content-validation", contentXml);
        Assert.Contains("table:name=\"val_1\"", contentXml);
        Assert.Contains("condition=\"of:cell-content-is-whole-number() and cell-content-is-between(1,100)\"", contentXml);
        Assert.Contains("table:error-message", contentXml);
        Assert.Contains("table:title=\"無效輸入\"", contentXml);
        Assert.Contains("table:message-type=\"stop\"", contentXml);
        Assert.Contains("<text:p>請輸入 1 至 100 的整數！</text:p>", contentXml);

        // 2. 驗證 A1 儲存格已正確被附加了此驗證名稱
        // 在 ODS 中列與欄均被展開，A1 為第 1 列 (row) 的第 1 個 cell
        Assert.Contains("table:content-validation-name=\"val_1\"", contentXml);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetDataValidations"/> 可讀回資料驗證規則。
    /// </summary>
    [Fact]
    public void GetDataValidations_RoundTripsAfterAdd()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");
        document.AddDataValidation("Sheet1", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 0, 4, 0, "Sheet1"),
            Condition = OdfValidationCondition.IntegerBetween,
            Formula1 = "1",
            Formula2 = "50",
            ErrorMessage = "範圍錯誤",
            ErrorTitle = "輸入錯誤",
            AlertStyle = OdfValidationAlertStyle.Warning,
        });

        Assert.Single(document.GetDataValidations());
        OdfDataValidationInfo info = document.GetDataValidations()[0];
        Assert.Equal("val_1", info.Name);
        Assert.True(info.TryGetCondition(out OdfValidationCondition condition));
        Assert.Equal(OdfValidationCondition.IntegerBetween, condition);
        Assert.Equal("範圍錯誤", info.ErrorMessage);
        Assert.Equal("warning", info.AlertStyle);
        Assert.Equal(5, info.AppliedRanges.Count);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetEmbeddedChartDocument"/> 可取得並編輯嵌入圖表。
    /// </summary>
    [Fact]
    public void GetEmbeddedChartDocument_AllowsEditingTitleAndType()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");
        document.AddChart("Sheet1", new OdfCellAddress(0, 3, "Sheet1"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "原始標題",
            DataRange = new OdfCellRange(0, 0, 3, 1, "Sheet1"),
        });

        OdfEmbeddedChartInfo chartInfo = Assert.Single(document.GetEmbeddedCharts());
        OdfChartDocument chartDoc = document.GetEmbeddedChartDocument(chartInfo);
        chartDoc.SetChartType(OdfChartType.Line);
        chartDoc.ChartTitle = "修訂標題";

        Assert.Equal("chart:line", chartDoc.ChartClass);
        Assert.Equal("修訂標題", chartDoc.ChartTitle);
        Assert.Equal(OdfChartType.Line, chartDoc.GetChartDefinition().ChartType);
    }

    /// <summary>
    /// 驗證嵌入圖表可設定座標軸與序列進階屬性。
    /// </summary>
    [Fact]
    public void GetEmbeddedChartDocument_AllowsAxisAndSeriesAdvancedEditing()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");
        document.AddChart("Sheet1", new OdfCellAddress(0, 3, "Sheet1"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "季度銷售",
            DataRange = new OdfCellRange(0, 0, 5, 1, "Sheet1"),
        });

        OdfChartDocument chartDoc = document.GetEmbeddedChartDocument(Assert.Single(document.GetEmbeddedCharts()));
        chartDoc.SetAxisGrid("y", OdfChartGridKind.Major, true);
        chartDoc.SetAxisMaximum("y", 500);

        if (chartDoc.SeriesCount > 0)
        {
            chartDoc.GetSeriesEditor(0).SeriesClass = "chart:bar";
        }

        OdfChartAxisInfo? axisInfo = chartDoc.FindAxisInfo("y");
        Assert.NotNull(axisInfo);
        Assert.True(axisInfo!.HasMajorGrid);
        Assert.Equal(500, axisInfo.Maximum);
    }

    /// <summary>
    /// 驗證 <see cref="OdfChartDocument.ClearSeries"/> 與 <see cref="OdfChartDocument.ApplyDefinition"/>
    /// 對嵌入圖表所做的修改，只呼叫父文件儲存也可正確持久化並於重新載入後讀回。
    /// </summary>
    [Fact]
    public void EmbeddedChartDocument_ClearSeriesAndApplyDefinition_PersistsWhenParentSaves()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        sheet.Cells["A1"].CellValue = "月份";
        sheet.Cells["B1"].CellValue = "營收";
        sheet.Cells["A2"].CellValue = "一月";
        sheet.Cells["B2"].CellValue = 120d;

        document.AddChart("Sheet1", new OdfCellAddress(4, 0, "Sheet1"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "原始標題",
            DataRange = new OdfCellRange(0, 0, 1, 1, "Sheet1"),
            HasLegend = false,
        });

        OdfEmbeddedChartInfo chartInfo = Assert.Single(document.GetEmbeddedCharts());
        OdfChartDocument chartDoc = document.GetEmbeddedChartDocument(chartInfo);

        // ClearSeries 應移除既有資料序列，ApplyDefinition 應同時更新類型、標題與圖例。
        chartDoc.ClearSeries();
        chartDoc.ApplyDefinition(new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "套用後標題",
            DataRange = new OdfCellRange(0, 0, 1, 1, "Sheet1"),
            HasLegend = true,
        });

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfEmbeddedChartInfo loadedInfo = Assert.Single(loaded.GetEmbeddedCharts());
        Assert.Equal(OdfChartType.Line, loadedInfo.ChartType);
        Assert.Equal("套用後標題", loadedInfo.Title);

        OdfChartDocument loadedChartDoc = loaded.GetEmbeddedChartDocument(loadedInfo);
        Assert.Equal("top", loadedChartDoc.LegendPosition);
        Assert.Equal(1, loadedChartDoc.SeriesCount);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetEmbeddedCharts"/> 可讀回嵌入圖表摘要。
    /// </summary>
    [Fact]
    public void GetEmbeddedCharts_RoundTripsAfterAdd()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");
        document.AddChart("Sheet1", new OdfCellAddress(0, 3, "Sheet1"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "季度銷售",
            DataRange = new OdfCellRange(0, 0, 5, 1, "Sheet1"),
            HasLegend = true,
        });

        Assert.Single(document.GetEmbeddedCharts());
        OdfEmbeddedChartInfo chart = document.GetEmbeddedCharts()[0];
        Assert.Equal("Sheet1", chart.SheetName);
        Assert.Equal(OdfChartType.Bar, chart.ChartType);
        Assert.Equal("季度銷售", chart.Title);
        Assert.Equal("Object 1/", chart.ObjectPath);
        Assert.False(chart.TryGetAnchorAddress(out _));
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetConditionalFormats"/> 可聚合所有工作表的條件格式規則。
    /// </summary>
    [Fact]
    public void GetConditionalFormats_AggregatesAllSheets()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet1 = document.AddSheet("Sheet1");
        OdfTableSheet sheet2 = document.AddSheet("Sheet2");
        sheet1.AddDataBarFormat(new OdfCellRange(0, 0, 4, 0), new OdfColor("#638EC6"));
        sheet2.AddColorScaleFormat(
            new OdfCellRange(0, 0, 2, 0),
            new OdfColor("#ff0000"),
            new OdfColor("#00ff00"));

        Assert.Equal(2, document.GetConditionalFormats().Count);
        Assert.Contains(document.GetConditionalFormats(), f => f.Kind == OdfConditionalFormatKind.DataBar);
        Assert.Contains(document.GetConditionalFormats(), f => f.Kind == OdfConditionalFormatKind.ColorScale);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetSparklineGroups"/> 可聚合所有工作表的走勢圖群組。
    /// </summary>
    [Fact]
    public void GetSparklineGroups_AggregatesAllSheets()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = 10d;
        sheet.Cells["A2"].CellValue = 20d;
        sheet.AddSparklineGroup(
            OdfCellRange.ParseExcel("A1:A2"),
            OdfCellAddress.ParseExcel("B1"),
            SparklineType.Line);

        Assert.Single(document.GetSparklineGroups());
        Assert.Equal(SparklineType.Line, document.GetSparklineGroups()[0].Type);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetNamedRanges"/> 可聚合文件層與工作表層命名範圍。
    /// </summary>
    [Fact]
    public void GetNamedRanges_AggregatesDocumentAndSheetScopes()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        document.AddNamedRange(
            "GlobalRange",
            new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(2, 0, "Sheet1")));
        sheet.AddNamedRange(
            "LocalRange",
            new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(1, 0, "Sheet1")));

        Assert.Equal(2, document.GetNamedRanges().Count);
        Assert.Contains(document.GetNamedRanges(), r => r.Name == "GlobalRange");
        Assert.Contains(document.GetNamedRanges(), r => r.Name == "LocalRange");
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetNamedExpressions"/> 可聚合文件層與工作表層具名運算式。
    /// </summary>
    [Fact]
    public void GetNamedExpressions_AggregatesDocumentAndSheetScopes()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        document.AddNamedExpression("GlobalSum", "SUM(Sheet1.A1:Sheet1.A3)");
        sheet.AddNamedExpression("LocalCount", "of:=COUNTA([.A1:.A3])", new OdfCellAddress(0, 0, "Sheet1"));

        Assert.Equal(2, document.GetNamedExpressions().Count);
        Assert.Contains(document.GetNamedExpressions(), e => e.Name == "GlobalSum");
        Assert.Contains(document.GetNamedExpressions(), e => e.Name == "LocalCount");
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetDatabaseRanges"/> 可讀回資料庫範圍、排序與篩選設定。
    /// </summary>
    [Fact]
    public void GetDatabaseRanges_RoundTripsAfterAdd()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");
        OdfDatabaseRange dbRange = document.AddDatabaseRange(
            "SalesData",
            new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 2, "Sheet1")));
        dbRange.SetSort((0, true), (1, false));
        dbRange.SetFilter((0, "=", "Active"), (2, ">", "100"));

        Assert.Single(document.GetDatabaseRanges());
        OdfDatabaseRangeInfo info = document.GetDatabaseRanges()[0];
        Assert.Equal("SalesData", info.Name);
        Assert.Equal(2, info.SortRules.Count);
        Assert.Equal(0, info.SortRules[0].FieldNumber);
        Assert.True(info.SortRules[0].Ascending);
        Assert.Equal(2, info.FilterConditions.Count);
        Assert.Equal("=", info.FilterConditions[0].Operator);
        Assert.Equal("Active", info.FilterConditions[0].Value);
    }

    /// <summary>
    /// 驗證工作表可一鍵建立自動篩選與排序資料庫範圍。
    /// </summary>
    [Fact]
    public void SheetAutoFilterAndSortCreateDatabaseRanges()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");

        OdfDatabaseRange filterRange = sheet.AutoFilter("A1:C10");
        filterRange.SetFilter((1, "=", "Active"));
        sheet.Sort("A1:C10", (2, false), (0, true));
        sheet.Ranges["D1:E5"].AutoFilter().Sort((1, true));

        OdfDatabaseRangeInfo[] ranges = document.GetDatabaseRanges().ToArray();
        Assert.Equal(4, ranges.Length);

        OdfDatabaseRangeInfo autoFilter = Assert.Single(ranges, range => range.Name == "Sheet1_AutoFilter_1");
        Assert.True(autoFilter.DisplayFilterButtons);
        Assert.Equal("Sheet1.A1:.C10", autoFilter.TargetRangeAddress);
        Assert.Single(autoFilter.FilterConditions);
        Assert.Equal(1, autoFilter.FilterConditions[0].FieldNumber);
        Assert.Equal("Active", autoFilter.FilterConditions[0].Value);

        OdfDatabaseRangeInfo sort = Assert.Single(ranges, range => range.Name == "Sheet1_Sort_2");
        Assert.False(sort.DisplayFilterButtons);
        Assert.Equal(2, sort.SortRules.Count);
        Assert.Equal(2, sort.SortRules[0].FieldNumber);
        Assert.False(sort.SortRules[0].Ascending);
        Assert.Equal(0, sort.SortRules[1].FieldNumber);
        Assert.True(sort.SortRules[1].Ascending);

        OdfDatabaseRangeInfo selectionFilter = Assert.Single(ranges, range => range.Name == "Sheet1_AutoFilter_3");
        OdfDatabaseRangeInfo selectionSort = Assert.Single(ranges, range => range.Name == "Sheet1_Sort_4");
        Assert.Equal("Sheet1.D1:.E5", selectionFilter.TargetRangeAddress);
        Assert.Equal("Sheet1.D1:.E5", selectionSort.TargetRangeAddress);
        Assert.True(selectionFilter.DisplayFilterButtons);
        Assert.Single(selectionSort.SortRules);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, fileName: "filter-sort.ods");

        OdfDatabaseRangeInfo reloadedFilter = Assert.Single(
            reloaded.GetDatabaseRanges(),
            range => range.Name == "Sheet1_AutoFilter_1");
        Assert.True(reloadedFilter.DisplayFilterButtons);
        Assert.Single(reloadedFilter.FilterConditions);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetPivotTables"/> 可讀回樞紐分析表欄位、排序與篩選設定。
    /// </summary>
    [Fact]
    public void GetPivotTables_RoundTripsAfterBuild()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        var sourceRange = new OdfCellRange(
            new OdfCellAddress(0, 0, "Sheet1"),
            new OdfCellAddress(9, 3, "Sheet1"));

        new OdfPivotTableBuilder("SalesPivot", sourceRange, new OdfCellAddress(12, 0, "Sheet1"), sheet)
            .AddRowField("Category")
            .AddColumnField("Region")
            .AddDataField("Sales", OdfPivotFunction.Sum)
            .AddPageField("Year")
            .AddSortInfo("Sales", ascending: false)
            .AddFilter("Category", OdfPivotFilterOperator.Equal, "Electronics")
            .Build();

        Assert.Single(document.GetPivotTables());
        OdfPivotTableInfo info = document.GetPivotTables()[0];
        Assert.Equal("Sheet1", info.SheetName);
        Assert.Equal("SalesPivot", info.Name);
        Assert.Equal(4, info.Fields.Count);
        Assert.Contains(info.Fields, f => f.Orientation == "row" && f.SourceFieldName == "Category");
        Assert.Contains(info.Fields, f => f.Orientation == "data" && f.Function == "sum");
        Assert.Single(info.SortFields);
        Assert.False(info.SortFields[0].Ascending);
        Assert.Single(info.FilterConditions);
        Assert.Equal("=", info.FilterConditions[0].Operator);
        Assert.Equal("Electronics", info.FilterConditions[0].Value);
    }

    /// <summary>
    /// 驗證 <see cref="OdfPivotTableBuilder.WithRowHeaders"/> 設定，以及
    /// <see cref="OdfPivotTableInfo.TryGetSourceRange"/>／<see cref="OdfPivotTableInfo.TryGetTargetStart"/>
    /// 在文件實際儲存並重新載入後仍能正確讀回。
    /// </summary>
    [Fact]
    public void GetPivotTables_WithRowHeadersFalse_PersistsAfterSaveAndReload()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        var sourceRange = new OdfCellRange(
            new OdfCellAddress(0, 0, "Sheet1"),
            new OdfCellAddress(9, 3, "Sheet1"));
        var targetStart = new OdfCellAddress(12, 0, "Sheet1");

        new OdfPivotTableBuilder("RowHeaderPivot", sourceRange, targetStart, sheet)
            .WithRowHeaders(false)
            .WithColumnHeaders(true)
            .AddRowField("Category")
            .AddDataField("Sales", OdfPivotFunction.Sum)
            .Build();

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfPivotTableInfo info = Assert.Single(loaded.GetPivotTables());
        Assert.Equal("Sheet1", info.SheetName);
        Assert.True(info.HasRowHeaders);
        Assert.True(info.HasColumnHeaders);

        Assert.True(info.TryGetSourceRange(out OdfCellRange resolvedSourceRange));
        Assert.Equal(sourceRange.StartAddress.Row, resolvedSourceRange.StartAddress.Row);
        Assert.Equal(sourceRange.StartAddress.Column, resolvedSourceRange.StartAddress.Column);
        Assert.Equal(sourceRange.EndAddress.Row, resolvedSourceRange.EndAddress.Row);
        Assert.Equal(sourceRange.EndAddress.Column, resolvedSourceRange.EndAddress.Column);

        Assert.True(info.TryGetTargetStart(out OdfCellAddress resolvedTargetStart));
        Assert.Equal(targetStart.Row, resolvedTargetStart.Row);
        Assert.Equal(targetStart.Column, resolvedTargetStart.Column);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetSplitPanes"/> 可讀回各工作表分割窗格設定。
    /// </summary>
    [Fact]
    public void GetSplitPanes_RoundTripsAfterSplit()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");
        sheet.SplitPanes(3, 2);

        Assert.Single(document.GetSplitPanes());
        OdfSheetSplitPanesInfo info = document.GetSplitPanes()[0];
        Assert.Equal("Report", info.SheetName);
        Assert.Equal(new OdfSplitPanes(3, 2), info.SplitPanes);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetFrozenPanes"/> 可讀回各工作表凍結窗格設定。
    /// </summary>
    [Fact]
    public void GetFrozenPanes_RoundTripsAfterFreeze()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");
        sheet.FreezePanes(2, 1);

        Assert.Single(document.GetFrozenPanes());
        OdfSheetFrozenPanesInfo info = document.GetFrozenPanes()[0];
        Assert.Equal("Report", info.SheetName);
        Assert.Equal(new OdfFrozenPanes(2, 1), info.FrozenPanes);
    }

    /// <summary>
    /// 驗證 <see cref="OdfTableSheet.UnmergeCells(string)"/> 會移除 span 屬性並還原 covered cell。
    /// </summary>
    [Fact]
    public void UnmergeCellsRestoresCoveredCells()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");

        sheet.MergeCells(new OdfCellRange(0, 0, 1, 1));
        Assert.Equal("2", sheet.Cells["A1"].Node.GetAttribute("number-columns-spanned", OdfNamespaces.Table));
        Assert.Equal("covered-table-cell", sheet.Cells["B2"].Node.LocalName);

        sheet.UnmergeCells("A1:B2");

        Assert.Null(sheet.Cells["A1"].Node.GetAttribute("number-columns-spanned", OdfNamespaces.Table));
        Assert.Null(sheet.Cells["A1"].Node.GetAttribute("number-rows-spanned", OdfNamespaces.Table));
        Assert.Equal("table-cell", sheet.Cells["B1"].Node.LocalName);
        Assert.Equal("table-cell", sheet.Cells["A2"].Node.LocalName);
        Assert.Equal("table-cell", sheet.Cells["B2"].Node.LocalName);
    }

    /// <summary>
    /// 驗證範圍選取的 <see cref="OdfCellRangeSelection.Unmerge"/> 入口會委派取消合併。
    /// </summary>
    [Fact]
    public void RangeSelectionUnmergeDelegatesToSheet()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");

        sheet.Ranges["A1:B1"].Merge();
        Assert.Equal("covered-table-cell", sheet.Cells["B1"].Node.LocalName);

        sheet.Ranges["A1:B1"].Unmerge();

        Assert.Null(sheet.Cells["A1"].Node.GetAttribute("number-columns-spanned", OdfNamespaces.Table));
        Assert.Equal("table-cell", sheet.Cells["B1"].Node.LocalName);
    }

    /// <summary>
    /// 驗證批次插入空列會以 <c>table:number-rows-repeated</c> 寫成單一列節點。
    /// </summary>
    [Fact]
    public void InsertRowsUsesRepeatedRowForBatchEmptyRows()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");

        sheet.InsertRows(0, 4);

        TableTableRowElement row = Assert.IsType<TableTableRowElement>(Assert.Single(
            sheet.TableNode.Children,
            child => child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table));
        Assert.Equal(4, row.NumberRowsRepeated);
    }

    /// <summary>
    /// 驗證工作表可批次複製與移動列，並保留來源列內容順序。
    /// </summary>
    [Fact]
    public void CopyRowsAndMoveRowsPreserveRowContentOrder()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");
        sheet.Cells["A1"].CellValue = "A";
        sheet.Cells["A2"].CellValue = "B";
        sheet.Cells["A3"].CellValue = "C";

        sheet.CopyRows(sourcePosition: 0, count: 2, targetPosition: 3);

        Assert.Equal(["A", "B", "C", "A", "B"], ReadFirstColumnText(sheet));

        sheet.MoveRows(sourcePosition: 0, count: 2, targetPosition: 3);

        Assert.Equal(["C", "A", "B", "A", "B"], ReadFirstColumnText(sheet));
    }

    /// <summary>
    /// 驗證工作表列操作會同步位移尚未具現化的稀疏匯入資料。
    /// </summary>
    [Fact]
    public void SheetRowOperationsShiftSparseImportDataWithoutMaterializingCells()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");
        var table = Assert.IsType<TableTableElement>(sheet.TableNode);
        DataTable dataTable = new();
        dataTable.Columns.Add("Name", typeof(string));
        dataTable.Columns.Add("Qty", typeof(int));
        for (int row = 0; row < 130; row++)
        {
            dataTable.Rows.Add($"Item {row}", row);
        }

        table.ImportData(dataTable);
        table.SetSparseCellStyle(129, 1, "tail-style");
        table.SetSparseCellFormula(129, 1, "of:=[.A130]");

        sheet.InsertRows(1, 2);
        sheet.CopyRows(sourcePosition: 3, count: 1, targetPosition: 5);
        sheet.MoveRows(sourcePosition: 0, count: 1, targetPosition: 4);
        sheet.DeleteRows(1, 1);

        Dictionary<(int Row, int Column), OdfCellData> views = ReadCellViews(table);
        Assert.DoesNotContain(table.TableTableRowChildElements, row => row.TableTableCellChildElements.Any());
        Assert.Equal("Item 1", views[(1, 0)].Text);
        Assert.Equal("Item 2", views[(2, 0)].Text);
        Assert.Equal("Item 0", views[(3, 0)].Text);
        Assert.Equal("Item 1", views[(4, 0)].Text);
        Assert.Equal(129, views[(131, 1)].Number);
        Assert.Equal("tail-style", views[(131, 1)].StyleName);
        Assert.Equal("of:=[.A130]", views[(131, 1)].Formula);
    }

    /// <summary>
    /// 驗證工作表層級 <see cref="OdfTableSheet.PruneAndCollect"/> 會從試算表 DOM 移除該工作表。
    /// </summary>
    [Fact]
    public void SheetPruneAndCollectRemovesSheetFromWorkbook()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet first = document.AddSheet("Keep");
        OdfTableSheet second = document.AddSheet("Archive");
        second.Cells["A1"].CellValue = "待剪裁";

        int prunedCount = second.PruneAndCollect();

        Assert.True(prunedCount >= 3);
        Assert.Single(document.GetSheets());
        Assert.NotNull(document.FindSheet("Keep"));
        Assert.Null(document.FindSheet("Archive"));
        Assert.Equal("Keep", first.Name);
    }

    /// <summary>
    /// 驗證工作表剪裁會同步釋放高階集合 facade 快取。
    /// </summary>
    [Fact]
    public void SheetPruneAndCollectReleasesFacadeCaches()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Archive");

        _ = sheet.Cells;
        _ = sheet.Rows;
        _ = sheet.Columns;
        _ = sheet.Ranges;
        Assert.Equal(4, sheet.FacadeCacheCount);

        sheet.PruneAndCollect();

        Assert.Equal(0, sheet.FacadeCacheCount);
        Assert.Empty(document.GetSheets());
    }

    /// <summary>
    /// 驗證工作表 facade 快取可在不剪裁 DOM 的情況下主動釋放，後續仍可重新存取資料。
    /// </summary>
    [Fact]
    public void SheetReleaseFacadeCachesKeepsWorkbookContent()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "保留資料";

        _ = sheet.Rows;
        _ = sheet.Columns;
        _ = sheet.Ranges;
        Assert.Equal(4, sheet.FacadeCacheCount);

        sheet.ReleaseFacadeCaches();

        Assert.Equal(0, sheet.FacadeCacheCount);
        Assert.Single(document.GetSheets());
        Assert.Equal("保留資料", sheet.Cells["A1"].CellValue);
        Assert.Equal(1, sheet.FacadeCacheCount);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.AdoptSheet"/> 會將來源工作表移入目標活頁簿並可往返儲存。
    /// </summary>
    [Fact]
    public void AdoptSheetMovesSheetBetweenWorkbooksAndRoundTrips()
    {
        using var source = SpreadsheetDocument.Create();
        OdfTableSheet sourceSheet = source.AddSheet("Imported");
        sourceSheet.Cells["A1"].CellValue = "跨文件資料";
        sourceSheet.GroupRows(0, 0, collapsed: true);

        using var target = SpreadsheetDocument.Create();
        target.AddSheet("Main");

        OdfTableSheet adopted = target.Worksheets.Adopt(sourceSheet, "Adopted");

        Assert.Null(source.FindSheet("Imported"));
        Assert.Equal(2, target.Worksheets.Count);
        Assert.Equal("Adopted", adopted.Name);
        Assert.Equal("跨文件資料", adopted.Cells["A1"].CellValue);

        using var stream = new MemoryStream();
        target.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, "adopted.ods");
        OdfTableSheet reloadedSheet = reloaded.Worksheets["Adopted"];

        Assert.Equal("跨文件資料", reloadedSheet.Cells["A1"].CellValue);
        string contentXml = ReadZipEntry(stream, "content.xml");
        Assert.Contains("table:table-row-group", contentXml);
        Assert.Contains("table:display=\"false\"", contentXml);
    }

    private static string[] ReadFirstColumnText(OdfTableSheet sheet)
        => sheet.TableNode.Children
            .Where(row => row.LocalName == "table-row" && row.NamespaceUri == OdfNamespaces.Table)
            .Select(row => row.Children.First(cell => cell.LocalName == "table-cell" && cell.NamespaceUri == OdfNamespaces.Table).TextContent ?? string.Empty)
            .ToArray();

    private static Dictionary<(int Row, int Column), OdfCellData> ReadCellViews(TableTableElement table)
    {
        var cells = new Dictionary<(int Row, int Column), OdfCellData>();
        foreach (OdfCellView view in table.EnumerateCellViews())
        {
            cells[(view.RowIndex, view.ColumnIndex)] = view.Data;
        }

        return cells;
    }

    private static string ReadZipEntry(Stream stream, string entryName)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry entry = archive.GetEntry(entryName)!;
        using Stream entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 驗證 <see cref="OdfExternalLinkManager"/> 可透過載入委派解析跨文件公式參照。
    /// </summary>
    [Fact]
    public void ExternalLinkManagerResolvesFormulaReferences()
    {
        using var external = SpreadsheetDocument.Create();
        OdfTableSheet externalSheet = external.AddSheet("Sheet1");
        externalSheet.Cells["A1"].CellValue = 41d;

        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Main");
        sheet.Cells["A1"].Formula = "='file:///external.ods#$Sheet1'!A1+1";
        document.ExternalLinks.DocumentResolver = documentId =>
            documentId == "file:///external.ods" ? external : null;

        document.EvaluateFormulas();

        Assert.Equal(42d, sheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// 驗證公式重算會將同一拓撲層的互不相依公式以並行 DAG 批次處理。
    /// </summary>
    [Fact]
    public void EvaluateFormulasUsesParallelDagLevelsForIndependentBranches()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Main");
        sheet.Cells["A1"].CellValue = 1d;
        sheet.Cells["B1"].CellValue = 2d;
        sheet.Cells["A2"].Formula = "=A1+1";
        sheet.Cells["B2"].Formula = "=B1+1";
        sheet.Cells["C3"].Formula = "=A2+B2";

        document.EvaluateFormulas();

        Assert.Equal(2d, sheet.Cells["A2"].CellValue);
        Assert.Equal(3d, sheet.Cells["B2"].CellValue);
        Assert.Equal(5d, sheet.Cells["C3"].CellValue);
        Assert.True(FormulaDocumentEvaluationEngine.LastParallelFormulaLevelCountForTests >= 2);
        Assert.True(FormulaDocumentEvaluationEngine.LastParallelFormulaMaxLevelWidthForTests >= 2);
        Assert.True(FormulaDocumentEvaluationEngine.LastParallelFormulaWorkerDegreeForTests >= 1);
    }

    /// <summary>
    /// 驗證存檔時公式重算會使用試算表文件上的跨文件連結管理器。
    /// </summary>
    [Fact]
    public void EvaluateFormulasOnSaveUsesSpreadsheetExternalLinks()
    {
        using var external = SpreadsheetDocument.Create();
        OdfTableSheet externalSheet = external.AddSheet("Sheet1");
        externalSheet.Cells["A1"].CellValue = 41d;

        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Main");
        sheet.Cells["A1"].Formula = "='file:///external.ods#$Sheet1'!A1+1";
        document.ExternalLinks.DocumentResolver = documentId =>
            documentId == "file:///external.ods" ? external : null;

        using var stream = new MemoryStream();
        document.SaveToStream(stream, new OdfSaveOptions { EvaluateFormulasOnSave = true });

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        OdfNode root = OdfXmlReader.Parse(contentStream);
        OdfNode cell = root.Descendants()
            .First(node => node.LocalName == "table-cell" && node.NamespaceUri == OdfNamespaces.Table);

        Assert.Equal("float", cell.GetAttribute("value-type", OdfNamespaces.Office));
        Assert.Equal("42", cell.GetAttribute("value", OdfNamespaces.Office));
        Assert.Equal("42", cell.TextContent);
    }

    /// <summary>
    /// 驗證外部連結快取值會保存至 settings.xml，並在重新載入後供公式評估使用。
    /// </summary>
    [Fact]
    public void ExternalLinkCachedValuesRoundTripThroughSettings()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Main");
        sheet.Cells["A1"].Formula = "='file:///external.ods#$Sheet1'!A1+1";
        document.ExternalLinks.SetCachedValue(
            "file:///external.ods",
            "Sheet1",
            new OdfCellAddress(0, 0),
            41d);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);

        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(stream, fileName: "cached.ods");
        OdfTableSheet reloadedSheet = reloaded.FindSheet("Main")!;
        reloaded.EvaluateFormulas();

        Assert.Equal(42d, reloadedSheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.GetPrintAreas"/> 可讀回各工作表列印範圍。
    /// </summary>
    [Fact]
    public void GetPrintAreas_RoundTripsAfterSet()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Report");
        sheet.SetPrintArea(new OdfCellRange(0, 0, 9, 4));

        Assert.Single(document.GetPrintAreas());
        OdfSheetPrintAreaInfo area = document.GetPrintAreas()[0];
        Assert.Equal("Report", area.SheetName);
        Assert.True(area.TryGetRange(out OdfCellRange range));
        Assert.Equal(9, range.EndAddress.Row);
        Assert.Equal(4, range.EndAddress.Column);
    }

    /// <summary>
    /// 驗證列印範圍、標題列欄、分頁符與縮放設定會寫入 ODS XML。
    /// </summary>
    [Fact]
    public void PrintSettingsApiWritesExpectedXml()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");

        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                sheet.GetCell(row, column).SetValue(row * 10d + column);
            }
        }

        sheet.SetPrintArea(new OdfCellRange(0, 0, 9, 2));
        sheet.SetPrintTitleRows(0, 0);
        sheet.SetPrintTitleColumns(0, 0);
        sheet.InsertRowPageBreak(afterRow: 1);
        sheet.InsertColumnPageBreak(afterCol: 1);
        Assert.True(sheet.RemoveRowPageBreak(afterRow: 1));
        Assert.False(sheet.RemoveRowPageBreak(afterRow: 1));
        Assert.True(sheet.RemoveColumnPageBreak(afterCol: 1));
        Assert.False(sheet.RemoveColumnPageBreak(afterCol: 1));
        sheet.InsertRowPageBreak(afterRow: 1);
        sheet.InsertColumnPageBreak(afterCol: 1);
        sheet.SetFitToPage(maxPagesWide: 1, maxPagesTall: 0);

        Assert.NotNull(sheet.GetPrintArea());

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var contentReader = new StreamReader(contentStream);
        string contentXml = contentReader.ReadToEnd();

        using Stream stylesStream = package.GetEntryStream("styles.xml");
        using var stylesReader = new StreamReader(stylesStream);
        string stylesXml = stylesReader.ReadToEnd();

        Assert.Contains("table:print-ranges=", contentXml);
        Assert.Contains("table:header-rows", contentXml);
        Assert.Contains("table:header-columns", contentXml);
        Assert.Contains("fo:break-before=\"page\"", contentXml);
        Assert.Contains("style:scale-to-pages=\"1\"", stylesXml);
    }

    /// <summary>
    /// 驗證工作表可設定 RTL 書寫方向。
    /// </summary>
    [Fact]
    public void SheetWritingModeWritesTableStyle()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("RTL");
        sheet.WritingMode = OdfWritingMode.RlTb;

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream stylesStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(stylesStream);
        string contentXml = reader.ReadToEnd();

        Assert.Equal(OdfWritingMode.RlTb, sheet.WritingMode);
        Assert.Contains("style:writing-mode=\"rl-tb\"", contentXml);
    }

    /// <summary>
    /// 驗證儲存格超連結 SetHyperlink / GetHyperlinkUrl / RemoveHyperlink API。
    /// </summary>
    [Fact]
    public void CellHyperlinkApiWorksCorrectly()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");

        // 1. SetHyperlink 帶顯示文字
        var cell = sheet.GetCell(0, 0);
        cell.SetHyperlink("https://example.com", "範例網站");
        Assert.Equal("https://example.com", cell.GetHyperlinkUrl());
        Assert.Equal("範例網站", cell.DisplayText);
        Assert.Equal("string", cell.ValueType);

        // 2. SetHyperlink 不帶顯示文字 — 使用 URL 本身
        var cell2 = sheet.GetCell(0, 1);
        cell2.SetHyperlink("https://odfkit.dev");
        Assert.Equal("https://odfkit.dev", cell2.GetHyperlinkUrl());
        Assert.Equal("https://odfkit.dev", cell2.DisplayText);

        // 3. 覆蓋既有超連結 URL，保留顯示文字
        cell.SetHyperlink("https://new.example.com");
        Assert.Equal("https://new.example.com", cell.GetHyperlinkUrl());
        Assert.Equal("範例網站", cell.DisplayText);

        // 4. RemoveHyperlink — 保留顯示文字，移除 text:a
        Assert.True(cell.RemoveHyperlink());
        Assert.False(cell.RemoveHyperlink());
        Assert.Null(cell.GetHyperlinkUrl());
        Assert.Equal("範例網站", cell.DisplayText);

        // 5. GetHyperlinkUrl on plain cell returns null
        var cell3 = sheet.GetCell(0, 2);
        cell3.SetValue("普通文字");
        Assert.Null(cell3.GetHyperlinkUrl());

        // 6. XML 結構驗證
        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();
        Assert.Contains("xlink:href=\"https://odfkit.dev\"", xml);
        Assert.Contains("xlink:type=\"simple\"", xml);
        Assert.Contains("text:a", xml);
    }

    /// <summary>
    /// 驗證儲存格批注 SetAnnotation / FindAnnotation / RemoveAnnotation API。
    /// </summary>
    [Fact]
    public void CellAnnotationApiWorksCorrectly()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");

        // 1. SetAnnotation 帶作者
        var cell = sheet.GetCell(0, 0);
        cell.SetAnnotation("這是批注", "Alice");
        var ann = cell.FindAnnotation();
        Assert.NotNull(ann);
        Assert.Equal("這是批注", ann.Text);
        Assert.Equal("Alice", ann.Author);
        Assert.False(ann.Visible);

        // 2. 覆蓋批注
        cell.SetAnnotation("新批注", visible: true);
        ann = cell.FindAnnotation();
        Assert.Equal("新批注", ann!.Text);
        Assert.True(ann.Visible);

        // 3. RemoveAnnotation
        Assert.True(cell.RemoveAnnotation());
        Assert.False(cell.RemoveAnnotation());
        Assert.Null(cell.FindAnnotation());

        // 4. 無批注的儲存格回傳 null
        var cell2 = sheet.GetCell(0, 1);
        Assert.Null(cell2.FindAnnotation());

        // 5. XML 結構驗證
        cell.SetAnnotation("XML 驗證批注", "Bob");
        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();
        Assert.Contains("office:annotation", xml);
        Assert.Contains("XML 驗證批注", xml);
        Assert.Contains("Bob", xml);
    }

    /// <summary>
    /// 驗證儲存格富文字 SetRichText / GetRichText API。
    /// </summary>
    [Fact]
    public void CellRichTextApiWorksCorrectly()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");

        // 1. SetRichText 包含粗體、斜體、色彩
        var cell = sheet.GetCell(0, 0);
        var rt = new OdfRichText();
        rt.AddRun("普通", bold: false);
        rt.AddRun("粗體", bold: true);
        rt.AddRun("紅色", color: new OdfKit.DOM.OdfColor("#ff0000"));
        cell.SetRichText(rt);
        Assert.Equal("string", cell.ValueType);

        // 2. GetRichText 應回傳 3 個 run
        var read = cell.GetRichText();
        Assert.NotNull(read);
        Assert.Equal(3, read.Runs.Count);
        Assert.Equal("普通", read.Runs[0].Text);
        Assert.False(read.Runs[1].Color.HasValue);
        Assert.True(read.Runs[1].Bold);
        Assert.Equal("#ff0000", read.Runs[2].Color?.Value);

        // 3. 純文字儲存格 GetRichText 回傳 null
        var cell2 = sheet.GetCell(0, 1);
        cell2.SetValue("pure text");
        Assert.Null(cell2.GetRichText());

        // 4. 相同格式的 run 共用同一個樣式名稱（XML 去重）
        var cell3 = sheet.GetCell(0, 2);
        var rt2 = new OdfRichText();
        rt2.AddRun("A", bold: true);
        rt2.AddRun("B", bold: true);
        cell3.SetRichText(rt2);

        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();
        Assert.Contains("text:span", xml);
        Assert.Contains("font-weight=\"bold\"", xml);
        Assert.Contains("#ff0000", xml);
    }

    /// <summary>
    /// 驗證儲存格富文字鏈式 API 會將換行輸出為 ODF 節點並啟用自動換行。
    /// </summary>
    [Fact]
    public void CellRichTextFluentApiWritesLineBreakAndWrapStyle()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");
        var cell = sheet.GetCell(0, 0);

        cell.RichText
            .Clear()
            .Append("第一行", bold: true)
            .LineBreak()
            .Append("第二行", italic: true, color: new OdfKit.DOM.OdfColor("#336699"));

        Assert.Equal("第一行\n第二行", cell.DisplayText);
        Assert.Equal("wrap", doc.StyleEngine.GetStyleProperty(cell.StyleName!, "wrap-option", OdfNamespaces.Fo, "table-cell"));

        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("text:span", xml);
        Assert.Contains("text:line-break", xml);
        Assert.Contains("fo:wrap-option=\"wrap\"", xml);
        Assert.Contains("#336699", xml);
    }

    /// <summary>
    /// 驗證儲存格可直接追加 HTML / Markdown 行內富文字。
    /// </summary>
    [Fact]
    public void CellAppendHtmlAndMarkdownCreatesRichTextRuns()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");
        var cell = sheet.GetCell(0, 0);

        cell.SetValue("開頭 ");
        cell
            .AppendHtml("<b>粗體</b><br><span style=\"font-style:italic; text-decoration:underline; color:blue\">提示</span>")
            .AppendMarkdown(" **標記**");

        OdfRichText richText = Assert.IsType<OdfRichText>(cell.GetRichText());
        Assert.Equal("開頭 粗體\n提示 標記", cell.DisplayText);
        Assert.Contains(richText.Runs, run => run.Text == "開頭");
        OdfRichTextRun boldRun = Assert.Single(richText.Runs, run => run.Text == "粗體");
        OdfRichTextRun noticeRun = Assert.Single(richText.Runs, run => run.Text == "提示");
        OdfRichTextRun markdownRun = Assert.Single(richText.Runs, run => run.Text == "標記");
        Assert.Contains(richText.Runs, run => run.Text == "\n");
        Assert.True(boldRun.Bold);
        Assert.True(noticeRun.Italic);
        Assert.True(noticeRun.Underline);
        Assert.Equal("#0000FF", noticeRun.Color?.Value);
        Assert.True(markdownRun.Bold);

        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("text:span", xml);
        Assert.Contains("text:line-break", xml);
        Assert.Contains("font-weight=\"bold\"", xml);
        Assert.Contains("#0000FF", xml);
    }

    /// <summary>
    /// 驗證列欄群組 GroupRows / Rows.Group / GroupColumns / Columns.Group API。
    /// </summary>
    [Fact]
    public void RowColumnGroupApiWorksCorrectly()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");

        // 填入資料（確保列節點已建立）
        for (int r = 0; r < 5; r++)
            sheet.GetCell(r, 0).SetValue(r + 1.0);

        // 1. Rows.Group — 群組第 1~3 列（collapsed = false）
        sheet.Rows.Group(1, 3);

        // 2. 群組後儲存並驗證 XML
        using var ms1 = new MemoryStream();
        doc.SaveToStream(ms1);
        ms1.Position = 0;
        using (var pkg1 = OdfKit.Core.OdfPackage.Open(ms1, leaveOpen: true))
        using (var cs1 = pkg1.GetEntryStream("content.xml"))
        using (var r1 = new StreamReader(cs1))
        {
            string xml1 = r1.ReadToEnd();
            Assert.Contains("table:table-row-group", xml1);
            Assert.Contains("table:display=\"true\"", xml1);
        }

        // 3. Rows.Group collapsed
        sheet.Rows.Group(4, 4, collapsed: true);
        using var ms2 = new MemoryStream();
        doc.SaveToStream(ms2);
        ms2.Position = 0;
        using (var pkg2 = OdfKit.Core.OdfPackage.Open(ms2, leaveOpen: true))
        using (var cs2 = pkg2.GetEntryStream("content.xml"))
        using (var r2 = new StreamReader(cs2))
        {
            string xml2 = r2.ReadToEnd();
            Assert.Contains("table:display=\"false\"", xml2);
        }

        // 4. Rows.Ungroup — 移除群組後 table-row-group 應消失
        sheet.Rows.Ungroup(1, 3);
        sheet.Rows.Ungroup(4, 4);
        using var ms3 = new MemoryStream();
        doc.SaveToStream(ms3);
        ms3.Position = 0;
        using (var pkg3 = OdfKit.Core.OdfPackage.Open(ms3, leaveOpen: true))
        using (var cs3 = pkg3.GetEntryStream("content.xml"))
        using (var r3 = new StreamReader(cs3))
        {
            string xml3 = r3.ReadToEnd();
            Assert.DoesNotContain("table:table-row-group", xml3);
        }

        // 5. Columns.Group
        for (int c = 0; c < 4; c++)
            sheet.GetCell(0, c).SetValue(c + 1.0);
        sheet.Columns.Group(1, 2);
        using var ms4 = new MemoryStream();
        doc.SaveToStream(ms4);
        ms4.Position = 0;
        using (var pkg4 = OdfKit.Core.OdfPackage.Open(ms4, leaveOpen: true))
        using (var cs4 = pkg4.GetEntryStream("content.xml"))
        using (var r4 = new StreamReader(cs4))
        {
            string xml4 = r4.ReadToEnd();
            Assert.Contains("table:table-column-group", xml4);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Rows.Group(3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Columns.Group(-1, 0));
    }

    /// <summary>
    /// 驗證 <see cref="OdfCell.SetBorders"/> 寫入的框線樣式可儲存並於重新載入後正確讀回。
    /// </summary>
    [Fact]
    public void SetBorders_WritesAndRoundTripsBorderStyles()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        OdfCell cell = sheet.Cells["A1"];
        cell.CellValue = "邊框測試";

        var topBorder = new OdfBorder(OdfBorder.BorderStyle.Solid, OdfLength.FromPoints(1), System.Drawing.Color.Black);
        var bottomBorder = new OdfBorder(OdfBorder.BorderStyle.Double, OdfLength.FromPoints(2), System.Drawing.Color.Red);

        // 僅設定上、下框線，左、右框線保留 null（不寫入該屬性）。
        cell.SetBorders(top: topBorder, bottom: bottomBorder, left: null, right: null);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("fo:border-top=\"1pt solid #000000\"", xml);
        Assert.Contains("fo:border-bottom=\"2pt double #FF0000\"", xml);
        Assert.DoesNotContain("fo:border-left", xml);
        Assert.DoesNotContain("fo:border-right", xml);

        stream.Position = 0;
        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfCell loadedCell = loaded.Worksheets["Sheet1"].Cells["A1"];
        Assert.Equal("邊框測試", loadedCell.CellValue);
    }

    /// <summary>
    /// 驗證範圍框線與對齊 facade 會依外框、內框與所有儲存格分別套用樣式。
    /// </summary>
    [Fact]
    public void RangeBordersAndAlignmentApplyToBoundaryAndInteriorCells()
    {
        using var document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        var outer = new OdfBorder(OdfBorder.BorderStyle.Solid, OdfLength.FromPoints(1), System.Drawing.Color.Black);
        var inner = new OdfBorder(OdfBorder.BorderStyle.Dotted, OdfLength.FromPoints(0.5), System.Drawing.Color.Blue);

        OdfCellRangeSelection range = sheet.Ranges["A1:C3"];
        range.Borders.SetGrid(outer, inner);
        range.HorizontalAlignment = "center";

        Assert.Equal("1pt solid #000000", GetCellStyle(sheet.Cells["A1"], "border-top"));
        Assert.Equal("1pt solid #000000", GetCellStyle(sheet.Cells["A1"], "border-left"));
        Assert.Equal("0.5pt dotted #0000FF", GetCellStyle(sheet.Cells["A1"], "border-bottom"));
        Assert.Equal("0.5pt dotted #0000FF", GetCellStyle(sheet.Cells["A1"], "border-right"));
        Assert.Equal("0.5pt dotted #0000FF", GetCellStyle(sheet.Cells["B2"], "border-top"));
        Assert.Equal("0.5pt dotted #0000FF", GetCellStyle(sheet.Cells["B2"], "border-bottom"));
        Assert.Equal("1pt solid #000000", GetCellStyle(sheet.Cells["C3"], "border-bottom"));
        Assert.Equal("1pt solid #000000", GetCellStyle(sheet.Cells["C3"], "border-right"));
        Assert.Equal("center", document.StyleEngine.GetStyleProperty(sheet.Cells["B2"].StyleName!, "text-align", OdfNamespaces.Fo, "table-cell"));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("fo:border-top=\"1pt solid #000000\"", xml);
        Assert.Contains("fo:border-bottom=\"0.5pt dotted #0000FF\"", xml);
        Assert.Contains("fo:text-align=\"center\"", xml);
    }

    private static string? GetCellStyle(OdfCell cell, string propertyName) =>
        cell.Document.StyleEngine.GetStyleProperty(cell.StyleName!, propertyName, OdfNamespaces.Fo, "table-cell");
}

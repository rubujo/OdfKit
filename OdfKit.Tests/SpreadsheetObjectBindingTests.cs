using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.Core;
using OdfKit.Compliance;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定試算表物件繫結高階 API 的實務情境。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Scenario)]
public sealed class SpreadsheetObjectBindingTests
{
    [Fact]
    public void WriteObjectsWritesHeadersValuesAndCreatesTable()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        var rows = new[]
        {
            new SalesRow { Customer = "Alpha", Amount = 125.5m, Closed = true },
            new SalesRow { Customer = "Beta", Amount = 88m, Closed = false }
        };

        OdfObjectBindingReport report = sheet.WriteObjects(
            new OdfCellAddress(0, 0, "Data"),
            rows,
            new OdfObjectBindingOptions { CreateTableName = "Sales" });

        Assert.Equal(2, report.RowCount);
        Assert.Equal(3, report.ColumnCount);
        Assert.Contains("Customer", report.ColumnNames);
        Assert.Equal("Customer", sheet.GetCell(0, 0).DisplayText);
        Assert.Equal("Alpha", sheet.GetCell(1, 0).DisplayText);
        Assert.Equal("125.5", sheet.GetCell(1, 1).DisplayText);

        OdfSpreadsheetTable? table = document.FindTable("Sales");
        Assert.NotNull(table);
        Assert.Equal(1, table.GetColumnIndex("Amount"));
    }

    [Fact]
    public void ReadObjectsUsesDisplayNameAndConvertsCommonTypes()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        Guid id = Guid.NewGuid();
        DateTime date = new(2026, 7, 8, 9, 30, 0, DateTimeKind.Utc);
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer Name", "Amount", "Closed", "State", "Id", "ClosedAt"],
                ["Alpha", 125.5m, true, "Won", id.ToString("D"), date]
            });

        OdfObjectBindingReport report = new();
        var rows = sheet.ReadObjects<DisplaySalesRow>(
            new OdfCellRange(0, 0, 1, 5, "Data"),
            new OdfObjectReadOptions { Report = report });

        DisplaySalesRow row = Assert.Single(rows);
        Assert.Equal("Alpha", row.Customer);
        Assert.Equal(125.5m, row.Amount);
        Assert.True(row.Closed);
        Assert.Equal(SalesState.Won, row.State);
        Assert.Equal(id, row.Id);
        Assert.Equal(date, row.ClosedAt);
        Assert.Equal(1, report.RowCount);
        Assert.Equal(6, report.ColumnCount);
    }

    [Fact]
    public void ReadObjectsAppliesDefaultValuesForBlankCells()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed"],
                ["Alpha", null, null]
            });

        var map = new OdfObjectColumnMap();
        map.Map(nameof(SalesRow.Amount)).DefaultValueFactory = () => 42m;
        map.Map(nameof(SalesRow.Closed)).DefaultValue = true;

        var rows = sheet.ReadObjects<SalesRow>(
            new OdfCellRange(0, 0, 1, 2, "Data"),
            new OdfObjectReadOptions { ColumnMap = map });

        SalesRow row = Assert.Single(rows);
        Assert.Equal(42m, row.Amount);
        Assert.True(row.Closed);
    }

    [Fact]
    public void AppendObjectsAndDocumentWrappersRoundTripObjects()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.AddSheet("Data");
        document.WriteObjects(
            "Data",
            new OdfCellAddress(0, 0, "Data"),
            new[] { new SalesRow { Customer = "Alpha", Amount = 1m, Closed = true } });

        OdfObjectBindingReport appendReport = document.AppendObjects(
            "Data",
            new[] { new SalesRow { Customer = "Beta", Amount = 2m, Closed = false } },
            new OdfObjectBindingOptions { IncludeHeader = false });

        Assert.Equal(1, appendReport.RowCount);
        var rows = document.ReadObjects<SalesRow>("Data", new OdfCellRange(0, 0, 2, 2, "Data"));

        Assert.Equal(2, rows.Count);
        Assert.Equal("Beta", rows[1].Customer);
        Assert.Equal(2m, rows[1].Amount);
        Assert.False(rows[1].Closed);
    }

    [Fact]
    public void ReadObjectsReportsMissingColumnsWhenRequested()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer"],
                ["Alpha"]
            });

        OdfObjectBindingReport report = new();
        var rows = sheet.ReadObjects<SalesRow>(
            new OdfCellRange(0, 0, 1, 0, "Data"),
            new OdfObjectReadOptions
            {
                MissingColumnPolicy = OdfObjectMissingColumnPolicy.Warn,
                Report = report
            });

        SalesRow row = Assert.Single(rows);
        Assert.Equal("Alpha", row.Customer);
        Assert.Contains("Amount", report.SkippedColumns);
        Assert.Contains(report.Warnings, warning => warning.Contains("Amount", StringComparison.Ordinal));
    }

    [Fact]
    public void SpreadsheetTableAppliesFilterAndSortByColumnName()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.WriteObjects(
            new OdfCellAddress(0, 0, "Data"),
            new[] { new SalesRow { Customer = "Alpha", Amount = 1m, Closed = true } },
            new OdfObjectBindingOptions { CreateTableName = "Sales" });

        OdfSpreadsheetTable table = document.FindTable("Sales")!;
        table.ApplyFilter("Customer", "=", "Alpha");
        table.ApplySort("Amount", ascending: false);

        string contentXml = SaveAndReadContentXml(document);
        Assert.Contains("table:filter-condition", contentXml);
        Assert.Contains("table:field-number=\"0\"", contentXml);
        Assert.Contains("table:sort-by", contentXml);
        Assert.Contains("table:field-number=\"1\"", contentXml);
        Assert.Contains("table:order=\"descending\"", contentXml);
    }

    [Fact]
    public void SpreadsheetTableMissingColumnThrowsColumnNotFoundMessage()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.WriteObjects(
            new OdfCellAddress(0, 0, "Data"),
            new[] { new SalesRow { Customer = "Alpha", Amount = 1m, Closed = true } },
            new OdfObjectBindingOptions { CreateTableName = "Sales" });

        OdfSpreadsheetTable table = document.FindTable("Sales")!;
        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => table.ApplySort("Missing"));

        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadObjectsConversionPolicyThrowThrowsLocalizedFormatException()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed"],
                ["Alpha", "not-a-number", true]
            });

        FormatException exception = Assert.Throws<FormatException>(() =>
            sheet.ReadObjects<SalesRow>(new OdfCellRange(0, 0, 1, 2, "Data")));

        Assert.Contains("Amount", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not-a-number", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadObjectsConversionPolicyWarnAndUseDefaultRecordsDiagnostic()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed"],
                ["Alpha", "not-a-number", true]
            });

        OdfObjectBindingReport report = new();
        var rows = sheet.ReadObjects<SalesRow>(
            new OdfCellRange(0, 0, 1, 2, "Data"),
            new OdfObjectReadOptions
            {
                ConversionErrorPolicy = OdfObjectConversionErrorPolicy.WarnAndUseDefault,
                Report = report
            });

        SalesRow row = Assert.Single(rows);
        Assert.Equal("Alpha", row.Customer);
        Assert.Equal(0m, row.Amount);
        OdfObjectBindingDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(OdfIssueSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Amount", diagnostic.PropertyName);
        Assert.Equal("not-a-number", diagnostic.RawValue);
    }

    [Fact]
    public void ReadObjectsConversionPolicyWarnAndSkipRowSkipsBadRows()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed"],
                ["Bad", "not-a-number", true],
                ["Good", 42m, false]
            });

        OdfObjectBindingReport report = new();
        var rows = sheet.ReadObjects<SalesRow>(
            new OdfCellRange(0, 0, 2, 2, "Data"),
            new OdfObjectReadOptions
            {
                ConversionErrorPolicy = OdfObjectConversionErrorPolicy.WarnAndSkipRow,
                Report = report
            });

        SalesRow row = Assert.Single(rows);
        Assert.Equal("Good", row.Customer);
        Assert.Single(report.Diagnostics);
    }

    [Fact]
    public void ObjectColumnMapControlsOrderHeaderIgnoreAliasAndFormatting()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        var map = new OdfObjectColumnMap();
        map.Map(nameof(SalesRow.Amount), "Total", order: 0).Format = new OdfObjectColumnFormat
        {
            StyleName = "MoneyCell",
            HeaderStyleName = "HeaderCell",
            NumberFormat = "N2",
            Width = 3.Cm()
        };
        map.Map(nameof(SalesRow.Customer), "Client", order: 1).Aliases.Add("Customer Name");
        map.Map(nameof(SalesRow.Closed), header: null, order: null, ignore: true);

        OdfObjectBindingReport report = sheet.WriteObjects(
            new OdfCellAddress(0, 0, "Data"),
            new[] { new SalesRow { Customer = "Alpha", Amount = 12.5m, Closed = true } },
            new OdfObjectBindingOptions { ColumnMap = map });

        Assert.Equal(["Total", "Client"], report.ColumnNames);
        Assert.Equal("Total", sheet.GetCell(0, 0).DisplayText);
        Assert.Equal("Client", sheet.GetCell(0, 1).DisplayText);
        Assert.Equal("HeaderCell", sheet.GetCell(0, 0).StyleName);
        Assert.Equal("MoneyCell", sheet.GetCell(1, 0).StyleName);
        Assert.False(string.IsNullOrEmpty(sheet.GetCell(1, 0).Style.NumberFormat));
        Assert.Equal(3d, sheet.GetColumnWidth(0)?.ToCentimeters());
        Assert.Equal(string.Empty, sheet.GetCell(0, 2).DisplayText);

        sheet.SetValues(
            new OdfCellAddress(3, 0, "Data"),
            new object?[][]
            {
                ["Total", "Customer Name"],
                [9m, "Beta"]
            });
        var rows = sheet.ReadObjects<SalesRow>(
            new OdfCellRange(3, 0, 4, 1, "Data"),
            new OdfObjectReadOptions { ColumnMap = map });

        SalesRow row = Assert.Single(rows);
        Assert.Equal("Beta", row.Customer);
        Assert.Equal(9m, row.Amount);
        Assert.False(row.Closed);
    }

    [Fact]
    public void ValidateObjectBindingReportsRequiredDuplicateUnknownAndConversionIssues()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Customer", "Amount", "Extra"],
                ["", "Alias", "oops", "ignored"]
            });
        var map = new OdfObjectColumnMap();
        map.Map(nameof(SalesRow.Customer), "Customer").RequiredValue = true;
        map.Map(nameof(SalesRow.Amount), "Amount").RequiredColumn = true;
        map.Map(nameof(SalesRow.Closed), "Closed").RequiredColumn = true;

        OdfObjectBindingValidationReport report = sheet.ValidateObjectBinding<SalesRow>(
            new OdfCellRange(0, 0, 1, 3, "Data"),
            new OdfObjectReadOptions
            {
                ColumnMap = map,
                UnknownColumnPolicy = OdfObjectUnknownColumnPolicy.Warn,
                DuplicateHeaderPolicy = OdfObjectDuplicateHeaderPolicy.WarnAndUseFirst
            });

        Assert.True(report.HasErrors);
        Assert.True(report.HasWarnings);
        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0001" && issue.PropertyName == nameof(SalesRow.Closed));
        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0003" && issue.PropertyName == nameof(SalesRow.Customer));
        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0002" && issue.PropertyName == nameof(SalesRow.Amount));
        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0004" && issue.PropertyName == "Extra");
        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0005" && issue.PropertyName == "Customer");
    }

    [Fact]
    public void UpdateObjectsUpdatesExistingRowsAndPreservesUnmappedFormulaCells()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed", "Total"],
                ["Alpha", 1m, true, null],
                ["Beta", 2m, false, null]
            });
        sheet.GetCell(1, 3).Formula = "of:=[.B2]*2";
        sheet.GetCell(2, 3).Formula = "of:=[.B3]*2";

        OdfObjectBindingReport report = sheet.UpdateObjects(
            new OdfCellRange(0, 0, 2, 3, "Data"),
            new[]
            {
                new SalesRow { Customer = "Beta", Amount = 20m, Closed = true },
                new SalesRow { Customer = "Gamma", Amount = 30m, Closed = false }
            },
            new OdfObjectUpdateOptions { KeyColumn = nameof(SalesRow.Customer) });

        Assert.Equal(1, report.UpdatedRowCount);
        Assert.Equal(1, report.SkippedRowCount);
        Assert.Equal("Beta", sheet.GetCell(2, 0).DisplayText);
        Assert.Equal("20", sheet.GetCell(2, 1).DisplayText);
        Assert.Equal("of:=[.B3]*2", sheet.GetCell(2, 3).Formula);
        Assert.Equal(string.Empty, sheet.GetCell(3, 0).DisplayText);
    }

    [Fact]
    public void UpsertObjectsInsertsMissingRowsAndResizesMatchingTable()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed", "Total"],
                ["Alpha", 1m, true, null],
                ["Beta", 2m, false, null]
            });
        sheet.GetCell(2, 1).StyleName = "MoneyStyle";
        sheet.GetCell(2, 3).StyleName = "FormulaStyle";
        sheet.GetCell(2, 3).Formula = "of:=[.B3]*2";
        document.CreateTable("Sales", new OdfCellRange(0, 0, 2, 3, "Data"));

        OdfObjectBindingReport report = document.UpsertObjects(
            "Data",
            new OdfCellRange(0, 0, 2, 3, "Data"),
            new[]
            {
                new SalesRow { Customer = "Beta", Amount = 20m, Closed = true },
                new SalesRow { Customer = "Gamma", Amount = 30m, Closed = false }
            },
            new OdfObjectUpdateOptions { KeyColumn = nameof(SalesRow.Customer) });

        Assert.Equal(1, report.UpdatedRowCount);
        Assert.Equal(1, report.InsertedRowCount);
        Assert.Equal("Gamma", sheet.GetCell(3, 0).DisplayText);
        Assert.Equal("30", sheet.GetCell(3, 1).DisplayText);
        Assert.Equal("MoneyStyle", sheet.GetCell(3, 1).StyleName);
        Assert.Equal("FormulaStyle", sheet.GetCell(3, 3).StyleName);
        Assert.Equal("of:=[.B4]*2", sheet.GetCell(3, 3).Formula);
        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0200");
        Assert.Equal(2, report.AffectedRowCount);
        Assert.True(report.HasIssues);

        OdfSpreadsheetTableInfo table = Assert.Single(document.GetTables(), item => item.Name == "Sales");
        Assert.True(OdfCellRange.TryParse(table.TargetRangeAddress, out OdfCellRange resizedRange));
        Assert.Equal(new OdfCellRange(0, 0, 3, 3, "Data"), resizedRange);
    }

    [Fact]
    public void UpsertObjectsCopyAsIsFormulaModeKeepsTemplateFormula()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed", "Total"],
                ["Beta", 2m, false, null]
            });
        sheet.GetCell(1, 3).Formula = "of:=[.B2]*2";

        _ = sheet.UpsertObjects(
            new OdfCellRange(0, 0, 1, 3, "Data"),
            new[] { new SalesRow { Customer = "Gamma", Amount = 30m, Closed = false } },
            new OdfObjectUpdateOptions
            {
                KeyColumn = nameof(SalesRow.Customer),
                FormulaCopyMode = OdfFormulaCopyMode.CopyAsIs
            });

        Assert.Equal("of:=[.B2]*2", sheet.GetCell(2, 3).Formula);
    }

    [Fact]
    public void UpsertObjectsShiftsRelativeFormulaReferencesAndPreservesAbsoluteRows()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed", "Total", "Span"],
                ["Beta", 2m, false, null, null]
            });
        sheet.GetCell(1, 3).Formula = "of:=[.B2]+[.$C$2]";
        sheet.GetCell(1, 4).Formula = "of:=SUM([.B2:.D2])";

        _ = sheet.UpsertObjects(
            new OdfCellRange(0, 0, 1, 4, "Data"),
            new[] { new SalesRow { Customer = "Gamma", Amount = 30m, Closed = false } },
            new OdfObjectUpdateOptions { KeyColumn = nameof(SalesRow.Customer) });

        Assert.Equal("of:=[.B3]+[.$C$2]", sheet.GetCell(2, 3).Formula);
        Assert.Equal("of:=SUM([.B3:.D3])", sheet.GetCell(2, 4).Formula);
    }

    [Fact]
    public void UpsertObjectsShiftsSheetQualifiedTemplateFormulaReferences()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed", "Total"],
                ["Beta", 2m, false, null]
            });
        sheet.GetCell(1, 3).Formula = "of:=[Data.B2]*2";

        _ = sheet.UpsertObjects(
            new OdfCellRange(0, 0, 1, 3, "Data"),
            new[] { new SalesRow { Customer = "Gamma", Amount = 30m, Closed = false } },
            new OdfObjectUpdateOptions { KeyColumn = nameof(SalesRow.Customer) });

        Assert.Equal("of:=[Data.B3]*2", sheet.GetCell(2, 3).Formula);
    }

    [Fact]
    public void UpsertObjectsClearFormulaModeDoesNotCopyTemplateFormula()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed", "Total"],
                ["Beta", 2m, false, null]
            });
        sheet.GetCell(1, 3).Formula = "of:=[.B2]*2";

        _ = sheet.UpsertObjects(
            new OdfCellRange(0, 0, 1, 3, "Data"),
            new[] { new SalesRow { Customer = "Gamma", Amount = 30m, Closed = false } },
            new OdfObjectUpdateOptions
            {
                KeyColumn = nameof(SalesRow.Customer),
                FormulaCopyMode = OdfFormulaCopyMode.Clear
            });

        Assert.Equal(string.Empty, sheet.GetCell(2, 3).Formula);
    }

    [Fact]
    public void UpsertObjectsDuplicateExistingKeysThrowsLocalizedException()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed"],
                ["Beta", 2m, false],
                ["Beta", 3m, true]
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            sheet.UpsertObjects(
                new OdfCellRange(0, 0, 2, 2, "Data"),
                new[] { new SalesRow { Customer = "Beta", Amount = 30m, Closed = false } },
                new OdfObjectUpdateOptions { KeyColumn = nameof(SalesRow.Customer) }));

        Assert.Contains("Beta", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadObjectsDuplicateHeaderPolicyThrowThrowsLocalizedException()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Customer", "Amount"],
                ["Alpha", "Alias", 1m]
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            sheet.ReadObjects<SalesRow>(
                new OdfCellRange(0, 0, 1, 2, "Data"),
                new OdfObjectReadOptions { DuplicateHeaderPolicy = OdfObjectDuplicateHeaderPolicy.Throw }));

        Assert.Contains("Customer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateObjectBindingDuplicateHeaderPolicyThrowReportsError()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Customer", "Amount"],
                ["Alpha", "Alias", 1m]
            });

        OdfObjectBindingValidationReport report = sheet.ValidateObjectBinding<SalesRow>(
            new OdfCellRange(0, 0, 1, 2, "Data"),
            new OdfObjectReadOptions { DuplicateHeaderPolicy = OdfObjectDuplicateHeaderPolicy.Throw });

        Assert.Contains(report.Diagnostics, issue =>
            issue.Code == "ODSOBJ0005" && issue.Severity == OdfIssueSeverity.Error);
    }

    [Fact]
    public void ValidateObjectBindingUsesValidationOptionsEntryPoint()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount"],
                ["Alpha", 1m]
            });
        var map = new OdfObjectColumnMap();
        map.Map(nameof(SalesRow.Closed), "Closed").RequiredColumn = true;

        OdfObjectBindingValidationReport report = sheet.ValidateObjectBinding<SalesRow>(
            new OdfCellRange(0, 0, 1, 1, "Data"),
            new OdfObjectBindingValidationOptions { ColumnMap = map });

        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0001");
    }

    [Fact]
    public void UpsertObjectsMissingKeyWarnAndSkipReportsStableDiagnostic()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed"],
                ["Beta", 2m, false]
            });

        OdfObjectBindingReport report = sheet.UpsertObjects(
            new OdfCellRange(0, 0, 1, 2, "Data"),
            new[] { new SalesRow { Customer = "", Amount = 30m, Closed = false } },
            new OdfObjectUpdateOptions
            {
                KeyColumn = nameof(SalesRow.Customer),
                MissingKeyPolicy = OdfObjectMissingKeyPolicy.WarnAndSkip
            });

        Assert.Equal(1, report.SkippedRowCount);
        Assert.Contains(report.Diagnostics, issue => issue.Code == "ODSOBJ0101");
    }

    [Fact]
    public void DocumentValidateObjectBindingDelegatesToSheet()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[][]
            {
                ["Customer", "Amount", "Closed"],
                ["Alpha", 1m, true]
            });

        OdfObjectBindingValidationReport report = document.ValidateObjectBinding<SalesRow>(
            "Data",
            new OdfCellRange(0, 0, 1, 2, "Data"));

        Assert.False(report.HasErrors);
    }

    private static string SaveAndReadContentXml(SpreadsheetDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry entry = archive.GetEntry("content.xml")!;
        using Stream entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class SalesRow
    {
        public string? Customer { get; set; }

        public decimal Amount { get; set; }

        public bool Closed { get; set; }
    }

    private sealed class DisplaySalesRow
    {
        [DisplayName("Customer Name")]
        public string? Customer { get; set; }

        public decimal Amount { get; set; }

        public bool Closed { get; set; }

        public SalesState State { get; set; }

        public Guid Id { get; set; }

        public DateTime ClosedAt { get; set; }
    }

    private enum SalesState
    {
        Open,
        Won
    }
}

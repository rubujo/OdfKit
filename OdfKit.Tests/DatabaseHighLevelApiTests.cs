using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.Database;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定資料庫文件高階 API 的整合測試。
/// </summary>
public class DatabaseHighLevelApiTests
{
    /// <summary>
    /// 驗證 <see cref="OdfDatabaseDocument.GetForms"/> 可讀回已新增的表單元件。
    /// </summary>
    [Fact]
    public void GetForms_RoundTripsAfterAdd()
    {
        using var database = OdfDatabaseDocument.Create();
        database.AddForm(
            "CustomerForm",
            "forms/CustomerForm",
            "客戶表單",
            "維護客戶資料的主表單。",
            asTemplate: false);
        database.AddForm("SearchForm", "forms/SearchForm", "搜尋表單");

        Assert.Equal(2, database.GetForms().Count);
        OdfDatabaseFormInfo customerForm = database.FindForm("CustomerForm")!;
        Assert.NotNull(customerForm);
        Assert.Equal("forms/CustomerForm", customerForm.Href);
        Assert.Equal("客戶表單", customerForm.Title);
        Assert.Equal("維護客戶資料的主表單。", customerForm.Description);
        Assert.False(customerForm.AsTemplate);

        Assert.True(database.RemoveForm("SearchForm"));
        Assert.Single(database.Forms);
        Assert.Null(database.FindForm("SearchForm"));
    }

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseDocument.GetReports"/> 可讀回已新增的報表元件。
    /// </summary>
    [Fact]
    public void GetReports_RoundTripsAfterAdd()
    {
        using var database = OdfDatabaseDocument.Create();
        database.AddReport(
            "SalesReport",
            "reports/SalesReport",
            "銷售報表",
            "每月銷售摘要。",
            asTemplate: false);
        database.AddReport("DraftReport", "reports/DraftReport");

        Assert.Equal(2, database.GetReports().Count);
        OdfDatabaseReportInfo? report = database.FindReport("SalesReport");
        Assert.NotNull(report);
        Assert.Equal("reports/SalesReport", report!.Href);
        Assert.Equal("銷售報表", report.Title);
        Assert.Equal("每月銷售摘要。", report.Description);
        Assert.False(report.AsTemplate);

        Assert.True(database.RemoveReport("DraftReport"));
        Assert.Single(database.Reports);
    }

    /// <summary>
    /// 驗證報表元件可於儲存後重新載入。
    /// </summary>
    [Fact]
    public void ReportsPersistAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.AddReport("SalesReport", "reports/SalesReport", "銷售報表", "每月銷售摘要。");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        OdfDatabaseReportInfo report = Assert.Single(loaded.GetReports());
        Assert.Equal("SalesReport", report.Name);
        Assert.Equal("reports/SalesReport", report.Href);
        Assert.Equal("銷售報表", report.Title);
        Assert.Equal("每月銷售摘要。", report.Description);
    }

    /// <summary>
    /// 驗證表單元件可於儲存後重新載入。
    /// </summary>
    [Fact]
    public void FormsPersistAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.AddForm("MainForm", "forms/MainForm", "主表單");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        OdfDatabaseFormInfo form = Assert.Single(loaded.GetForms());
        Assert.Equal("MainForm", form.Name);
        Assert.Equal("forms/MainForm", form.Href);
        Assert.Equal("主表單", form.Title);
    }

    /// <summary>
    /// 驗證 <see cref="OdfSchemaColumn"/> 的唯一值、預設值與檢查約束可於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void SchemaColumnConstraints_RoundTripAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        var schema = new OdfDatabaseSchema(database);

        var table = new OdfSchemaTable("Customers");
        table.Columns.Add(new OdfSchemaColumn("Id", "INTEGER", isNullable: false, isAutoIncrement: true));
        table.Columns.Add(new OdfSchemaColumn("Email", "VARCHAR")
        {
            IsUnique = true,
            DefaultValue = "unknown@example.com",
            CheckConstraint = "Email LIKE '%@%'",
        });
        schema.AddTable(table);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        var loadedSchema = new OdfDatabaseSchema(loaded);
        OdfSchemaTable loadedTable = Assert.Single(loadedSchema.Tables);
        OdfSchemaColumn emailColumn = loadedTable.Columns.Single(c => c.Name == "Email");

        Assert.True(emailColumn.IsUnique);
        Assert.Equal("unknown@example.com", emailColumn.DefaultValue);
        Assert.Equal("Email LIKE '%@%'", emailColumn.CheckConstraint);
    }

    /// <summary>
    /// 驗證 <see cref="OdfSchemaIndex"/> 可於儲存／載入後保留索引定義。
    /// </summary>
    [Fact]
    public void SchemaIndexes_RoundTripAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        var schema = new OdfDatabaseSchema(database);

        var table = new OdfSchemaTable("Customers");
        table.Columns.Add(new OdfSchemaColumn("Id", "INTEGER"));
        table.Columns.Add(new OdfSchemaColumn("Email", "VARCHAR"));
        table.Indexes.Add(new OdfSchemaIndex("IX_Customers_Email", isUnique: true, new List<string> { "Email" }));
        schema.AddTable(table);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        var loadedSchema = new OdfDatabaseSchema(loaded);
        OdfSchemaTable loadedTable = Assert.Single(loadedSchema.Tables);
        OdfSchemaIndex index = Assert.Single(loadedTable.Indexes);

        Assert.Equal("IX_Customers_Email", index.Name);
        Assert.True(index.IsUnique);
        Assert.Equal("Email", Assert.Single(index.Columns));
    }
}

using System;
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

    /// <summary>
    /// 驗證表單進階控制項（B-3）可新增並於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void AdvancedFormControls_RoundTripAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        var designer = new OdfDatabaseFormDesigner(database);

        designer.AddRadioButton("Gender", "男", isSelected: true);
        designer.AddComboBox("City", "城市", new[] { "台北", "台中" });
        designer.AddNumericField("Amount", "金額", 123.5);
        designer.AddDateField("BirthDate", "生日", new DateTime(2026, 1, 15));
        designer.AddTimeField("Appointment", "預約時間", new TimeSpan(13, 30, 0));

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        using var contentStream = loaded.Package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("<form:radio", xml);
        Assert.Contains("<form:combobox", xml);
        Assert.Contains("<form:number", xml);
        Assert.Contains("<form:date", xml);
        Assert.Contains("<form:time", xml);
        Assert.Contains("form:current-value=\"123.5\"", xml);
        Assert.Contains("form:current-value=\"2026-01-15\"", xml);
    }

    /// <summary>
    /// 驗證連線登入與驅動程式設定（B-4）可往返。
    /// </summary>
    [Fact]
    public void LoginAndDriverSettings_RoundTripAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.SetLogin(userName: "admin", useSystemUser: false, isPasswordRequired: true, loginTimeout: 30);
        database.SetDriverSettings(showDeleted: false, isFirstRowHeaderLine: true, parameterNameSubstitution: true);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        OdfDatabaseLoginInfo? login = loaded.GetLogin();
        Assert.NotNull(login);
        Assert.Equal("admin", login!.UserName);
        Assert.False(login.UseSystemUser);
        Assert.True(login.IsPasswordRequired);
        Assert.Equal(30, login.LoginTimeout);

        OdfDatabaseDriverSettingsInfo? driver = loaded.GetDriverSettings();
        Assert.NotNull(driver);
        Assert.False(driver!.ShowDeleted);
        Assert.True(driver.IsFirstRowHeaderLine);
        Assert.True(driver.ParameterNameSubstitution);
    }

    /// <summary>
    /// 驗證查詢排序／篩選／欄位／更新表設定（B-6）可往返。
    /// </summary>
    [Fact]
    public void QueryStatementsColumnsAndUpdateTable_RoundTripAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        database.AddQuery("CustomerQuery", "SELECT * FROM Customers");
        database.SetQueryOrderStatement("CustomerQuery", "Name ASC", applyCommand: true);
        database.SetQueryFilterStatement("CustomerQuery", "Age > 18", applyCommand: true);
        database.SetQueryColumns("CustomerQuery", new[] { "Name", "Age" });
        database.SetQueryUpdateTable("CustomerQuery", "Customers");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        OdfDatabaseQueryStatementInfo? order = loaded.GetQueryOrderStatement("CustomerQuery");
        Assert.NotNull(order);
        Assert.Equal("Name ASC", order!.Command);
        Assert.True(order.ApplyCommand);

        OdfDatabaseQueryStatementInfo? filter = loaded.GetQueryFilterStatement("CustomerQuery");
        Assert.NotNull(filter);
        Assert.Equal("Age > 18", filter!.Command);

        Assert.Equal(new[] { "Name", "Age" }, loaded.GetQueryColumns("CustomerQuery"));
        Assert.Equal("Customers", loaded.GetQueryUpdateTable("CustomerQuery"));
    }

    /// <summary>
    /// 驗證表單控制項事件繫結與必填／最大長度設定（B-8）可往返。
    /// </summary>
    [Fact]
    public void ControlEventAndValidationAttributes_RoundTripAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        var designer = new OdfDatabaseFormDesigner(database);
        var textBox = designer.AddTextBox("CustomerName", "客戶名稱");
        designer.SetControlEvent(textBox, "form:approveaction", "Standard.Module1.OnApprove");
        designer.SetControlRequired(textBox, true);
        designer.SetControlMaxLength(textBox, 50);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        using var contentStream = loaded.Package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("<script:event-listener", xml);
        Assert.Contains("script:event-name=\"form:approveaction\"", xml);
        Assert.Contains("script:macro-name=\"Standard.Module1.OnApprove\"", xml);
        Assert.Contains("form:input-required=\"true\"", xml);
        Assert.Contains("form:max-length=\"50\"", xml);
    }

    /// <summary>
    /// 驗證群組框控制項（B-9）可新增並於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void GroupBox_RoundTripsAfterSaveAndLoad()
    {
        using var database = OdfDatabaseDocument.Create();
        var designer = new OdfDatabaseFormDesigner(database);
        designer.AddGroupBox("ContactGroup", "聯絡資訊");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfDatabaseDocument.Load(stream, "database.odb");
        using var contentStream = loaded.Package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("<form:frame", xml);
        Assert.Contains("form:label=\"聯絡資訊\"", xml);
    }
}

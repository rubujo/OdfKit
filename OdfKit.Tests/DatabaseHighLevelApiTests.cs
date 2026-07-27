using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定資料庫文件高階 API 的整合測試。
/// </summary>
public class DatabaseHighLevelApiTests
{
    private static readonly string[] CityChoices = ["台北", "台中"];
    private static readonly string[] CustomerQueryColumns = ["Name", "Age"];
    /// <summary>
    /// 驗證 <see cref="DatabaseDocument.GetForms"/> 可讀回已新增的表單元件。
    /// </summary>
    [Fact]
    public void GetFormsRoundTripsAfterAdd()
    {
        using var database = DatabaseDocument.Create();
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
    /// 驗證 <see cref="DatabaseDocument.GetReports"/> 可讀回已新增的報表元件。
    /// </summary>
    [Fact]
    public void GetReportsRoundTripsAfterAdd()
    {
        using var database = DatabaseDocument.Create();
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
    /// 驗證資料庫摘要可彙總常見檢查資訊。
    /// </summary>
    [Fact]
    public void GetSummaryReturnsPracticalDatabaseCounts()
    {
        using var database = DatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.AddTable("Customers");
        database.AddQuery("Adults", "SELECT * FROM Customers WHERE Age > 18");
        database.AddForm("CustomerForm", "forms/CustomerForm");
        database.AddReport("CustomerReport", "reports/CustomerReport");
        database.AddDataSourceSetting("UseCatalog", OdfDatabaseDataSourceSettingType.Boolean, "true");

        OdfDatabaseSummaryInfo summary = database.GetSummary();

        Assert.Equal("sdbc:embedded:hsqldb", summary.ConnectionHref);
        Assert.Equal(1, summary.TableCount);
        Assert.Equal(1, summary.QueryCount);
        Assert.Equal(1, summary.FormCount);
        Assert.Equal(1, summary.ReportCount);
        Assert.Equal(1, summary.DataSourceSettingCount);
    }

    /// <summary>
    /// 驗證資料庫實務 helper 可描述連線、schema 與參數化查詢。
    /// </summary>
    [Fact]
    public void PracticalDepthHelpersDescribeConnectionSchemaAndParameterizedQuery()
    {
        using var database = DatabaseDocument.Create();
        database
            .ConfigureConnection("sdbc:embedded:hsqldb")
            .AddTableSchema("Customers", ["Id INT", "Name VARCHAR"]);
        database.AddParameterizedQuery(
            "ByName",
            "SELECT * FROM Customers WHERE Name = :name",
            [new OdfDatabaseQueryParameter("name", "string", "Customer name")],
            "依名稱查詢");

        Assert.Equal("sdbc:embedded:hsqldb", database.ConnectionHref);
        Assert.Equal("Id INT, Name VARCHAR", database.FindTable("Customers")?.Command);
        OdfDatabaseQueryInfo? query = database.FindQuery("ByName");
        Assert.NotNull(query);
        Assert.Equal("依名稱查詢", query!.Title);
        Assert.Contains("name:string", query.Description);
    }

    /// <summary>
    /// 驗證報表元件可於儲存後重新載入。
    /// </summary>
    [Fact]
    public void ReportsPersistAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.AddReport("SalesReport", "reports/SalesReport", "銷售報表", "每月銷售摘要。");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
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
        using var database = DatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.AddForm("MainForm", "forms/MainForm", "主表單");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
        OdfDatabaseFormInfo form = Assert.Single(loaded.GetForms());
        Assert.Equal("MainForm", form.Name);
        Assert.Equal("forms/MainForm", form.Href);
        Assert.Equal("主表單", form.Title);
    }

    /// <summary>
    /// 驗證 <see cref="OdfSchemaColumn"/> 的唯一值、預設值與檢查約束可於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void SchemaColumnConstraintsRoundTripAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        var schema = new OdfDatabaseSchema(database);

        var table = new OdfSchemaTable("Customers");
        table.AddColumn(new OdfSchemaColumn("Id", "INTEGER", isNullable: false, isAutoIncrement: true));
        table.AddColumn(new OdfSchemaColumn("Email", "VARCHAR")
        {
            IsUnique = true,
            DefaultValue = "unknown@example.com",
            CheckConstraint = "Email LIKE '%@%'",
        });
        schema.AddTable(table);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
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
    public void SchemaIndexesRoundTripAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        var schema = new OdfDatabaseSchema(database);

        var table = new OdfSchemaTable("Customers");
        table.AddColumn(new OdfSchemaColumn("Id", "INTEGER"));
        table.AddColumn(new OdfSchemaColumn("Email", "VARCHAR"));
        table.AddIndex(new OdfSchemaIndex("IX_Customers_Email", isUnique: true, new List<string> { "Email" }));
        schema.AddTable(table);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
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
    public void AdvancedFormControlsRoundTripAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        var designer = new OdfDatabaseFormDesigner(database);

        designer.AddRadioButton("Gender", "男", isSelected: true);
        designer.AddComboBox("City", "城市", CityChoices);
        designer.AddNumericField("Amount", "金額", 123.5);
        designer.AddDateField("BirthDate", "生日", new DateTime(2026, 1, 15));
        designer.AddTimeField("Appointment", "預約時間", new TimeSpan(13, 30, 0));

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
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
    public void LoginAndDriverSettingsRoundTripAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.SetLogin(userName: "admin", useSystemUser: false, isPasswordRequired: true, loginTimeout: 30);
        database.SetDriverSettings(showDeleted: false, isFirstRowHeaderLine: true, parameterNameSubstitution: true);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
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
    public void QueryStatementsColumnsAndUpdateTableRoundTripAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        database.AddQuery("CustomerQuery", "SELECT * FROM Customers");
        database.SetQueryOrderStatement("CustomerQuery", "Name ASC", applyCommand: true);
        database.SetQueryFilterStatement("CustomerQuery", "Age > 18", applyCommand: true);
        database.SetQueryColumns("CustomerQuery", CustomerQueryColumns);
        database.SetQueryUpdateTable("CustomerQuery", "Customers");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
        OdfDatabaseQueryStatementInfo? order = loaded.FindQueryOrderStatement("CustomerQuery");
        Assert.NotNull(order);
        Assert.Equal("Name ASC", order!.Command);
        Assert.True(order.ApplyCommand);

        OdfDatabaseQueryStatementInfo? filter = loaded.FindQueryFilterStatement("CustomerQuery");
        Assert.NotNull(filter);
        Assert.Equal("Age > 18", filter!.Command);

        Assert.Equal(CustomerQueryColumns, loaded.GetQueryColumns("CustomerQuery"));
        Assert.Equal("Customers", loaded.FindQueryUpdateTable("CustomerQuery"));
    }

    /// <summary>
    /// 驗證表單控制項事件繫結與必填／最大長度設定（B-8）可往返。
    /// </summary>
    [Fact]
    public void ControlEventAndValidationAttributesRoundTripAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        var designer = new OdfDatabaseFormDesigner(database);
        var textBox = designer.AddTextBox("CustomerName", "客戶名稱");
        designer.SetControlEvent(textBox, "form:approveaction", "Standard.Module1.OnApprove");
        designer.SetControlRequired(textBox, true);
        designer.SetControlMaxLength(textBox, 50);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
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
    public void GroupBoxRoundTripsAfterSaveAndLoad()
    {
        using var database = DatabaseDocument.Create();
        var designer = new OdfDatabaseFormDesigner(database);
        designer.AddGroupBox("ContactGroup", "聯絡資訊");

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = DatabaseDocument.Load(stream, "database.odb");
        using var contentStream = loaded.Package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string xml = reader.ReadToEnd();

        Assert.Contains("<form:frame", xml);
        Assert.Contains("form:label=\"聯絡資訊\"", xml);
    }

    /// <summary>
    /// 驗證資料表與查詢支援更新、個別移除及集合清除，並可正確往返。
    /// </summary>
    [Fact]
    public void TableAndQueryCrudUpdateRemoveAndClearRoundTrips()
    {
        using var database = DatabaseDocument.Create();
        database.AddTable("Customers", "customers_v1");
        database.AddTable("Orders", "orders_v1");
        database.AddQuery("ActiveCustomers", "SELECT * FROM customers");
        database.AddQuery("RecentOrders", "SELECT * FROM orders");

        Assert.True(database.UpdateTable("Customers", "customers_v2"));
        Assert.True(database.UpdateQuery("ActiveCustomers", "SELECT * FROM customers WHERE active = TRUE"));
        Assert.False(database.UpdateTable("Missing", "ignored"));
        Assert.False(database.UpdateQuery("Missing", "SELECT 1"));
        Assert.True(database.RemoveTable("Orders"));
        Assert.True(database.RemoveQuery("RecentOrders"));
        Assert.Equal(1, database.ClearQueries());

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using DatabaseDocument loaded = DatabaseDocument.Load(stream, "database.odb");
        OdfDatabaseTableInfo table = Assert.Single(loaded.GetTables());
        Assert.Equal("Customers", table.Name);
        Assert.Equal("customers_v2", table.Command);
        Assert.Empty(loaded.GetQueries());
        Assert.Equal(1, loaded.ClearTables());
        Assert.Empty(loaded.GetTables());
    }

    /// <summary>
    /// 驗證表單、報表與資料來源設定可用描述物件完整更新、往返及清除。
    /// </summary>
    [Fact]
    public void ComponentsAndSettingsUpdateRoundTripAndClear()
    {
        using var database = DatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:firebird");
        database.AddForm("CustomerForm", "Forms/customer", "客戶", "舊表單", true);
        database.AddReport("CustomerReport", "Reports/customer", "客戶報表", "舊報表", false);
        database.AddDataSourceSetting(
            "JavaDriverClass",
            OdfDatabaseDataSourceSettingType.String,
            false,
            "old.Driver");

        Assert.True(database.UpdateForm(
            new OdfDatabaseFormInfo("CustomerForm", "Forms/customer-v2", "客戶維護", null, false)));
        Assert.True(database.UpdateReport(
            new OdfDatabaseReportInfo("CustomerReport", null, "客戶清單", "新版報表", true)));
        Assert.True(database.UpdateDataSourceSetting(
            new OdfDatabaseDataSourceSettingInfo(
                "JavaDriverClass",
                OdfDatabaseDataSourceSettingType.String,
                true,
                ["new.Driver", "fallback.Driver"])));
        Assert.False(database.UpdateForm(
            new OdfDatabaseFormInfo("Missing", null, null, null, null)));

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using DatabaseDocument loaded = DatabaseDocument.Load(stream, "components.odb");
        OdfDatabaseFormInfo form = Assert.Single(loaded.GetForms());
        Assert.Equal("Forms/customer-v2", form.Href);
        Assert.Equal("客戶維護", form.Title);
        Assert.Null(form.Description);
        Assert.False(form.AsTemplate);

        OdfDatabaseReportInfo report = Assert.Single(loaded.GetReports());
        Assert.Null(report.Href);
        Assert.Equal("客戶清單", report.Title);
        Assert.Equal("新版報表", report.Description);
        Assert.True(report.AsTemplate);

        OdfDatabaseDataSourceSettingInfo setting = Assert.Single(loaded.GetDataSourceSettings());
        Assert.True(setting.IsList);
        Assert.Equal(["new.Driver", "fallback.Driver"], setting.Values);

        Assert.Equal(1, loaded.ClearForms());
        Assert.Equal(1, loaded.ClearReports());
        Assert.Equal(1, loaded.ClearDataSourceSettings());
        Assert.Empty(loaded.GetForms());
        Assert.Empty(loaded.GetReports());
        Assert.Empty(loaded.GetDataSourceSettings());
    }

    /// <summary>
    /// 驗證無效的設定值會在修改文件前被拒絕，避免留下部分更新。
    /// </summary>
    [Fact]
    public void UpdateDataSourceSettingNullValueDoesNotPartiallyUpdate()
    {
        using var database = DatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:firebird");
        database.AddDataSourceSetting(
            "JavaDriverClass",
            OdfDatabaseDataSourceSettingType.String,
            false,
            "old.Driver");

        Assert.Throws<ArgumentException>(() => database.UpdateDataSourceSetting(
            new OdfDatabaseDataSourceSettingInfo(
                "JavaDriverClass",
                OdfDatabaseDataSourceSettingType.Boolean,
                true,
                ["new.Driver", null!])));

        OdfDatabaseDataSourceSettingInfo setting = Assert.Single(database.GetDataSourceSettings());
        Assert.Equal(OdfDatabaseDataSourceSettingType.String, setting.Type);
        Assert.False(setting.IsList);
        Assert.Equal(["old.Driver"], setting.Values);
    }

    /// <summary>
    /// 驗證資料表與查詢可由不可變快照完整更新，並保留查詢子內容。
    /// </summary>
    [Fact]
    public void TableAndQuerySnapshotsApplyDesiredStateAndRoundTrip()
    {
        using var database = DatabaseDocument.Create();
        database.AddTable("Customers", "customers_v1");
        database.AddQuery(
            "ActiveCustomers",
            "SELECT * FROM customers",
            "舊標題",
            "舊描述",
            true);
        database.SetQueryOrderStatement("ActiveCustomers", "name ASC");

        Assert.True(database.UpdateTable(
            new OdfDatabaseTableInfo("Customers", "customers_v2")));
        Assert.True(database.UpdateQuery(
            new OdfDatabaseQueryInfo(
                "ActiveCustomers",
                "SELECT * FROM customers WHERE active = TRUE",
                "啟用客戶",
                null,
                false)));

        OdfDatabaseQueryInfo query = Assert.Single(database.GetQueries());
        Assert.Equal("啟用客戶", query.Title);
        Assert.Null(query.Description);
        Assert.False(query.EscapeProcessing);
        Assert.Equal(
            "name ASC",
            database.FindQueryOrderStatement("ActiveCustomers")?.Command);

        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        using DatabaseDocument loaded = DatabaseDocument.Load(stream, "snapshot.odb");
        Assert.Equal("customers_v2", Assert.Single(loaded.GetTables()).Command);
        OdfDatabaseQueryInfo loadedQuery = Assert.Single(loaded.GetQueries());
        Assert.Equal("啟用客戶", loadedQuery.Title);
        Assert.Null(loadedQuery.Description);
        Assert.False(loadedQuery.EscapeProcessing);
        Assert.Equal(
            "name ASC",
            loaded.FindQueryOrderStatement("ActiveCustomers")?.Command);
    }
}

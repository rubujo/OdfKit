using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODB 資料庫 Schema 與表單設計器 API 的單元與整合測試。
/// </summary>
public class DatabaseSchemaAndFormTests
{
    private static readonly OdfLength Cm1 = OdfLength.FromCentimeters(1);
    private static readonly OdfLength Cm2 = OdfLength.FromCentimeters(2);
    private static readonly OdfLength Cm4 = OdfLength.FromCentimeters(4);
    private static readonly OdfLength Cm08 = OdfLength.FromCentimeters(0.8);

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseSchema"/> 對資料表、欄位型別、主鍵與外鍵的 Round-trip 儲存與重新載入。
    /// </summary>
    [Fact]
    public void Schema_AddTableAndKeys_RoundTripsSuccessfully()
    {
        using var database = OdfDatabaseDocument.Create();
        var schema = database.Schema;

        // 1. 建立 Customers 表格
        var customersTable = new OdfSchemaTable("Customers");
        customersTable.Columns.Add(new OdfSchemaColumn("Id", "INTEGER", isNullable: false, isAutoIncrement: true));
        customersTable.Columns.Add(new OdfSchemaColumn("Name", "VARCHAR", isNullable: true));
        customersTable.PrimaryKey = new OdfSchemaPrimaryKey("PK_Customers", ["Id"]);
        schema.AddTable(customersTable);

        // 2. 建立 Orders 表格，並設定外鍵關聯到 Customers
        var ordersTable = new OdfSchemaTable("Orders");
        ordersTable.Columns.Add(new OdfSchemaColumn("OrderId", "INTEGER", isNullable: false));
        ordersTable.Columns.Add(new OdfSchemaColumn("CustomerId", "INTEGER", isNullable: false));
        ordersTable.Columns.Add(new OdfSchemaColumn("Amount", "DECIMAL", isNullable: true));
        ordersTable.PrimaryKey = new OdfSchemaPrimaryKey("PK_Orders", ["OrderId"]);

        var fkMapping = new OdfSchemaKeyMapping("CustomerId", "Id");
        ordersTable.ForeignKeys.Add(new OdfSchemaForeignKey(
            "FK_Orders_Customers",
            "Customers",
            [fkMapping],
            updateRule: "cascade",
            deleteRule: "restrict"));
        schema.AddTable(ordersTable);

        // 3. 儲存至 Stream
        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;

        // 4. 重新載入並驗證
        using var loadedDb = OdfDatabaseDocument.Load(stream, "test_db.odb");
        var loadedSchema = loadedDb.Schema;

        Assert.Equal(2, loadedSchema.Tables.Count);

        var loadedCustomers = loadedSchema.Tables.FirstOrDefault(t => t.Name == "Customers");
        Assert.NotNull(loadedCustomers);
        Assert.Equal(2, loadedCustomers!.Columns.Count);
        Assert.Equal("Id", loadedCustomers.Columns[0].Name);
        Assert.Equal("INTEGER", loadedCustomers.Columns[0].TypeName);
        Assert.False(loadedCustomers.Columns[0].IsNullable);
        Assert.True(loadedCustomers.Columns[0].IsAutoIncrement);

        Assert.NotNull(loadedCustomers.PrimaryKey);
        Assert.Equal("PK_Customers", loadedCustomers.PrimaryKey!.Name);
        Assert.Single(loadedCustomers.PrimaryKey.Columns);
        Assert.Equal("Id", loadedCustomers.PrimaryKey.Columns[0]);

        var loadedOrders = loadedSchema.Tables.FirstOrDefault(t => t.Name == "Orders");
        Assert.NotNull(loadedOrders);
        Assert.Equal(3, loadedOrders!.Columns.Count);
        Assert.NotNull(loadedOrders.PrimaryKey);
        Assert.Equal("PK_Orders", loadedOrders.PrimaryKey!.Name);

        var loadedFk = Assert.Single(loadedOrders.ForeignKeys);
        Assert.Equal("FK_Orders_Customers", loadedFk.Name);
        Assert.Equal("Customers", loadedFk.ReferencedTable);
        Assert.Equal("cascade", loadedFk.UpdateRule);
        Assert.Equal("restrict", loadedFk.DeleteRule);

        var map = Assert.Single(loadedFk.KeyColumns);
        Assert.Equal("CustomerId", map.Column);
        Assert.Equal("Id", map.RelatedColumn);
    }

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseFormDesigner"/> 能在子文件中正確新增文字框、核取方塊、下拉式選單、按鈕與標籤。
    /// </summary>
    [Fact]
    public void FormDesigner_AddControls_XmlContainsExpectedElements()
    {
        using var formDoc = TextDocument.Create();
        var designer = new OdfDatabaseFormDesigner(formDoc);

        // 新增文字框
        designer.AddTextBox("txtUserName", "用戶名", "預設值", Cm1, Cm1, Cm4, Cm08);
        // 新增核取方塊
        designer.AddCheckBox("chkAgreed", "同意服務條款", isChecked: true, Cm1, Cm2, Cm4, Cm08);
        // 新增下拉選單
        designer.AddListBox("lstCountry", "國家", ["台灣", "日本", "美國"], Cm1, Cm1, Cm4, Cm2);
        // 新增按鈕
        designer.AddButton("btnSubmit", "提交", "submit_action", Cm1, Cm1, Cm2, Cm08);
        // 新增標籤
        designer.AddLabel("lblInfo", "提示訊息", Cm1, Cm2, Cm4, Cm08);

        using var stream = new MemoryStream();
        formDoc.SaveToStream(stream);
        stream.Position = 0;

        using var pkg = OdfPackage.Open(stream, leaveOpen: true);
        using var entryStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(entryStream);
        string xml = reader.ReadToEnd();

        // 驗證表單控制項 XML 結構
        Assert.Contains("form:text", xml);
        Assert.Contains("form:name=\"txtUserName\"", xml);
        Assert.Contains("form:string-value=\"預設值\"", xml);

        Assert.Contains("form:checkbox", xml);
        Assert.Contains("form:name=\"chkAgreed\"", xml);
        Assert.Contains("form:current-state=\"checked\"", xml);

        Assert.Contains("form:listbox", xml);
        Assert.Contains("form:name=\"lstCountry\"", xml);
        Assert.Contains("form:option form:value=\"台灣\"", xml);
        Assert.Contains("form:option form:value=\"日本\"", xml);
        Assert.Contains("form:option form:value=\"美國\"", xml);

        Assert.Contains("form:button", xml);
        Assert.Contains("form:name=\"btnSubmit\"", xml);
        Assert.Contains("form:value=\"submit_action\"", xml);

        Assert.Contains("form:fixed-text", xml);
        Assert.Contains("form:name=\"lblInfo\"", xml);
        Assert.Contains("form:label=\"提示訊息\"", xml);

        // 驗證畫面上繪圖控制項對應之 svg 屬性
        Assert.Contains("draw:control", xml);
        Assert.Contains("svg:x=\"1cm\"", xml);
        Assert.Contains("svg:y=\"1cm\"", xml);
        Assert.Contains("svg:width=\"4cm\"", xml);
        Assert.Contains("svg:height=\"0.8cm\"", xml);
    }

    /// <summary>
    /// 驗證 <see cref="OdfParagraph.AddDatabaseDisplayField"/>／<see cref="OdfParagraph.AddDatabaseNextField"/>
    /// 產生合法的 <c>text:database-display</c>／<c>text:database-next</c> 結構（官方 ODF schema 元素）。
    /// </summary>
    [Fact]
    public void Paragraph_DatabaseFields_ProduceValidSchemaElements()
    {
        using var reportDoc = TextDocument.Create();

        var header = reportDoc.AddParagraph("客戶：");
        header.AddDatabaseDisplayField("Customers", "CustomerName", tableType: "table", databaseName: "SalesDb");

        var detail = reportDoc.AddParagraph("金額：");
        detail.AddDatabaseDisplayField("Orders", "Amount");
        detail.AddDatabaseNextField("Orders", condition: "true()");

        var contentXml = SaveAndGetContentXml(reportDoc);

        Assert.Contains("text:database-display", contentXml);
        Assert.Contains("text:table-name=\"Customers\"", contentXml);
        Assert.Contains("text:column-name=\"CustomerName\"", contentXml);
        Assert.Contains("text:table-type=\"table\"", contentXml);
        Assert.Contains("text:database-name=\"SalesDb\"", contentXml);

        Assert.Contains("text:database-next", contentXml);
        Assert.Contains("text:table-name=\"Orders\"", contentXml);
        Assert.Contains("text:condition=\"true()\"", contentXml);
    }

    /// <summary>
    /// 驗證報表內容應建立為獨立 <see cref="TextDocument"/>（使用真實 ODF 欄位元素），
    /// 再透過 <see cref="OdfDatabaseDocument.AddReport"/> 的 <c>xlink:href</c> 參照機制連結至 .odb 套件，
    /// 取代先前基於虛構 <c>report:1.0</c> 命名空間的 <c>DefineReportStructure</c>。
    /// </summary>
    [Fact]
    public void Database_ReportLinkedViaHref_ToTextDocumentBasedReport()
    {
        using var reportDoc = TextDocument.Create();
        var p = reportDoc.AddParagraph("銷售人員：");
        p.AddDatabaseDisplayField("Sales", "SalesPersonName", tableType: "table");

        using var database = OdfDatabaseDocument.Create();
        database.AddReport("SalesReport", href: "Reports/SalesReport/", title: "銷售報表");

        OdfDatabaseReportInfo? report = database.FindReport("SalesReport");
        Assert.NotNull(report);
        Assert.Equal("Reports/SalesReport/", report!.Href);

        var reportContentXml = SaveAndGetContentXml(reportDoc);
        Assert.Contains("text:database-display", reportContentXml);
        Assert.Contains("text:table-name=\"Sales\"", reportContentXml);
    }

    private static string SaveAndGetContentXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var pkg = OdfPackage.Open(stream, leaveOpen: true);
        using var entryStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(entryStream);
        return reader.ReadToEnd();
    }
}

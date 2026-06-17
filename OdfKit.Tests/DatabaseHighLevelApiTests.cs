using System.IO;
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
}

using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Z-2 自訂文件屬性強型別 API 的整合測試。
/// </summary>
public class CustomPropertiesTests
{
    /// <summary>
    /// 驗證 SetCustomProperty(string, string) 能寫入並以 GetCustomProperty 讀回。
    /// </summary>
    [Fact]
    public void SetCustomProperty_String_CanBeReadBack()
    {
        using var doc = TextDocument.Create();
        doc.SetCustomProperty("Author", "Alice");
        Assert.Equal("Alice", doc.GetCustomProperty("Author") as string);
    }

    /// <summary>
    /// 驗證 SetCustomProperty(string, int) 能以整數讀回。
    /// </summary>
    [Fact]
    public void SetCustomProperty_Int_CanBeReadBack()
    {
        using var doc = TextDocument.Create();
        doc.SetCustomProperty("Version", 42);
        var val = doc.GetCustomProperty("Version");
        Assert.NotNull(val);
        Assert.Equal(42.0, Convert.ToDouble(val));
    }

    /// <summary>
    /// 驗證 SetCustomProperty(string, double) 能以 double 讀回。
    /// </summary>
    [Fact]
    public void SetCustomProperty_Double_CanBeReadBack()
    {
        using var doc = TextDocument.Create();
        doc.SetCustomProperty("Score", 3.14);
        var val = doc.GetCustomProperty("Score");
        Assert.NotNull(val);
        double d = Convert.ToDouble(val);
        Assert.InRange(d, 3.13, 3.15);
    }

    /// <summary>
    /// 驗證 SetCustomProperty(string, bool) 能以 bool 讀回。
    /// </summary>
    [Fact]
    public void SetCustomProperty_Bool_CanBeReadBack()
    {
        using var doc = TextDocument.Create();
        doc.SetCustomProperty("IsActive", true);
        var val = doc.GetCustomProperty("IsActive");
        Assert.Equal(true, val);
    }

    /// <summary>
    /// 驗證 SetCustomProperty(string, DateTime) 能以 DateTime 讀回（精度至秒）。
    /// </summary>
    [Fact]
    public void SetCustomProperty_DateTime_CanBeReadBack()
    {
        using var doc = TextDocument.Create();
        var dt = new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc);
        doc.SetCustomProperty("CreatedAt", dt);
        var val = doc.GetCustomProperty("CreatedAt");
        Assert.NotNull(val);
        var result = Convert.ToDateTime(val);
        Assert.Equal(2026, result.Year);
        Assert.Equal(6, result.Month);
        Assert.Equal(16, result.Day);
    }

    /// <summary>
    /// 驗證 GetCustomProperty&lt;T&gt; 泛型版本能正確轉型字串屬性。
    /// </summary>
    [Fact]
    public void GetCustomProperty_Generic_ReturnsTypedValue()
    {
        using var doc = TextDocument.Create();
        doc.SetCustomProperty("Department", "工程部");
        string? val = doc.GetCustomProperty<string>("Department");
        Assert.Equal("工程部", val);
    }

    /// <summary>
    /// 驗證 GetCustomProperty&lt;T&gt; 對不存在的屬性回傳預設值。
    /// </summary>
    [Fact]
    public void GetCustomProperty_Generic_NonExistent_ReturnsDefault()
    {
        using var doc = TextDocument.Create();
        string? val = doc.GetCustomProperty<string>("NonExistent");
        Assert.Null(val);
    }

    /// <summary>
    /// 驗證 GetAllCustomProperties 回傳所有已設定的屬性。
    /// </summary>
    [Fact]
    public void GetAllCustomProperties_ReturnsAllSetProperties()
    {
        using var doc = TextDocument.Create();
        doc.SetCustomProperty("Name", "測試");
        doc.SetCustomProperty("Count", 5);
        doc.SetCustomProperty("Flag", false);

        IReadOnlyDictionary<string, object?> all = doc.GetAllCustomProperties();

        Assert.True(all.Count >= 3);
        Assert.True(all.ContainsKey("Name"));
        Assert.True(all.ContainsKey("Count"));
        Assert.True(all.ContainsKey("Flag"));
    }

    /// <summary>
    /// 驗證覆寫同名屬性時只保留最新值。
    /// </summary>
    [Fact]
    public void SetCustomProperty_OverwriteSameName_KeepsLatestValue()
    {
        using var doc = TextDocument.Create();
        doc.SetCustomProperty("Title", "舊標題");
        doc.SetCustomProperty("Title", "新標題");
        Assert.Equal("新標題", doc.GetCustomProperty("Title") as string);

        var all = doc.GetAllCustomProperties();
        int count = 0;
        foreach (var kvp in all)
            if (kvp.Key == "Title")
                count++;
        Assert.Equal(1, count);
    }

    /// <summary>
    /// 驗證屬性存檔後仍可讀回（Round-trip 測試）。
    /// </summary>
    [Fact]
    public void SetCustomProperty_RoundTrip_ValuesPersistedAfterSaveLoad()
    {
        byte[] bytes;
        using (var doc = TextDocument.Create())
        {
            doc.SetCustomProperty("Author", "Bob");
            doc.SetCustomProperty("Pages", 100);
            bytes = doc.SaveToBytes();
        }

        using var ms2 = new System.IO.MemoryStream(bytes);
        using var loaded = (TextDocument)OdfDocumentFactory.LoadDocument(ms2);
        Assert.Equal("Bob", loaded.GetCustomProperty("Author") as string);
        Assert.Equal(100.0, Convert.ToDouble(loaded.GetCustomProperty("Pages")));
    }
}

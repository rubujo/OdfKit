using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODM 主控文字文件子文件參照管理 API 的整合測試。
/// </summary>
public class MasterDocumentTests
{
    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.RemoveSubDocumentReference"/> 可移除指定名稱的子文件參照。
    /// </summary>
    [Fact]
    public void RemoveSubDocumentReference_RemovesMatchingSection()
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");
        master.AddSubDocumentReference("Chapter2", "chapter2.odt");

        Assert.True(master.RemoveSubDocumentReference("Chapter1"));
        Assert.False(master.RemoveSubDocumentReference("Chapter1"));

        var remaining = master.GetSubDocumentReferences();
        Assert.Single(remaining);
        Assert.Equal("Chapter2", remaining[0].SectionName);
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.ReorderSubDocumentReferences"/> 可依指定順序重新排列子文件參照，並於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void ReorderSubDocumentReferences_ReordersAndPersists()
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");
        master.AddSubDocumentReference("Chapter2", "chapter2.odt");
        master.AddSubDocumentReference("Chapter3", "chapter3.odt");

        master.ReorderSubDocumentReferences(new List<string> { "Chapter3", "Chapter1", "Chapter2" });

        var reordered = master.GetSubDocumentReferences();
        Assert.Equal(new[] { "Chapter3", "Chapter1", "Chapter2" }, reordered.Select(r => r.SectionName));

        using var stream = new MemoryStream();
        master.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextMasterDocument.Load(stream);
        var persisted = loaded.GetSubDocumentReferences();
        Assert.Equal(new[] { "Chapter3", "Chapter1", "Chapter2" }, persisted.Select(r => r.SectionName));
    }

    /// <summary>
    /// 驗證子文件參照的載入時機（<c>xlink:actuate</c>）可透過 <c>loadOnRequest</c> 參數指定，
    /// 並透過 <see cref="TextMasterDocument.SetSubDocumentLoadOnRequest"/> 變更，於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void SubDocumentActuate_SetAndPersistAfterSaveAndLoad()
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");
        master.AddSubDocumentReference("Chapter2", "chapter2.odt", loadOnRequest: true);

        var references = master.GetSubDocumentReferences();
        Assert.Equal("onLoad", references.Single(r => r.SectionName == "Chapter1").Actuate);
        Assert.Equal("onRequest", references.Single(r => r.SectionName == "Chapter2").Actuate);

        Assert.True(master.SetSubDocumentLoadOnRequest("Chapter1", true));
        Assert.False(master.SetSubDocumentLoadOnRequest("NotExist", true));

        using var stream = new MemoryStream();
        master.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextMasterDocument.Load(stream);
        var persisted = loaded.GetSubDocumentReferences();
        Assert.Equal("onRequest", persisted.Single(r => r.SectionName == "Chapter1").Actuate);
        Assert.Equal("onRequest", persisted.Single(r => r.SectionName == "Chapter2").Actuate);
    }
}

using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Z-3 ODF 版本宣告 API 的整合測試。
/// </summary>
public class OdfVersionDeclarationTests
{
    private static (string manifestXml, string contentXml) GetXmls(TextDocument doc)
    {
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;
        using var pkg = OdfPackage.Open(ms, leaveOpen: true);

        string manifestXml;
        using (var s = pkg.GetEntryStream("META-INF/manifest.xml"))
            manifestXml = new System.IO.StreamReader(s).ReadToEnd();

        string contentXml;
        using (var s = pkg.GetEntryStream("content.xml"))
            contentXml = new System.IO.StreamReader(s).ReadToEnd();

        return (manifestXml, contentXml);
    }

    /// <summary>
    /// 驗證預設文件的 manifest root 含有 manifest:version="1.4"（預設版本）。
    /// </summary>
    [Fact]
    public void DefaultDocument_ManifestRootContainsVersion()
    {
        using var doc = TextDocument.Create();
        var (manifestXml, _) = GetXmls(doc);
        Assert.Contains("manifest:version=\"1.4\"", manifestXml);
    }

    /// <summary>
    /// 驗證預設文件的 manifest root file-entry 含有 manifest:version 屬性。
    /// </summary>
    [Fact]
    public void DefaultDocument_ManifestRootFileEntryContainsVersion()
    {
        using var doc = TextDocument.Create();
        var (manifestXml, _) = GetXmls(doc);
        Assert.Contains("manifest:full-path=\"/\"", manifestXml);
        Assert.Contains("manifest:media-type=", manifestXml);
        Assert.Contains("manifest:version=", manifestXml);
    }

    /// <summary>
    /// 驗證預設文件的 content.xml 含有 office:version="1.4"（預設版本）。
    /// </summary>
    [Fact]
    public void DefaultDocument_ContentXmlContainsOfficeVersion()
    {
        using var doc = TextDocument.Create();
        var (_, contentXml) = GetXmls(doc);
        Assert.Contains("office:version=\"1.4\"", contentXml);
    }

    /// <summary>
    /// 驗證設定 TargetVersion = Odf13 後，存檔 manifest 含有 manifest:version="1.3"。
    /// </summary>
    [Fact]
    public void TargetVersion_Odf13_ManifestVersionIs13()
    {
        using var doc = TextDocument.Create();
        doc.TargetVersion = OdfVersion.Odf13;
        var (manifestXml, _) = GetXmls(doc);
        Assert.Contains("manifest:version=\"1.3\"", manifestXml);
    }

    /// <summary>
    /// 驗證設定 TargetVersion = Odf13 後，存檔 content.xml 含有 office:version="1.3"。
    /// </summary>
    [Fact]
    public void TargetVersion_Odf13_ContentXmlVersionIs13()
    {
        using var doc = TextDocument.Create();
        doc.TargetVersion = OdfVersion.Odf13;
        var (_, contentXml) = GetXmls(doc);
        Assert.Contains("office:version=\"1.3\"", contentXml);
    }

    /// <summary>
    /// 驗證設定 TargetVersion = Odf12 後，存檔版本宣告正確為 1.2。
    /// </summary>
    [Fact]
    public void TargetVersion_Odf12_VersionStringsAre12()
    {
        using var doc = TextDocument.Create();
        doc.TargetVersion = OdfVersion.Odf12;
        var (manifestXml, contentXml) = GetXmls(doc);
        Assert.Contains("manifest:version=\"1.2\"", manifestXml);
        Assert.Contains("office:version=\"1.2\"", contentXml);
    }

    /// <summary>
    /// 驗證 OdfSaveOptions.ForceVersion 優先於 TargetVersion。
    /// </summary>
    [Fact]
    public void SaveOptions_ForceVersion_OverridesTargetVersion()
    {
        using var doc = TextDocument.Create();
        doc.TargetVersion = OdfVersion.Odf12;

        using var ms = new MemoryStream();
        doc.SaveToStream(ms, new OdfSaveOptions { ForceVersion = OdfVersion.Odf14 });
        ms.Position = 0;

        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var s = pkg.GetEntryStream("content.xml");
        string contentXml = new System.IO.StreamReader(s).ReadToEnd();

        Assert.Contains("office:version=\"1.4\"", contentXml);
    }

    /// <summary>
    /// 驗證 TargetVersion 為 null 時不變更現有 DOM 的版本字串。
    /// </summary>
    [Fact]
    public void TargetVersion_Null_DoesNotChangeDefaultVersion()
    {
        using var doc = TextDocument.Create();
        doc.TargetVersion = null;
        var (manifestXml, contentXml) = GetXmls(doc);
        Assert.Contains("1.4", manifestXml);
        Assert.Contains("office:version=\"1.4\"", contentXml);
    }
}

using System.IO;
using System.Text;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證封裝專案在未指定 MIME 類型時的自動判定行為。
/// </summary>
public class OdfPackageAutoMediaTypeTests
{
    [Fact]
    public void WriteEntry_WithoutMediaType_AutoResolvesFromEntryPath()
    {
        using var package = OdfPackage.Create(new MemoryStream());
        package.SetMimeType("application/vnd.oasis.opendocument.text");

        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<root/>"));
        package.WriteEntry("Pictures/logo.png", [0x89, 0x50, 0x4E, 0x47]);

        Assert.Equal("text/xml", package.Manifest["content.xml"]);
        Assert.Equal("image/png", package.Manifest["Pictures/logo.png"]);
    }

    [Fact]
    public void WriteEntry_StreamWithoutMediaType_AutoUpdatesEmbeddedDocumentMime()
    {
        using var package = OdfPackage.Create(new MemoryStream());
        package.SetMimeType("application/vnd.oasis.opendocument.text");

        using var mimeStream = new MemoryStream(Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"));
        package.WriteEntry("Object1/mimetype", mimeStream);
        package.WriteEntry("Object1/content.xml", Encoding.UTF8.GetBytes("<office:document-content/>"));

        Assert.Equal(string.Empty, package.Manifest["Object1/mimetype"]);
        Assert.Equal("application/vnd.oasis.opendocument.formula", package.Manifest["Object1/"]);
        Assert.Equal("text/xml", package.Manifest["Object1/content.xml"]);
    }
}

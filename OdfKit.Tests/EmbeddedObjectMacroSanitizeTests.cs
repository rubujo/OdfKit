using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：<see cref="OdfPackage.SanitizeMacros"/> 必須同時移除內嵌物件（子文件）內的巨集與巨集簽章。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class EmbeddedObjectMacroSanitizeTests
{
    private static readonly string[] MacroEntries =
    [
        "Basic/Standard/Module1.xba",
        "Scripts/python/x.py",
        "Object 1/Basic/Standard/Module1.xba",
        "Object 1/Scripts/python/y.py",
        "Object 1/Nested/Basic/Module2.xba",
        "Object 1/META-INF/macrosignatures.xml",
        "META-INF/macrosignatures.xml",
    ];

    private static readonly string[] BenignEntries =
    [
        "content.xml",
        "Object 1/content.xml",
        "Pictures/basic.png",
        "Pictures/Scripts.png",
        "basic.txt",
    ];

    private static byte[] BuildPackage()
    {
        var manifest = new StringBuilder();
        manifest.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        manifest.Append("<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">");
        manifest.Append("<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>");
        foreach (string name in MacroEntries)
        {
            manifest.Append("<manifest:file-entry manifest:full-path=\"").Append(name).Append("\" manifest:media-type=\"text/plain\"/>");
        }

        foreach (string name in BenignEntries)
        {
            manifest.Append("<manifest:file-entry manifest:full-path=\"").Append(name).Append("\" manifest:media-type=\"text/plain\"/>");
        }

        manifest.Append("</manifest:manifest>");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text, CompressionLevel level = CompressionLevel.Optimal)
            {
                using Stream w = zip.CreateEntry(name, level).Open();
                byte[] b = Encoding.UTF8.GetBytes(text);
                w.Write(b, 0, b.Length);
            }

            Add("mimetype", "application/vnd.oasis.opendocument.text", CompressionLevel.NoCompression);
            Add("META-INF/manifest.xml", manifest.ToString());
            foreach (string name in MacroEntries)
            {
                Add(name, name.EndsWith(".xml", System.StringComparison.Ordinal) ? "<x/>" : "payload");
            }

            foreach (string name in BenignEntries)
            {
                Add(
                    name,
                    name == "content.xml" || name == "Object 1/content.xml"
                        ? "<?xml version=\"1.0\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>"
                        : "payload");
            }
        }

        return ms.ToArray();
    }

    [Fact]
    public void SanitizeMacros_RemovesMacrosAtAnyDepthAndKeepsBenignEntries()
    {
        using var package = OdfPackage.Open(new MemoryStream(BuildPackage()));
        foreach (string name in MacroEntries)
        {
            Assert.True(package.HasEntry(name), $"前置條件：{name} 應存在。");
        }

        package.SanitizeMacros();

        foreach (string name in MacroEntries)
        {
            Assert.False(package.HasEntry(name), $"巨集項目 {name} 未被移除。");
        }

        foreach (string name in BenignEntries)
        {
            Assert.True(package.HasEntry(name), $"一般項目 {name} 不應被移除。");
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證高階文件保存流程不會刪除或改寫未知封裝專案。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class OdfPackageUnknownEntryTests
{
    private static readonly IReadOnlyDictionary<string, byte[]> UnknownEntries = new Dictionary<string, byte[]>
    {
        ["Extra/custom.bin"] = [0x00, 0x01, 0x02, 0xFE, 0xFF],
        ["Configurations2/accelerator/current.xml"] = Encoding.UTF8.GetBytes("<config:acceleratorlist xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" />"),
        ["ObjectReplacements/Object 1"] = Encoding.UTF8.GetBytes("replacement-placeholder"),
        ["Media/unknown-image.bin"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A]
    };

    /// <summary>
    /// 驗證高階 wrapper 修改核心 XML 後仍保留未知 package entries。
    /// </summary>
    [Fact]
    public void HighLevelSavePreservesUnknownPackageEntries()
    {
        using MemoryStream source = CreateTextPackageWithUnknownEntries();
        using OdfDocument document = OdfDocument.Load(source, "document.odt");
        TextDocument textDocument = Assert.IsType<TextDocument>(document);

        textDocument.AddParagraph("觸發高階文件修改");

        using var saved = new MemoryStream();
        textDocument.SaveToStream(saved);
        saved.Position = 0;

        using OdfPackage package = OdfPackage.Open(saved, leaveOpen: true);
        foreach (KeyValuePair<string, byte[]> expected in UnknownEntries)
        {
            Assert.True(package.HasEntry(expected.Key), "缺少未知封裝項目：" + expected.Key);
            Assert.Equal(expected.Value, ReadEntryBytes(package, expected.Key));
        }
    }

    /// <summary>
    /// 驗證 package-level 保存流程保留未知 package entries 與 manifest 記錄。
    /// </summary>
    [Fact]
    public void PackageSavePreservesUnknownPackageEntriesAndManifestRecords()
    {
        using MemoryStream source = CreateTextPackageWithUnknownEntries();
        using OdfPackage package = OdfPackage.Open(source, leaveOpen: true);

        using var saved = new MemoryStream();
        package.SaveToStream(saved);
        saved.Position = 0;

        using OdfPackage reopened = OdfPackage.Open(saved, leaveOpen: true);
        foreach (KeyValuePair<string, byte[]> expected in UnknownEntries)
        {
            Assert.True(reopened.HasEntry(expected.Key), "缺少未知封裝項目：" + expected.Key);
            Assert.True(reopened.Manifest.ContainsKey(expected.Key), "manifest 缺少未知封裝項目：" + expected.Key);
            Assert.Equal(expected.Value, ReadEntryBytes(reopened, expected.Key));
        }
    }

    private static MemoryStream CreateTextPackageWithUnknownEntries()
    {
        var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.WriteEntry("Extra/custom.bin", UnknownEntries["Extra/custom.bin"], "application/octet-stream");
            package.WriteEntry("Configurations2/accelerator/current.xml", UnknownEntries["Configurations2/accelerator/current.xml"], "text/xml");
            package.WriteEntry("ObjectReplacements/Object 1", UnknownEntries["ObjectReplacements/Object 1"], "application/octet-stream");
            package.WriteEntry("Media/unknown-image.bin", UnknownEntries["Media/unknown-image.bin"], "application/octet-stream");
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static byte[] ReadEntryBytes(OdfPackage package, string entryName)
    {
        using Stream stream = package.GetEntryStream(entryName);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：以檔案路徑載入（MMF 快速路徑）時，中央目錄不得被無聲截斷，且必須正確處理 ZIP64。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class ZipDirectoryIntegrityTests
{
    private static readonly string[] ExtraEntries = ["a.txt", "b.txt", "c.txt"];

    private const string ContentXml =
        "<?xml version=\"1.0\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>";

    private static string BuildManifest(bool includeExtras)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">");
        sb.Append("<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>");
        sb.Append("<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>");
        if (includeExtras)
        {
            foreach (string name in ExtraEntries)
            {
                sb.Append("<manifest:file-entry manifest:full-path=\"").Append(name).Append("\" manifest:media-type=\"text/plain\"/>");
            }
        }

        sb.Append("</manifest:manifest>");
        return sb.ToString();
    }

    private static byte[] BuildStandardPackage()
    {
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
            Add("META-INF/manifest.xml", BuildManifest(includeExtras: true));
            Add("content.xml", ContentXml);
            foreach (string name in ExtraEntries)
            {
                Add(name, "payload-" + name);
            }
        }

        return ms.ToArray();
    }

    private static uint Crc(byte[] bytes)
    {
        using var sink = new MemoryStream();
        using var crc = new OdfCrc32Stream(sink);
        crc.Write(bytes, 0, bytes.Length);
        return crc.Crc32;
    }

    /// <summary>手工組出所有項目皆以 ZIP64 extra field 記錄大小的封裝（標頭欄位為 0xFFFFFFFF sentinel）。</summary>
    private static byte[] BuildZip64Package()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var directory = new List<(string Name, byte[] Data, long Offset)>();
        (string Name, string Text)[] files =
        [
            ("mimetype", "application/vnd.oasis.opendocument.text"),
            ("META-INF/manifest.xml", BuildManifest(includeExtras: false)),
            ("content.xml", ContentXml),
        ];

        foreach ((string name, string text) in files)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            long offset = ms.Position;
            w.Write(0x04034b50);
            w.Write((ushort)45);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write(0u);
            w.Write(Crc(data));
            w.Write(0xFFFFFFFFu);
            w.Write(0xFFFFFFFFu);
            w.Write((ushort)nameBytes.Length);
            w.Write((ushort)20);
            w.Write(nameBytes);
            w.Write((ushort)1);
            w.Write((ushort)16);
            w.Write((long)data.Length);
            w.Write((long)data.Length);
            w.Write(data);
            directory.Add((name, data, offset));
        }

        long directoryStart = ms.Position;
        foreach ((string name, byte[] data, long offset) in directory)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            w.Write(0x02014b50);
            w.Write((ushort)45);
            w.Write((ushort)45);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write(0u);
            w.Write(Crc(data));
            w.Write(0xFFFFFFFFu);
            w.Write(0xFFFFFFFFu);
            w.Write((ushort)nameBytes.Length);
            w.Write((ushort)20);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write(0u);
            w.Write((uint)offset);
            w.Write(nameBytes);
            w.Write((ushort)1);
            w.Write((ushort)16);
            w.Write((long)data.Length);
            w.Write((long)data.Length);
        }

        long directorySize = ms.Position - directoryStart;
        w.Write(0x06054b50);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((ushort)directory.Count);
        w.Write((ushort)directory.Count);
        w.Write((uint)directorySize);
        w.Write((uint)directoryStart);
        w.Write((ushort)0);
        w.Flush();
        return ms.ToArray();
    }

    private static string WriteTempFile(byte[] data)
    {
        string path = Path.Combine(Path.GetTempPath(), "odfkit-zipdir-" + Guid.NewGuid().ToString("N") + ".odt");
        File.WriteAllBytes(path, data);
        return path;
    }

    /// <summary>載入必須「完整成功」或「明確失敗」，不得無聲遺失項目。</summary>
    private static void AssertNoSilentLoss(string path)
    {
        OdfPackage? package = null;
        Exception? failure = Record.Exception(() => package = OdfPackage.Open(path));
        using (package)
        {
            if (failure is null)
            {
                Assert.NotNull(package);
                foreach (string name in ExtraEntries)
                {
                    Assert.True(package!.HasEntry(name), $"項目 {name} 無聲遺失。");
                }
            }
        }
    }

    [Fact]
    public void FileLoadWithCorruptedCentralDirectoryRecordNeverLoadsPartialEntries()
    {
        byte[] data = BuildStandardPackage();

        // 依序為 mimetype、manifest、content、a、b、c：破壞第 5 筆（b.txt）記錄簽章。
        int seen = 0;
        for (int i = 0; i + 4 < data.Length; i++)
        {
            if (data[i] == 0x50 && data[i + 1] == 0x4B && data[i + 2] == 1 && data[i + 3] == 2 && ++seen == 5)
            {
                data[i + 2] = 0x7F;
                break;
            }
        }

        Assert.Equal(5, seen);
        string path = WriteTempFile(data);
        try
        {
            AssertNoSilentLoss(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FileLoadWithOverstatedRecordCountNeverLoadsPartialEntries()
    {
        byte[] data = BuildStandardPackage();
        int eocd = data.AsSpan().LastIndexOf(new byte[] { 0x50, 0x4B, 0x05, 0x06 });
        Assert.True(eocd > 0);

        // EOCD 宣告的記錄數多於實際存在的中央目錄記錄。
        ushort declared = BitConverter.ToUInt16(data, eocd + 10);
        BitConverter.GetBytes((ushort)(declared + 1)).CopyTo(data, eocd + 10);
        BitConverter.GetBytes((ushort)(declared + 1)).CopyTo(data, eocd + 8);

        string path = WriteTempFile(data);
        try
        {
            AssertNoSilentLoss(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FileLoadWithZip64SentinelsLoadsEntriesLikeStreamLoad()
    {
        byte[] data = BuildZip64Package();

        using (OdfPackage fromStream = OdfPackage.Open(new MemoryStream(data)))
        {
            Assert.True(fromStream.HasEntry("content.xml"));
        }

        string path = WriteTempFile(data);
        try
        {
            using OdfPackage fromFile = OdfPackage.Open(path);
            Assert.True(fromFile.HasEntry("mimetype"));
            Assert.True(fromFile.HasEntry("content.xml"));
            Assert.Equal(ContentXml, Encoding.UTF8.GetString(fromFile.ReadEntry("content.xml")));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FileLoadWithWellFormedPackageLoadsAllEntries()
    {
        string path = WriteTempFile(BuildStandardPackage());
        try
        {
            using OdfPackage package = OdfPackage.Open(path);
            foreach (string name in ExtraEntries)
            {
                Assert.Equal("payload-" + name, Encoding.UTF8.GetString(package.ReadEntry(name)));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}

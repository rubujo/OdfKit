using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證封裝載入與另存後，會保留原始 ZIP entry 的壓縮模式。
/// </summary>
public class OdfPackageCompressionPreserveTests
{
    [Fact]
    public void OpenAndSave_PreservesStoredCompressionForEmptyEntry()
    {
        using var source = CreatePackageWithStoredEntry("Extra/empty.bin", Array.Empty<byte>());
        using var package = OdfPackage.Open(source, leaveOpen: true);

        object emptyEntry = GetPackageEntry(package, "Extra/empty.bin");
        PropertyInfo isCompressedProperty = emptyEntry.GetType().GetProperty("IsCompressed")!;
        Assert.NotNull(isCompressedProperty);
        Assert.False((bool)isCompressedProperty.GetValue(emptyEntry)!);
    }

    [Fact]
    public void OpenAndSave_RoundTripsStoredCompressionMethod()
    {
        const string entryName = "Extra/stored.bin";
        byte[] content = new byte[1024];
        Array.Fill(content, (byte)'A');
        using var source = CreatePackageWithStoredEntry(entryName, content);
        using var package = OdfPackage.Open(source, leaveOpen: true);
        using var saved = new MemoryStream();

        package.SaveToStream(saved);
        saved.Position = 0;

        using var archive = new ZipArchive(saved, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry storedEntry = archive.GetEntry(entryName)!;

        Assert.NotNull(storedEntry);
        Assert.True(IsStoredEntry(storedEntry));
        Assert.Equal(content.Length, storedEntry.Length);
    }

    [Fact]
    public void SaveToStream_CopiesUnmodifiedMmfEntryCompressedPayload()
    {
        const string entryName = "Extra/raw-copy.txt";
        string tempPath = Path.Combine(Path.GetTempPath(), $"odfkit_raw_copy_{Guid.NewGuid():N}.odt");
        byte[] content = new byte[4096];
        Array.Fill(content, (byte)'A');

        try
        {
            using (OdfPackage package = OdfDocumentFactory.CreatePackage(tempPath, OdfDocumentKind.Text))
            {
                package.WriteEntry(entryName, content, "text/plain");
                package.Save();
            }

            byte[] originalCompressedPayload = ReadCompressedPayload(File.ReadAllBytes(tempPath), entryName, out ushort originalMethod);
            Assert.Equal((ushort)8, originalMethod);

            using var opened = OdfPackage.Open(tempPath);
            using var saved = new MemoryStream();

            opened.SaveToStream(saved);

            byte[] savedCompressedPayload = ReadCompressedPayload(saved.ToArray(), entryName, out ushort savedMethod);
            Assert.Equal(originalMethod, savedMethod);
            Assert.Equal(originalCompressedPayload, savedCompressedPayload);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void FallbackSave_LargeEntryAvoidsExtraByteCloneAndRoundTrips()
    {
        byte[] payload = Enumerable.Range(0, 512 * 1024).Select(static value => (byte)(value % 251)).ToArray();
        using var source = new MemoryStream();
        using var destination = new MemoryStream();
        using (var package = OdfPackage.Create(source, leaveOpen: true, new OdfSaveOptions { Password = "force-fallback" }))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.WriteEntry("Pictures/large.bin", payload, "application/octet-stream");
            package.WriteEntry("META-INF/manifest.xml", Encoding.UTF8.GetBytes(CreateMinimalManifestXml("Pictures/large.bin")), "text/xml");
            OdfPackageArchiveWriter.LastFallbackCrcWriteCount = 0;
            OdfPackageArchiveWriter.LastFallbackEntryByteCloneCount = 0;
            OdfPackageArchiveWriter.LastParallelPreparedEntryCount = 0;

            package.SaveCollaborators.WriteToArchive(destination);
        }

        Assert.Equal(4, OdfPackageArchiveWriter.LastFallbackCrcWriteCount);
        Assert.Equal(0, OdfPackageArchiveWriter.LastFallbackEntryByteCloneCount);
        Assert.Equal(0, OdfPackageArchiveWriter.LastParallelPreparedEntryCount);

        destination.Position = 0;
        using (var archive = new ZipArchive(destination, ZipArchiveMode.Read, leaveOpen: true))
        {
            ZipArchiveEntry? entry = archive.GetEntry("Pictures/large.bin");
            Assert.NotNull(entry);
            using Stream entryStream = entry!.Open();
            using var copied = new MemoryStream();
            entryStream.CopyTo(copied);
            Assert.Equal(payload, copied.ToArray());
        }

        destination.Position = 0;
        using OdfPackage reloaded = OdfPackage.Open(destination, leaveOpen: true);
        Assert.Equal(payload, reloaded.ReadEntry("Pictures/large.bin"));
    }

    [Fact]
    public void FallbackWriter_ComputesCrcDuringSingleWritePass()
    {
        byte[] payload = Encoding.UTF8.GetBytes(new string('x', 4096));
        using var output = new MemoryStream();
        OdfPackageArchiveWriter.LastFallbackCrcWriteCount = 0;

        uint crc = OdfPackageArchiveWriter.WriteEntryContentWithCrc32ForTests(output, payload);

        Assert.Equal(1, OdfPackageArchiveWriter.LastFallbackCrcWriteCount);
        Assert.Equal(OdfCrc32.Compute(payload), crc);
        Assert.Equal(payload, output.ToArray());
    }

    [Fact]
    public void Crc32Stream_ReadDetectsCrcMismatch()
    {
        byte[] payload = Encoding.UTF8.GetBytes("crc mismatch payload");
        uint wrongCrc = OdfCrc32.Compute(payload) ^ 0xFFFFFFFFu;
        using var input = new MemoryStream(payload);
        using var crcStream = new OdfCrc32Stream(input, wrongCrc);
        byte[] buffer = new byte[8];

        Assert.Throws<InvalidDataException>(() =>
        {
            while (crcStream.Read(buffer, 0, buffer.Length) > 0)
            { }
        });
    }

    [Fact]
    public async Task LoadEntries_TracksEntryOrderWithDuplicates()
    {
        byte[] first = Encoding.UTF8.GetBytes("first");
        byte[] second = Encoding.UTF8.GetBytes("second");
        using MemoryStream packageStream = CreateZipPackage(
            ("mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.text")),
            ("content.xml", first),
            ("content.xml", second),
            ("styles.xml", Encoding.UTF8.GetBytes("<styles/>")),
            ("META-INF/manifest.xml", Encoding.UTF8.GetBytes(CreateMinimalManifestXml())));

        using OdfPackage package = OdfPackage.Open(packageStream, leaveOpen: true);

        Assert.Contains("content.xml", package.DuplicateEntryNames);
        Assert.Equal(new[] { "mimetype", "content.xml", "styles.xml", "META-INF/manifest.xml" }, package.EntryOrder);
        Assert.Equal(second, package.ReadEntry("content.xml"));

        packageStream.Position = 0;
        await using OdfPackage asyncPackage = await OdfPackage.OpenAsync(
            packageStream,
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("content.xml", asyncPackage.DuplicateEntryNames);
        Assert.Equal(new[] { "mimetype", "content.xml", "styles.xml", "META-INF/manifest.xml" }, asyncPackage.EntryOrder);
        Assert.Equal(second, asyncPackage.ReadEntry("content.xml"));
    }

    [Fact]
    public void LoadEntriesFromMmf_TracksEntryOrderWithoutLinearContains()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"odfkit_mmf_entry_order_{Guid.NewGuid():N}.odt");
        try
        {
            using (MemoryStream packageStream = CreateZipPackage(
                ("mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.text")),
                ("content.xml", Encoding.UTF8.GetBytes("<content/>")),
                ("styles.xml", Encoding.UTF8.GetBytes("<styles/>")),
                ("meta.xml", Encoding.UTF8.GetBytes("<meta/>")),
                ("META-INF/manifest.xml", Encoding.UTF8.GetBytes(CreateMinimalManifestXml()))))
            {
                File.WriteAllBytes(tempPath, packageStream.ToArray());
            }

            using OdfPackage package = OdfPackage.Open(
                tempPath,
                new OdfLoadOptions { AllowLazyLoading = true });

            Assert.NotNull(package.MmfEntries);
            Assert.Equal(
                new[] { "mimetype", "content.xml", "styles.xml", "meta.xml", "META-INF/manifest.xml" },
                package.EntryOrder);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static MemoryStream CreatePackageWithStoredEntry(string storedEntryName, byte[] storedEntryContent)
    {
        var baseStream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(baseStream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.WriteEntry(storedEntryName, storedEntryContent, "application/octet-stream");
            package.Save();
        }

        baseStream.Position = 0;
        using var inputZip = new ZipArchive(baseStream, ZipArchiveMode.Read, leaveOpen: true);
        var rebuilt = new MemoryStream();
        using (var outputZip = new ZipArchive(rebuilt, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in inputZip.Entries)
            {
                CompressionLevel level = string.Equals(entry.FullName, storedEntryName, StringComparison.Ordinal)
                    ? CompressionLevel.NoCompression
                    : CompressionLevel.Optimal;
                ZipArchiveEntry copy = outputZip.CreateEntry(entry.FullName, level);
                using Stream src = entry.Open();
                using Stream dst = copy.Open();
                src.CopyTo(dst);
            }
        }

        rebuilt.Position = 0;
        return rebuilt;
    }

    private static string CreateMinimalManifestXml(params string[] additionalEntries)
    {
        var builder = new StringBuilder();
        builder.Append("<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">");
        builder.Append("<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\" />");
        builder.Append("<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\" />");
        builder.Append("<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\" />");
        foreach (string entry in additionalEntries)
        {
            builder.Append("<manifest:file-entry manifest:full-path=\"");
            builder.Append(entry);
            builder.Append("\" manifest:media-type=\"application/octet-stream\" />");
        }

        builder.Append("</manifest:manifest>");
        return builder.ToString();
    }

    private static MemoryStream CreateZipPackage(params (string Name, byte[] Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                using Stream entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static bool IsStoredEntry(ZipArchiveEntry entry)
    {
        FieldInfo? compressionMethodField = typeof(ZipArchiveEntry).GetField(
                "_compressionMethod",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(ZipArchiveEntry).GetField(
                "m_compressionMethod",
                BindingFlags.NonPublic | BindingFlags.Instance);

        if (compressionMethodField is null)
        {
            return entry.CompressedLength == entry.Length;
        }

        object? value = compressionMethodField.GetValue(entry);
        return value is not null && Convert.ToInt32(value) == 0;
    }

    private static byte[] ReadCompressedPayload(byte[] zipBytes, string entryName, out ushort compressionMethod)
    {
        using var stream = new MemoryStream(zipBytes, writable: false);
        using var reader = new BinaryReader(stream);
        long eocdOffset = FindEocdOffset(zipBytes);
        Assert.True(eocdOffset >= 0);

        stream.Position = eocdOffset + 10;
        ushort totalRecords = reader.ReadUInt16();
        _ = reader.ReadUInt32();
        uint centralDirectoryOffset = reader.ReadUInt32();

        stream.Position = centralDirectoryOffset;
        for (int i = 0; i < totalRecords; i++)
        {
            Assert.Equal(0x02014b50u, reader.ReadUInt32());
            stream.Position += 4;
            _ = reader.ReadUInt16();
            compressionMethod = reader.ReadUInt16();
            stream.Position += 4;
            _ = reader.ReadUInt32();
            uint compressedSize = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            ushort fileNameLength = reader.ReadUInt16();
            ushort extraFieldLength = reader.ReadUInt16();
            ushort commentLength = reader.ReadUInt16();
            stream.Position += 8;
            uint localHeaderOffset = reader.ReadUInt32();
            string name = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(fileNameLength));
            stream.Position += extraFieldLength + commentLength;

            if (!string.Equals(name, entryName, StringComparison.Ordinal))
                continue;

            long savedPosition = stream.Position;
            stream.Position = localHeaderOffset;
            Assert.Equal(0x04034b50u, reader.ReadUInt32());
            stream.Position += 22;
            ushort localFileNameLength = reader.ReadUInt16();
            ushort localExtraFieldLength = reader.ReadUInt16();
            long dataOffset = localHeaderOffset + 30 + localFileNameLength + localExtraFieldLength;
            stream.Position = dataOffset;
            byte[] payload = reader.ReadBytes(checked((int)compressedSize));
            stream.Position = savedPosition;
            return payload;
        }

        throw new InvalidDataException($"找不到 ZIP entry：{entryName}");
    }

    private static long FindEocdOffset(byte[] zipBytes)
    {
        int searchStart = Math.Max(0, zipBytes.Length - 65557);
        for (int i = zipBytes.Length - 22; i >= searchStart; i--)
        {
            if (zipBytes[i] == 0x50 &&
                zipBytes[i + 1] == 0x4B &&
                zipBytes[i + 2] == 0x05 &&
                zipBytes[i + 3] == 0x06)
            {
                return i;
            }
        }

        return -1;
    }

    private static object GetPackageEntry(OdfPackage package, string entryName)
    {
        FieldInfo? entriesField = typeof(OdfPackage).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(entriesField);
        object? entriesObject = entriesField!.GetValue(package);
        Assert.NotNull(entriesObject);

        var tryGetValueMethod = entriesObject!.GetType().GetMethod("TryGetValue");
        Assert.NotNull(tryGetValueMethod);
        object?[] args = [entryName, null];
        bool found = (bool)tryGetValueMethod!.Invoke(entriesObject, args)!;
        Assert.True(found);
        Assert.NotNull(args[1]);
        return args[1]!;
    }
}

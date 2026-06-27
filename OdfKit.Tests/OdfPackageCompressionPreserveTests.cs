using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
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

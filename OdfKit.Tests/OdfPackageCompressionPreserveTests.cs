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
        using var source = CreatePackageWithStoredEmptyEntry();
        using var package = OdfPackage.Open(source, leaveOpen: true);

        object emptyEntry = GetPackageEntry(package, "Extra/empty.bin");
        PropertyInfo isCompressedProperty = emptyEntry.GetType().GetProperty("IsCompressed")!;
        Assert.NotNull(isCompressedProperty);
        Assert.False((bool)isCompressedProperty.GetValue(emptyEntry)!);
    }

    private static MemoryStream CreatePackageWithStoredEmptyEntry()
    {
        var baseStream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(baseStream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.WriteEntry("Extra/empty.bin", Array.Empty<byte>(), "application/octet-stream");
            package.Save();
        }

        baseStream.Position = 0;
        using var inputZip = new ZipArchive(baseStream, ZipArchiveMode.Read, leaveOpen: true);
        var rebuilt = new MemoryStream();
        using (var outputZip = new ZipArchive(rebuilt, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in inputZip.Entries)
            {
                CompressionLevel level = string.Equals(entry.FullName, "Extra/empty.bin", StringComparison.Ordinal)
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

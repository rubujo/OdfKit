using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 對抗測試：ZIP 標頭（中央目錄與本機標頭）謊報未壓縮大小時，
/// 實際解壓出的位元組數不得超過 <see cref="OdfLoadOptions.MaxEntrySize"/>。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class ZipHeaderSizeLieTests(Xunit.ITestOutputHelper output)
{
    private const string BombEntryName = "Pictures/bomb.bin";
    private const int RealSize = 20 * 1024 * 1024;
    private const long EntryLimit = 1024 * 1024;
    private const int ReadCap = 32 * 1024 * 1024;

    /// <summary>建立含高壓縮比項目的 ODT 封裝，並把該項目的宣告大小改為 <paramref name="declaredSize"/>。</summary>
    private static byte[] BuildPackageWithLyingHeader(uint declaredSize)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var w = mime.Open())
            {
                byte[] b = Encoding.ASCII.GetBytes("application/vnd.oasis.opendocument.text");
                w.Write(b, 0, b.Length);
            }

            ZipArchiveEntry content = zip.CreateEntry("content.xml", CompressionLevel.Optimal);
            using (var w = content.Open())
            {
                byte[] b = Encoding.UTF8.GetBytes(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>");
                w.Write(b, 0, b.Length);
            }

            ZipArchiveEntry manifest = zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Optimal);
            using (var w = manifest.Open())
            {
                byte[] b = Encoding.UTF8.GetBytes(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                    "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">" +
                    "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>" +
                    "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>" +
                    "<manifest:file-entry manifest:full-path=\"Pictures/bomb.bin\" manifest:media-type=\"application/octet-stream\"/>" +
                    "</manifest:manifest>");
                w.Write(b, 0, b.Length);
            }

            ZipArchiveEntry bomb = zip.CreateEntry(BombEntryName, CompressionLevel.Optimal);
            using (var w = bomb.Open())
            {
                byte[] chunk = new byte[64 * 1024];
                for (int written = 0; written < RealSize; written += chunk.Length)
                {
                    w.Write(chunk, 0, chunk.Length);
                }
            }
        }

        byte[] data = ms.ToArray();
        byte[] nameBytes = Encoding.UTF8.GetBytes(BombEntryName);
        int patched = 0;

        for (int i = 0; i + 46 < data.Length; i++)
        {
            // 本機標頭 PK\3\4：未壓縮大小位於偏移 22。
            if (data[i] == 0x50 && data[i + 1] == 0x4B && data[i + 2] == 3 && data[i + 3] == 4 &&
                NameMatches(data, i + 30, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i + 26)), nameBytes))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i + 22), declaredSize);
                patched++;
            }
            // 中央目錄 PK\1\2：未壓縮大小位於偏移 24，檔名自偏移 46。
            else if (data[i] == 0x50 && data[i + 1] == 0x4B && data[i + 2] == 1 && data[i + 3] == 2 &&
                NameMatches(data, i + 46, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i + 28)), nameBytes))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i + 24), declaredSize);
                patched++;
            }
        }

        Assert.Equal(2, patched);
        return data;
    }

    private static bool NameMatches(byte[] data, int offset, int length, byte[] expected) =>
        length == expected.Length &&
        offset + length <= data.Length &&
        data.AsSpan(offset, length).SequenceEqual(expected);

    /// <summary>讀取直到 EOF、例外或 <see cref="ReadCap"/>，回傳實際取得的位元組數。</summary>
    private static long DrainStream(Stream stream, out Exception? failure)
    {
        failure = null;
        long total = 0;
        byte[] buffer = new byte[81920];
        try
        {
            int n;
            while (total < ReadCap && (n = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += n;
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        return total;
    }

    private static OdfLoadOptions CreateOptions(bool lazy) => new()
    {
        MaxEntrySize = EntryLimit,
        MaxTotalUncompressedSize = 2 * EntryLimit,
        AllowLazyLoading = lazy,
    };

    [Theory]
    [InlineData(true, true, 1000u)]
    [InlineData(true, false, 1000u)]
    [InlineData(false, true, 1000u)]
    [InlineData(false, false, 1000u)]
    [InlineData(true, true, 0u)]
    [InlineData(false, true, 0u)]
    public void LyingHeaderMustNotAllowExpansionBeyondEntryLimit(bool fromFile, bool lazy, uint declaredSize)
    {
        byte[] package = BuildPackageWithLyingHeader(declaredSize);
        string? path = null;
        long observed = 0;
        Exception? loadFailure = null;
        Exception? readFailure = null;

        try
        {
            Stream? source = null;
            if (fromFile)
            {
                path = Path.Combine(Path.GetTempPath(), "odfkit-lie-" + Guid.NewGuid().ToString("N") + ".odt");
                File.WriteAllBytes(path, package);
            }
            else
            {
                source = new MemoryStream(package);
            }

            try
            {
                using OdfPackage pkg = fromFile
                    ? OdfPackage.Open(path!, CreateOptions(lazy))
                    : OdfPackage.Open(source!, false, CreateOptions(lazy));

                if (pkg.HasEntry(BombEntryName))
                {
                    try
                    {
                        using Stream s = pkg.GetEntryStream(BombEntryName);
                        observed = DrainStream(s, out readFailure);
                    }
                    catch (Exception ex)
                    {
                        readFailure = ex;
                    }
                }
            }
            catch (Exception ex)
            {
                loadFailure = ex;
            }
        }
        finally
        {
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        string diag =
            $"fromFile={fromFile}, lazy={lazy}, declared={declaredSize}, observedBytes={observed}, " +
            $"loadFailure={loadFailure?.GetType().Name}, readFailure={readFailure?.GetType().Name}";

        output.WriteLine(diag + $" | load={loadFailure?.Message} | read={readFailure?.Message}");

        // 安全條件：載入或讀取被拒絕，或實際取得的位元組數不超過單項上限。
        Assert.True(observed <= EntryLimit, "解壓量超過 MaxEntrySize（標頭謊報生效）。" + diag);
    }
}

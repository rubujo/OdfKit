using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 串流寫入器與封裝儲存路徑在取消／失敗時的資源與輸出完整性測試。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class StreamWriterExceptionSafetyTests
{
    /// <summary>
    /// 驗證 <see cref="OdtStreamWriter.CompleteAsync"/> 被取消後，writer 不會停留在
    /// 「已標記為 disposed 但資源未釋放」的狀態，後續仍能得到結構完整的 ZIP。
    /// </summary>
    /// <remarks>
    /// <c>CompleteAsync</c> 一旦進入主體就會把 <c>_disposed</c> 設為 <see langword="true"/>，
    /// 之後 <c>DisposeAsync</c> 會直接返回。因此取消時若沒有經由 <c>finally</c> 釋放
    /// writer、entry 串流與 ZIP，就再也沒有第二次清理機會，輸出會缺少中央目錄。
    /// 進入主體前就取消（此測試涵蓋的路徑）則不得設定 <c>_disposed</c>，否則同樣失去清理機會。
    /// </remarks>
    [Fact]
    public async Task OdtCompleteAsyncCancelledLeavesWriterRecoverable()
    {
        using var target = new MemoryStream();
        using var cts = new CancellationTokenSource();

        var writer = new OdtStreamWriter(target);
        writer.AddParagraph("取消前寫入的內容");
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.CompleteAsync(cts.Token));

        // 取消未消耗掉唯一的清理機會：DisposeAsync 仍應完成最終化。
        await writer.DisposeAsync();

        target.Position = 0;
        using var archive = new ZipArchive(target, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Contains(archive.Entries, entry => entry.FullName == "mimetype");
        Assert.Contains(archive.Entries, entry => entry.FullName == "content.xml");
    }

    /// <summary>
    /// 驗證 <see cref="OdsStreamWriter"/> 的收尾寫入即使中途失敗，ZIP 中央目錄仍會被寫出。
    /// </summary>
    [Fact]
    public void OdsWriterProducesParsableArchiveOnNormalCompletion()
    {
        using var target = new MemoryStream();
        using (var writer = new OdsStreamWriter(target))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell("值");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        target.Position = 0;
        using var archive = new ZipArchive(target, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Contains(archive.Entries, entry => entry.FullName == "content.xml");
        Assert.Contains(archive.Entries, entry => entry.FullName == "styles.xml");
    }

    /// <summary>
    /// 驗證 <see cref="OdfDirectIoReadableStream"/> 以不存在的路徑建構時會擲出，
    /// 且不會洩漏建構期間已配置的對齊原生緩衝區。
    /// </summary>
    /// <remarks>
    /// <c>AlignedNativeBuffer</c> 依 CA2015 不提供 GC 備援釋放，因此建構子失敗若未自行清理，
    /// 每次失敗都會永久洩漏兩個 64 KiB 原生配置。量測必須用進程私有位元組而非
    /// <see cref="GC.GetAllocatedBytesForCurrentThread"/>：後者只計算託管堆積，而
    /// <c>NativeMemory.AlignedAlloc</c> 配置的記憶體完全不在其中，用它會得到恆真的假斷言。
    /// 512 次失敗的洩漏量約 64 MiB，足以蓋過進程層級量測的雜訊。
    /// </remarks>
    [Fact]
    public void DirectIoReadableStreamConstructorFailureDoesNotLeakNativeBuffers()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"odfkit_missing_{Guid.NewGuid():N}.odt");
        Assert.False(File.Exists(missingPath));

        const int attempts = 512;
        long before = MeasureStablePrivateBytes();

        for (int i = 0; i < attempts; i++)
        {
            Assert.ThrowsAny<IOException>(() => new OdfDirectIoReadableStream(missingPath));
        }

        long delta = MeasureStablePrivateBytes() - before;
        Assert.True(
            delta < 32L * 1024 * 1024,
            $"建構子失敗後私有位元組增加 {delta / (1024 * 1024)} MiB，疑似未釋放已配置緩衝區");
    }

    private static long MeasureStablePrivateBytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.PrivateMemorySize64;
    }

    /// <summary>
    /// 驗證 <see cref="OdfDirectIoWritableStream"/> 以無效目錄建構時會擲出，
    /// 且不會洩漏建構期間已配置的對齊原生緩衝區。
    /// </summary>
    /// <remarks>
    /// 與 <see cref="DirectIoReadableStreamConstructorFailureDoesNotLeakNativeBuffers"/> 同機制，
    /// 但觸發點是後備路徑的 <see cref="FileStream"/> 建構。單次洩漏量較小（4 KiB），
    /// 因此以更多次數放大訊號。
    /// </remarks>
    [Fact]
    public void DirectIoWritableStreamConstructorFailureDoesNotLeakNativeBuffers()
    {
        string invalidPath = Path.Combine(
            Path.GetTempPath(),
            $"odfkit_missing_dir_{Guid.NewGuid():N}",
            "output.odt");
        Assert.False(Directory.Exists(Path.GetDirectoryName(invalidPath)!));

        const int attempts = 8192;
        long before = MeasureStablePrivateBytes();

        for (int i = 0; i < attempts; i++)
        {
            Assert.ThrowsAny<IOException>(() => new OdfDirectIoWritableStream(invalidPath));
        }

        long delta = MeasureStablePrivateBytes() - before;
        Assert.True(
            delta < 16L * 1024 * 1024,
            $"建構子失敗後私有位元組增加 {delta / (1024 * 1024)} MiB，疑似未釋放已配置緩衝區");
    }

    /// <summary>
    /// 驗證封裝儲存在複製途中被取消時，不會讓原始檔案先被截斷為零長度。
    /// </summary>
    [Fact]
    public void PackageSaveDoesNotZeroDestinationBeforeContentIsWritten()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"odfkit_save_truncate_{Guid.NewGuid():N}.odt");
        try
        {
            using (var document = TextDocument.Create())
            {
                document.AddParagraph("原始內容，必須在儲存失敗時仍可辨識");
                document.Save(tempPath);
            }

            long originalLength = new FileInfo(tempPath).Length;
            Assert.True(originalLength > 0);

            // 正常儲存後檔案長度應反映新內容，且不得殘留舊尾端資料。
            using (var package = OdfPackage.Open(tempPath))
            {
                package.WriteEntry("extra.txt", Encoding.UTF8.GetBytes("追加內容"), "text/plain");
                package.Save();
            }

            using (var reopened = OdfPackage.Open(tempPath))
            {
                Assert.True(reopened.HasEntry("content.xml"));
                Assert.Equal("追加內容", Encoding.UTF8.GetString(reopened.ReadEntry("extra.txt")));
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// 驗證路徑式非同步儲存在取消時不會先截斷既有目的檔。
    /// </summary>
    [Fact]
    public async Task DocumentSaveAsyncCancellationPreservesExistingDestination()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"odfkit_document_save_cancel_{Guid.NewGuid():N}.odt");
        byte[] original = Encoding.UTF8.GetBytes("existing destination must survive");
        try
        {
            File.WriteAllBytes(tempPath, original);
            using TextDocument document = TextDocument.Create();
            document.AddParagraph("new content");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => document.SaveAsync(tempPath, cts.Token));

            Assert.Equal(original, File.ReadAllBytes(tempPath));
            string pattern = $".{Path.GetFileName(tempPath)}.odfkit-save-*.tmp";
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(tempPath)!, pattern));
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

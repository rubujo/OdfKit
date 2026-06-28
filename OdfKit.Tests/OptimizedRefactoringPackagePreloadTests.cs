using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

public partial class OptimizedRefactoringTests
{
    /// <summary>
    /// 驗證 MMF lazy preload 會遵守全域 CPU 核心預留平行度。
    /// </summary>
    [Fact]
    public void Test_OdfPackage_MmfPreload_UsesReservedCpuConcurrency()
    {
        double originalRatio = OdfParallelScheduler.ReservationRatio;
        OdfParallelScheduler.ReservationRatio = 0.99d;
        try
        {
            Assert.Equal(
                OdfParallelScheduler.GetEffectiveConcurrency(),
                OdfPackageZipLoader.CreatePreloadParallelOptions().MaxDegreeOfParallelism);
        }
        finally
        {
            OdfParallelScheduler.ReservationRatio = originalRatio;
        }
    }

    /// <summary>
    /// 驗證檔案路徑載入會以 MMF 定位核心 XML entries，並將多個獨立 entry 排入平行預讀。
    /// </summary>
    [Fact]
    public async Task Test_OdfPackage_MmfPreload_QueuesCoreXmlEntriesForParallelRandomAccess()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"odfkit_mmf_preload_{Guid.NewGuid():N}.ods");
        byte[] xml = Encoding.UTF8.GetBytes("<root><item>payload</item></root>");
        byte[] manifest = Encoding.UTF8.GetBytes("""
            <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0">
              <manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet" />
              <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml" />
              <manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml" />
              <manifest:file-entry manifest:full-path="meta.xml" manifest:media-type="text/xml" />
              <manifest:file-entry manifest:full-path="settings.xml" manifest:media-type="text/xml" />
            </manifest:manifest>
            """);

        try
        {
            using (MemoryStream packageStream = CreateZipPackage(
                ("mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet")),
                ("content.xml", xml),
                ("styles.xml", xml),
                ("meta.xml", xml),
                ("settings.xml", xml),
                ("META-INF/manifest.xml", manifest),
                ("Pictures/image.bin", [1, 2, 3, 4])))
            {
                File.WriteAllBytes(tempFile, packageStream.ToArray());
            }

            using OdfPackage package = OdfPackage.Open(
                tempFile,
                new OdfLoadOptions { AllowLazyLoading = true });

            Assert.NotNull(package.MmfEntries);
            Assert.NotNull(package.PreloadTask);

            await package.PreloadTask!.WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(4, OdfPackageZipLoader.LastMmfParallelPreloadEntryCountForTests);
            Assert.Equal(4, OdfPackageZipLoader.LastMmfParallelPreloadVisitedEntryCountForTests);
            Assert.Equal(
                OdfParallelScheduler.GetEffectiveConcurrency(),
                OdfPackageZipLoader.LastMmfParallelPreloadMaxDegreeForTests);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// 驗證平行調度器會在工作委派期間暫時套用執行緒優先權，並於完成後還原。
    /// </summary>
    [Fact]
    public void Test_OdfParallelScheduler_AppliesAndRestoresWorkerThreadPriority()
    {
        ThreadPriority? originalConfiguredPriority = OdfParallelScheduler.WorkerThreadPriority;
        ThreadPriority originalThreadPriority = Thread.CurrentThread.Priority;
        ThreadPriority targetPriority = originalThreadPriority == ThreadPriority.BelowNormal
            ? ThreadPriority.Normal
            : ThreadPriority.BelowNormal;

        try
        {
            OdfParallelScheduler.WorkerThreadPriority = targetPriority;

            ThreadPriority observedPriority = OdfParallelScheduler.RunWithConfiguredThreadPriority(
                static () => Thread.CurrentThread.Priority);

            Assert.Equal(targetPriority, observedPriority);
            Assert.Equal(originalThreadPriority, Thread.CurrentThread.Priority);
        }
        finally
        {
            OdfParallelScheduler.WorkerThreadPriority = originalConfiguredPriority;
            Thread.CurrentThread.Priority = originalThreadPriority;
        }
    }
}

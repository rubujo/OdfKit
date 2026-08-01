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
    public void TestOdfPackageMmfPreloadUsesReservedCpuConcurrency()
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
    public async Task TestOdfPackageMmfPreloadQueuesCoreXmlEntriesForParallelRandomAccess()
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

            Assert.Equal(4, package.LastMmfParallelPreloadEntryCountForTests);
            Assert.Equal(4, package.LastMmfParallelPreloadVisitedEntryCountForTests);
            Assert.Equal(
                OdfParallelScheduler.GetEffectiveConcurrency(),
                package.LastMmfParallelPreloadMaxDegreeForTests);
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
    /// 驗證非同步檔案路徑載入同樣可觀測 MMF 平行預讀計數，不受 ExecutionContext 邊界影響。
    /// </summary>
    [Fact]
    public async Task TestOdfPackageOpenAsyncMmfPreloadCountersRemainObservable()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"odfkit_mmf_preload_async_{Guid.NewGuid():N}.ods");
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

            using OdfPackage package = await OdfPackage.OpenAsync(
                tempFile,
                new OdfLoadOptions { AllowLazyLoading = true },
                TestContext.Current.CancellationToken);

            Assert.NotNull(package.MmfEntries);
            Assert.NotNull(package.PreloadTask);

            await package.PreloadTask!.WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(4, package.LastMmfParallelPreloadEntryCountForTests);
            Assert.Equal(4, package.LastMmfParallelPreloadVisitedEntryCountForTests);
            Assert.Equal(
                OdfParallelScheduler.GetEffectiveConcurrency(),
                package.LastMmfParallelPreloadMaxDegreeForTests);
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
    /// 驗證 PrefetchAsync 會等待 MMF entry 的實際載入完成，而非只等待排入背景通道。
    /// </summary>
    [Fact]
    public async Task TestOdfPackageEntryPrefetchAsyncWaitsForActualMmfLoad()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"odfkit_mmf_prefetch_completion_{Guid.NewGuid():N}.ods");
        byte[] payload = [1, 2, 3, 4];
        byte[] manifest = Encoding.UTF8.GetBytes("""
            <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0">
              <manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet" />
              <manifest:file-entry manifest:full-path="Pictures/image.bin" manifest:media-type="application/octet-stream" />
            </manifest:manifest>
            """);
        try
        {
            using (MemoryStream packageStream = CreateZipPackage(
                ("mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet")),
                ("META-INF/manifest.xml", manifest),
                ("Pictures/image.bin", payload)))
            {
                File.WriteAllBytes(tempFile, packageStream.ToArray());
            }

            using OdfPackage package = OdfPackage.Open(
                tempFile,
                new OdfLoadOptions { AllowLazyLoading = true });

            OdfPackageEntry entry = package.LoadCollaborators.Entries["Pictures/image.bin"];
            object loadLock = typeof(OdfPackageEntry)
                .GetField("_loadLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(entry)!;
            Task prefetchTask;
            Monitor.Enter(loadLock);
            try
            {
                entry.Prefetch();
                prefetchTask = entry.PrefetchAsync(TestContext.Current.CancellationToken);
                Assert.False(prefetchTask.IsCompleted);
            }
            finally
            {
                Monitor.Exit(loadLock);
            }

            await prefetchTask;
            Assert.Equal(payload, entry.GetCachedBytes());
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
    public void TestOdfParallelSchedulerAppliesAndRestoresWorkerThreadPriority()
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

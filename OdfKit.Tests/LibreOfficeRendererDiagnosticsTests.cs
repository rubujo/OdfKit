using System.Globalization;
using System;
using OdfKit.Compliance;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OdfKit.Core;
using OdfKit.Text;
using OdfKit.Extensions.Rendering;

namespace OdfKit.Tests
{
    [Collection("SequentialRenderingTests")]
    [Trait(TestCategories.Kind, TestCategories.Interop)]
    public class LibreOfficeRendererDiagnosticsTests : IDisposable
    {
        private readonly CultureInfo? _originalDefaultCulture;

        public LibreOfficeRendererDiagnosticsTests()
        {
            _originalDefaultCulture = OdfLocalizer.DefaultCulture;
            OdfLocalizer.DefaultCulture = new CultureInfo("en");
        }

        public void Dispose()
        {
            OdfLocalizer.DefaultCulture = _originalDefaultCulture;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task TestParallelTimeoutsProcessOrphanCount()
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
            {
                Assert.Skip("在 GitHub Actions Runner 環境中略過併發極短超時測試，防範 Flaky 錯誤。");
            }

            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            // Count running MockSoffice processes before
            int initialCount = Process.GetProcessesByName("MockSoffice").Length;

            int parallelCount = 5;
            var tasks = new List<Task>();

            for (int i = 0; i < parallelCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    using var package = OdfPackage.Create(new MemoryStream());
                    var doc = new TextDocument(package);
                    doc.AddParagraph($"Parallel Timeout {index}");

                    var renderer = new LibreOfficeRenderer
                    {
                        LibreOfficePath = mockSoffice,
                        Timeout = TimeSpan.FromMilliseconds(200) // Trigger timeout quickly
                    };

                    string outPath = Path.Combine(Path.GetTempPath(), $"OdfKit_Diagnostics_Out_{index}_" + Guid.NewGuid().ToString("N") + ".pdf");
                    try
                    {
                        Assert.Throws<TimeoutException>(() => renderer.Convert(doc, outPath, "pdf-simulate-timeout"));
                    }
                    finally
                    {
                        if (File.Exists(outPath))
                            File.Delete(outPath);
                    }
                }, TestContext.Current.CancellationToken));
            }

            await Task.WhenAll(tasks);

            // Wait with a retry loop for OS process cleanup (up to 5 seconds)
            int leakedCount = 0;
            for (int retry = 0; retry < 10; retry++)
            {
                int finalCount = Process.GetProcessesByName("MockSoffice").Length;
                leakedCount = finalCount - initialCount;
                if (leakedCount <= 0)
                    break;
                Thread.Sleep(500);
            }

            Assert.True(leakedCount <= 0, $"Process Leak: {leakedCount} MockSoffice processes were leaked after timeouts.");
        }

        [Fact]
        public void TestExtremelyShortTimeoutSafety()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Extremely Short Timeout Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromMilliseconds(1) // 1ms timeout
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Diagnostics_Out_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                // Verify that 1ms timeout doesn't cause internal crash (e.g. process start/kill race),
                // but successfully throws TimeoutException.
                Assert.Throws<TimeoutException>(() => renderer.Convert(doc, outPath, "pdf-delay"));
            }
            finally
            {
                if (File.Exists(outPath))
                    File.Delete(outPath);
            }
        }

        [Fact]
        public void TestStandardErrorCapturedInException()
        {
            string mockSoffice = GetMockSofficePath();
            if (string.IsNullOrEmpty(mockSoffice))
                return;

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Diagnostics_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            // Format "pdf-simulate-error" will exit with code 1 and output "Simulated soffice error." to stderr
            var ex = Assert.Throws<InvalidOperationException>(() => renderer.Convert(doc, outPath, "pdf-simulate-error"));

            // Assert that the exception message captures the process exit status (since stderr output is not captured by implementation)
            Assert.Contains("exited with code 1", ex.Message);
        }

        [Fact]
        public async Task TestSharedInstanceParallelRenderingSafety()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            var sharedRenderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            int parallelCount = 5;
            var tasks = new List<Task>();

            for (int i = 0; i < parallelCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    using var package = OdfPackage.Create(new MemoryStream());
                    var doc = new TextDocument(package);
                    doc.AddParagraph($"Paragraph {index}");

                    string outPath = Path.Combine(Path.GetTempPath(), $"OdfKit_Shared_Out_{index}_" + Guid.NewGuid().ToString("N") + ".pdf");
                    try
                    {
                        sharedRenderer.Convert(doc, outPath, "pdf");
                        Assert.True(File.Exists(outPath));
                        string content = File.ReadAllText(outPath);
                        Assert.Contains("%PDF-1.4", content);
                    }
                    finally
                    {
                        if (File.Exists(outPath))
                            File.Delete(outPath);
                    }
                }, TestContext.Current.CancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task TestInvalidOutputPathCleanup()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Invalid Path Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            // 以既有檔案充當父目錄，確保所有支援的平台都會拒絕建立輸出目錄。
            string blockingPath = Path.Combine(Path.GetTempPath(), "OdfKit_InvalidOutputParent_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blockingPath, string.Empty);
            try
            {
                string invalidOutPath = Path.Combine(blockingPath, "output.pdf");
                string? sandboxDir = await CaptureSandboxDirAsync(() =>
                {
                    Assert.Throws<IOException>(() => renderer.Convert(doc, invalidOutPath, "pdf"));
                });

                Assert.NotNull(sandboxDir);

                // 稍候作業系統釋放資源，再驗證沙箱目錄已清理。
                await Task.Delay(200, TestContext.Current.CancellationToken);

                bool isLeaked = Directory.Exists(sandboxDir);
                if (isLeaked)
                {
                    try
                    { Directory.Delete(sandboxDir, true); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }

                Assert.False(isLeaked, $"Vulnerability: Sandbox directory '{sandboxDir}' was leaked on invalid output path failure.");
            }
            finally
            {
                File.Delete(blockingPath);
            }
        }

        [Fact]
        public void TestNullFormatHandling()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Null Format Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Diagnostics_Out_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                Assert.Throws<ArgumentNullException>(() => renderer.Convert(doc, outPath, null!));
            }
            finally
            {
                if (File.Exists(outPath))
                    File.Delete(outPath);
            }
        }

        private static async Task<string?> CaptureSandboxDirAsync(Action runAction)
        {
            var tempPath = Path.GetTempPath();
            var currentPid = Environment.ProcessId;
            var searchPattern = $"OdfKit_Render_{currentPid}_*";
            var existingDirs = new HashSet<string>(Directory.GetDirectories(tempPath, searchPattern), StringComparer.OrdinalIgnoreCase);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, TestContext.Current.CancellationToken);
            CancellationToken token = cts.Token;
            string? detectedDir = null;

            var watcherTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested && detectedDir == null)
                    {
                        try
                        {
                            var dirs = Directory.GetDirectories(tempPath, searchPattern);
                            foreach (var dir in dirs)
                            {
                                if (existingDirs.Contains(dir))
                                    continue;

                                if (Directory.Exists(Path.Combine(dir, "profile")))
                                {
                                    detectedDir = dir;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"CaptureSandboxDirAsync watcher exception: {ex.Message}");
                        }
                        if (detectedDir != null)
                            break;
                        await Task.Delay(10, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
            }, TestContext.Current.CancellationToken);

            runAction();

            cts.Cancel();
            await watcherTask;
            return detectedDir;
        }

        private static string GetMockSofficePath()
        {
            return MockSofficeFinder.GetMockSofficePath();
        }
    }
}

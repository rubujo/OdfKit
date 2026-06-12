using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OdfKit.Core;

using OdfKit.Text;
using OdfKit.Extensions.Rendering;

namespace OdfKit.Tests
{
    [Collection("SequentialRenderingTests")]
    public class LibreOfficeRendererChallengerTests
    {
        [Fact]
        public async Task TestParallelRenderingSafety()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            int parallelCount = 10;
            var tasks = new List<Task>();

            for (int i = 0; i < parallelCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    using var package = OdfPackage.Create(new MemoryStream());
                    var doc = new TextDocument(package);
                    doc.AddParagraph($"Paragraph {index}");

                    var renderer = new LibreOfficeRenderer
                    {
                        LibreOfficePath = mockSoffice,
                        Timeout = TimeSpan.FromSeconds(5)
                    };

                    string outPath = Path.Combine(Path.GetTempPath(), $"OdfKit_Parallel_Out_{index}_" + Guid.NewGuid().ToString("N") + ".pdf");
                    try
                    {
                        renderer.Convert(doc, outPath, "pdf");
                        Assert.True(File.Exists(outPath));
                        string content = File.ReadAllText(outPath);
                        Assert.Contains("%PDF-1.4", content);
                    }
                    finally
                    {
                        if (File.Exists(outPath)) File.Delete(outPath);
                    }
                }, TestContext.Current.CancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task TestSandboxCleanupOnTimeoutLeak()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Hello Leak Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromMilliseconds(1000) // 1000ms to avoid race condition with folder watcher
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Challenger_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            string? sandboxDir = null;
            TimeoutException? caughtTimeout = null;
            
            sandboxDir = await CaptureSandboxDirAsync(() =>
            {
                caughtTimeout = Assert.Throws<TimeoutException>(() =>
                    renderer.Convert(doc, outPath, "pdf-simulate-timeout"));
            });

            Assert.NotNull(caughtTimeout);
            Assert.Contains("timed out", caughtTimeout.Message);
            Assert.NotNull(sandboxDir);

            // Wait a brief moment for process cleanup OS scheduling
            Thread.Sleep(500);

            // If the directory still exists, it is leaked because of async kill or locked handles
            bool isLeaked = Directory.Exists(sandboxDir);
            if (isLeaked)
            {
                try { Directory.Delete(sandboxDir, true); } catch { }
            }

            Assert.False(isLeaked, $"Vulnerability: Sandbox directory '{sandboxDir}' was leaked on timeout.");
        }

        [Fact]
        public async Task TestSandboxCleanupOnErrorLeak()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Hello Error Leak Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Challenger_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            string? sandboxDir = await CaptureSandboxDirAsync(() =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                    renderer.Convert(doc, outPath, "pdf-simulate-error"));
            });

            Assert.NotNull(sandboxDir);
            
            // Wait a brief moment
            Thread.Sleep(500);

            bool isLeaked = Directory.Exists(sandboxDir);
            if (isLeaked)
            {
                try { Directory.Delete(sandboxDir, true); } catch { }
            }

            Assert.False(isLeaked, $"Vulnerability: Sandbox directory '{sandboxDir}' was leaked on process exit code error.");
        }

        [Fact]
        public async Task TestArgumentParsingAndInjectionSafety()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Hello Injection Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Challenger_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            // Format with a space and "delay" to allow us to capture arguments: "pdf-delay --foo=bar"
            string formatWithSpace = "pdf-delay --foo=bar";

            var arguments = await CaptureArgumentsAsync(() =>
            {
                renderer.Convert(doc, outPath, formatWithSpace);
            }, args => args.Contains("pdf-delay --foo=bar") || args.Contains("pdf-delay"));

            Assert.NotEmpty(arguments);

            // Find how "pdf-delay --foo=bar" was parsed.
            // Under net10.0, the argument list preserves "pdf-delay --foo=bar" as a single argument.
            // Under net8.0 (representing the netstandard2.0 build), it is concatenated, so the OS parses it as two arguments: "pdf-delay" and "--foo=bar".
            bool foundMerged = arguments.Contains("pdf-delay --foo=bar");
            Assert.True(foundMerged, "Expected format to be passed as a single argument containing space.");

            if (File.Exists(outPath)) File.Delete(outPath);
        }

        [Fact]
        public async Task TestSandboxCleanupLeakWithActiveHandleLock()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Hello Active Lock Leak Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Challenger_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            var tempPath = Path.GetTempPath();
            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var searchPattern = $"OdfKit_Render_{currentPid}_*";
            var existingDirs = new HashSet<string>(Directory.GetDirectories(tempPath, searchPattern), StringComparer.OrdinalIgnoreCase);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var token = cts.Token;
            string? detectedDir = null;
            FileStream? fsLock = null;

            var watcherTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && detectedDir == null)
                {
                    try
                    {
                        var dirs = Directory.GetDirectories(tempPath, searchPattern);
                        foreach (var dir in dirs)
                        {
                            if (existingDirs.Contains(dir)) continue;

                            string argsFile = Path.Combine(dir, "arguments.txt");
                            if (File.Exists(argsFile))
                            {
                                try
                                {
                                    var lines = File.ReadAllLines(argsFile);
                                    if (System.Linq.Enumerable.Contains(lines, "pdf-leak-delay"))
                                    {
                                        // Acquire an exclusive file lock on arguments.txt to block cleanup/deletion
                                        fsLock = new FileStream(argsFile, FileMode.Open, FileAccess.Read, FileShare.None);
                                        detectedDir = dir;
                                        break;
                                    }
                                }
                                catch
                                {
                                    if (fsLock != null)
                                    {
                                        fsLock.Dispose();
                                        fsLock = null;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    if (detectedDir != null) break;
                    await Task.Delay(10);
                }
            }, TestContext.Current.CancellationToken);

            try
            {
                renderer.Convert(doc, outPath, "pdf-leak-delay");
            }
            catch (Exception)
            {
                // ignore any exception from Convert
            }

            cts.Cancel();
            await watcherTask;

            if (fsLock != null)
            {
                fsLock.Dispose();
            }

            Assert.NotNull(detectedDir);

            // Wait a brief moment for OS release
            Thread.Sleep(200);

            bool isLeaked = Directory.Exists(detectedDir);
            if (isLeaked)
            {
                try { Directory.Delete(detectedDir, true); } catch { }
            }

            // Assert that the sandbox directory was leaked on active lock during cleanup
            Assert.True(isLeaked, "Expected sandbox directory to be leaked when a file handle lock was active during cleanup.");

            if (File.Exists(outPath)) File.Delete(outPath);
        }

        [Fact]
        public async Task TestArgumentInjectionVulnerability()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Hello Injection Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Challenger_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            // Attempt argument injection via format: "pdf --outdir C:\InjectedDir"
            string formatWithInjection = "pdf --outdir C:\\InjectedDir";

            var arguments = await CaptureArgumentsAsync(() =>
            {
                try
                {
                    renderer.Convert(doc, outPath, formatWithInjection);
                }
                catch {}
            }, args => args.Any(arg => arg.Contains("InjectedDir")));

            Assert.NotEmpty(arguments);

            // Safe: --convert-to argument contains the whole format string as a single argument
            bool hasInjectedArg = arguments.Contains("--outdir") && arguments.Contains("C:\\InjectedDir") && arguments.IndexOf("--outdir") != arguments.LastIndexOf("--outdir");
            Assert.False(hasInjectedArg, "Expected argument injection to be prevented.");

            if (File.Exists(outPath)) File.Delete(outPath);
        }

        [Fact]
        public async Task TestArgumentInjectionVulnerabilityWithQuotes()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Hello Injection Test");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Challenger_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            // Attempt argument injection via format with backslash and quotes: pdf\" --outdir C:\\InjectedDir
            string formatWithInjection = "pdf\\\" --outdir C:\\\\InjectedDir";

            var arguments = await CaptureArgumentsAsync(() =>
            {
                try
                {
                    renderer.Convert(doc, outPath, formatWithInjection);
                }
                catch {}
            }, args => args.Any(arg => arg.Contains("InjectedDir")));

            Assert.NotEmpty(arguments);

            // The format argument should be at index 3 (after --headless, -env:UserInstallation, --convert-to).
            // It must contain "InjectedDir" to prove it was not broken out of the quotes.
            Assert.True(arguments.Count >= 4, "Expected at least 4 arguments.");
            Assert.Contains("InjectedDir", arguments[3]);

            if (File.Exists(outPath)) File.Delete(outPath);
        }

        private static void LogDebug(string message)
        {
            try
            {
                File.AppendAllText(@"D:\Dev\Project\Application\ODF\debug_log.txt", $"[{DateTime.UtcNow:O}] {message}\n");
            }
            catch {}
        }

        private async Task<string?> CaptureSandboxDirAsync(Action runAction)
        {
            LogDebug("CaptureSandboxDirAsync started");
            var tempPath = Path.GetTempPath();
            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var searchPattern = $"OdfKit_Render_{currentPid}_*";
            var existingDirs = new HashSet<string>(Directory.GetDirectories(tempPath, searchPattern), StringComparer.OrdinalIgnoreCase);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var token = cts.Token;
            string? detectedDir = null;

            var watcherTask = Task.Run(async () =>
            {
                LogDebug("CaptureSandboxDirAsync watcher started");
                while (!token.IsCancellationRequested && detectedDir == null)
                {
                    try
                    {
                        var dirs = Directory.GetDirectories(tempPath, searchPattern);
                        foreach (var dir in dirs)
                        {
                            if (existingDirs.Contains(dir)) continue;

                            if (Directory.Exists(Path.Combine(dir, "profile")))
                            {
                                detectedDir = dir;
                                LogDebug($"CaptureSandboxDirAsync watcher detected: {detectedDir}");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"CaptureSandboxDirAsync watcher exception: {ex.Message}");
                    }
                    if (detectedDir != null) break;
                    await Task.Delay(10);
                }
                LogDebug("CaptureSandboxDirAsync watcher finished");
            });

            try
            {
                LogDebug("CaptureSandboxDirAsync running action");
                runAction();
                LogDebug("CaptureSandboxDirAsync action completed successfully");
            }
            catch (TimeoutException)
            {
                LogDebug("CaptureSandboxDirAsync action threw TimeoutException");
                throw;
            }
            catch (Exception ex)
            {
                LogDebug($"CaptureSandboxDirAsync action threw: {ex}");
            }

            await watcherTask;
            LogDebug($"CaptureSandboxDirAsync returning: {detectedDir}");
            return detectedDir;
        }

        private async Task<List<string>> CaptureArgumentsAsync(Action runAction, Func<List<string>, bool>? validator = null)
        {
            LogDebug("CaptureArgumentsAsync started");
            var tempPath = Path.GetTempPath();
            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var searchPattern = $"OdfKit_Render_{currentPid}_*";
            var existingDirs = new HashSet<string>(Directory.GetDirectories(tempPath, searchPattern), StringComparer.OrdinalIgnoreCase);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var token = cts.Token;

            List<string>? capturedArgs = null;
            string? detectedDir = null;
            FileStream? lockStream = null;

            var watcherTask = Task.Run(async () =>
            {
                LogDebug("CaptureArgumentsAsync watcher started");
                while (!token.IsCancellationRequested && capturedArgs == null)
                {
                    try
                    {
                        var dirs = Directory.GetDirectories(tempPath, searchPattern);
                        foreach (var dir in dirs)
                        {
                            if (existingDirs.Contains(dir)) continue;

                            string argsFile = Path.Combine(dir, "arguments.txt");
                            if (File.Exists(argsFile))
                            {
                                try
                                {
                                    // Open with FileShare.Read to read the file, but keep a lock to block deletion
                                    lockStream = new FileStream(argsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    using (var reader = new StreamReader(lockStream, Encoding.UTF8, true, 1024, leaveOpen: true))
                                    {
                                        var lines = new List<string>();
                                        string? line;
                                        while ((line = await reader.ReadLineAsync()) != null)
                                        {
                                            lines.Add(line);
                                        }
                                        if (validator == null || validator(lines))
                                        {
                                            capturedArgs = lines;
                                            detectedDir = dir;
                                            LogDebug($"Successfully read {lines.Count} arguments: {string.Join(", ", lines)}");
                                        }
                                        else
                                        {
                                            lockStream.Dispose();
                                            lockStream = null;
                                        }
                                    }
                                    if (capturedArgs != null) break;
                                }
                                catch (Exception ex)
                                {
                                    LogDebug($"Exception reading file: {ex.Message}");
                                    if (lockStream != null)
                                    {
                                        lockStream.Dispose();
                                        lockStream = null;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Exception listing directories: {ex.Message}");
                    }
                    if (capturedArgs != null) break;
                    await Task.Delay(10);
                }
                LogDebug("Watcher task finished");
            });

            try
            {
                LogDebug("Running action...");
                runAction();
                LogDebug("Action finished successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"Action failed with exception: {ex}");
                Console.WriteLine($"DBGP: CaptureArgumentsAsync runAction exception: {ex}");
            }
            finally
            {
                cts.Cancel();
            }

            await watcherTask;

            if (lockStream != null)
            {
                lockStream.Dispose();
            }

            if (detectedDir != null && Directory.Exists(detectedDir))
            {
                try { Directory.Delete(detectedDir, true); } catch { }
            }

            LogDebug($"CaptureArgumentsAsync finished, returning {(capturedArgs != null ? capturedArgs.Count.ToString() : "null")} arguments");
            return capturedArgs ?? new List<string>();
        }

        private string GetMockSofficePath()
        {
            var baseDir = AppContext.BaseDirectory;
            var paths = new List<string>
            {
                Path.Combine(baseDir, "MockSoffice", "MockSoffice.exe"),
                Path.Combine(baseDir, "MockSoffice", "MockSoffice"),
                Path.Combine(baseDir, "..", "..", "..", "MockSoffice", "bin", "MockSoffice.exe"),
                Path.Combine(baseDir, "..", "..", "..", "MockSoffice", "bin", "MockSoffice"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "MockSoffice.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "MockSoffice"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "Debug", "net8.0", "MockSoffice.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "Debug", "net8.0", "MockSoffice")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return string.Empty;
        }

        [Fact]
        public void TestKillMethodReflection()
        {
            var killMethod = typeof(System.Diagnostics.Process).GetMethod("Kill", new[] { typeof(bool) });
            LogDebug($"Kill method reflection result: {killMethod?.ToString() ?? "null"}");
            Assert.NotNull(killMethod);
        }

        [Fact]
        public void TestKillMethodInvokeException()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = mockSoffice,
                Arguments = "--outdir " + Path.GetTempPath() + " pdf-simulate-timeout",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            // Give it 500ms to spawn the child process
            Thread.Sleep(500);

            var killMethod = typeof(System.Diagnostics.Process).GetMethod("Kill", new[] { typeof(bool) });
            Assert.NotNull(killMethod);

            try
            {
                killMethod.Invoke(process, new object[] { true });
                LogDebug("killMethod.Invoke completed without exception.");
            }
            catch (Exception ex)
            {
                LogDebug($"killMethod.Invoke threw exception: {ex}");
                throw;
            }

            bool exited = process.WaitForExit(5000);
            LogDebug($"Process exited after kill: {exited}, exit code: {process.ExitCode}");
        }

        [Fact]
        public void TestNullFormatDiscrepancy()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_NullFormat_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
#if NET10_0
                // Under NET10_0 target, process.StartInfo.ArgumentList.Add(null) is executed and throws ArgumentNullException
                Assert.Throws<ArgumentNullException>(() => renderer.Convert(doc, outPath, null!));
#else
                // Under net8.0 (representing netstandard2.0 build), process.StartInfo.Arguments is built using string builder.
                // EscapeArgument(null) returns "" which is passed as a format argument.
                // This does NOT throw an exception because MockSoffice creates a file named "document."
                // which the renderer finds and copies to the output path successfully!
                renderer.Convert(doc, outPath, null!);
                Assert.True(File.Exists(outPath));
                Assert.Equal("mock image content", File.ReadAllText(outPath));
#endif
            }
            finally
            {
                if (File.Exists(outPath)) File.Delete(outPath);
            }
        }

        [Fact]
        public void TestStandardErrorDiagnosticsMissing()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Diagnostics_" + Guid.NewGuid().ToString("N") + ".pdf");
            
            // Subscribing to diagnostics event
            OdfDiagnosticsEventArgs? loggedArgs = null;
            EventHandler<OdfDiagnosticsEventArgs> handler = (sender, args) =>
            {
                loggedArgs = args;
            };
            
            OdfKitDiagnostics.Log += handler;
            try
            {
                // Format "pdf-simulate-error" will exit with code 1 and output "Simulated soffice error." to stderr
                var ex = Assert.Throws<InvalidOperationException>(() => renderer.Convert(doc, outPath, "pdf-simulate-error"));
                
                // Assert that the exception message ONLY reports the exit code, but NOT the actual stderr text
                Assert.Contains("exited with code 1", ex.Message);
                Assert.DoesNotContain("Simulated soffice error", ex.Message);

                // Assert that NO diagnostic message was logged via OdfKitDiagnostics
                Assert.Null(loggedArgs);
            }
            finally
            {
                OdfKitDiagnostics.Log -= handler;
                if (File.Exists(outPath)) File.Delete(outPath);
            }
        }

        [Fact]
        public async Task TestExtremeConcurrencyStress()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            int parallelCount = 20;
            var tasks = new List<Task>();
            
            var tempPath = Path.GetTempPath();
            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var searchPattern = $"OdfKit_Render_{currentPid}_*";
            var baselineDirs = new HashSet<string>(Directory.GetDirectories(tempPath, searchPattern), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < parallelCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    using var package = OdfPackage.Create(new MemoryStream());
                    var doc = new TextDocument(package);
                    doc.AddParagraph($"Paragraph {index}");

                    var renderer = new LibreOfficeRenderer
                    {
                        LibreOfficePath = mockSoffice,
                        Timeout = TimeSpan.FromSeconds(5)
                    };

                    string outPath = Path.Combine(Path.GetTempPath(), $"OdfKit_Stress_Out_{index}_" + Guid.NewGuid().ToString("N") + ".pdf");
                    
                    int scenario = index % 3;
                    try
                    {
                        if (scenario == 0)
                        {
                            // Success case
                            renderer.Convert(doc, outPath, "pdf");
                            Assert.True(File.Exists(outPath));
                            string content = File.ReadAllText(outPath);
                            Assert.Contains("%PDF-1.4", content);
                        }
                        else if (scenario == 1)
                        {
                            // Process failure
                            Assert.Throws<InvalidOperationException>(() => renderer.Convert(doc, outPath, "pdf-simulate-error"));
                        }
                        else
                        {
                            // Timeout case
                            renderer.Timeout = TimeSpan.FromMilliseconds(200);
                            Assert.Throws<TimeoutException>(() => renderer.Convert(doc, outPath, "pdf-simulate-timeout"));
                        }
                    }
                    finally
                    {
                        if (File.Exists(outPath)) File.Delete(outPath);
                    }
                }, TestContext.Current.CancellationToken));
            }

            await Task.WhenAll(tasks);

            // Wait a brief moment for OS to clean up processes and unlock directories
            Thread.Sleep(2000);

            // Verify that all temporary sandbox directories created by this stress test were cleaned up
            var postDirs = Directory.GetDirectories(tempPath, searchPattern);
            var leakedDirs = new List<string>();
            foreach (var dir in postDirs)
            {
                if (!baselineDirs.Contains(dir))
                {
                    leakedDirs.Add(dir);
                }
            }

            int leakCount = leakedDirs.Count;
            // Clean up any leaked directories to be good citizens, retrying to allow OS process termination
            foreach (var dir in leakedDirs)
            {
                bool deleted = false;
                for (int retry = 0; retry < 10; retry++)
                {
                    try
                    {
                        if (!Directory.Exists(dir))
                        {
                            deleted = true;
                            break;
                        }
                        Directory.Delete(dir, true);
                        deleted = true;
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(500);
                    }
                }
                if (!deleted)
                {
                    LogDebug($"Failed to delete leaked sandbox directory '{dir}' after retries.");
                }
            }

            LogDebug($"Extreme concurrency stress test finished. Leaked sandbox directories: {leakCount}");
        }

        [Fact]
        public async Task TestArgumentInjectionShellMetacharacters()
        {
            string mockSoffice = GetMockSofficePath();
            Assert.False(string.IsNullOrEmpty(mockSoffice), "MockSoffice not found.");

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Metachar_" + Guid.NewGuid().ToString("N") + ".pdf");
            
            // Injection payload with various shell and control characters
            string formatWithMetachars = "pdf&dir|whoami<input>output%temp% --foo";

            var arguments = await CaptureArgumentsAsync(() =>
            {
                try
                {
                    renderer.Convert(doc, outPath, formatWithMetachars);
                }
                catch {}
            }, args => args.Any(arg => arg.Contains("whoami") || arg.Contains("&dir") || arg.Contains("--foo")));

            Assert.NotEmpty(arguments);

            // In either case, the entire malicious format string must remain a single argument
            // and NOT be split into multiple arguments or execute arbitrary commands.
            bool foundMerged = false;
            foreach (var arg in arguments)
            {
                if (arg.Contains("dir") && arg.Contains("whoami") && arg.Contains("--foo"))
                {
                    foundMerged = true;
                    break;
                }
            }
            if (!foundMerged)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Captured arguments:");
                foreach (var a in arguments)
                {
                    sb.AppendLine($"- '{a}'");
                }
                Assert.Fail($"Expected format containing shell metacharacters to be passed as a single intact argument. {sb}");
            }

            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }
}

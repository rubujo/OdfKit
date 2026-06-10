using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;

namespace OdfKit.Extensions.Rendering
{
    public static class LibreOfficeRendererExtensions
    {
        public static void ConvertToPdf(this OdfDocument document, string outputPath)
        {
            var renderer = new LibreOfficeRenderer();
            renderer.Convert(document, outputPath, "pdf");
        }

        public static void ConvertToImage(this OdfDocument document, string outputPath, string format = "png")
        {
            var renderer = new LibreOfficeRenderer();
            renderer.Convert(document, outputPath, format);
        }
    }

    public class LibreOfficeRenderer
    {
        public string LibreOfficePath { get; set; } = DefaultLibreOfficePath();
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        public void Convert(OdfDocument document, string outputPath, string format)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            string? sandboxDir = null;
            try
            {
                // Create a unique sandbox directory inside the OS temporary path
                string tempSandbox = Path.Combine(Path.GetTempPath(), "OdfKit_Render_" + Guid.NewGuid().ToString("N"));
                if (!Directory.Exists(tempSandbox))
                {
                    Directory.CreateDirectory(tempSandbox);
                }
                sandboxDir = tempSandbox;

                // Define profile subdirectory for isolated LibreOffice configuration environment
                string profileDir = Path.Combine(sandboxDir, "profile");
                if (!Directory.Exists(profileDir))
                {
                    Directory.CreateDirectory(profileDir);
                }

                // Write ODF bytes to input sandbox file
                string inputFileName = "document.odt"; // safe fallback name
                if (document is PresentationDocument) inputFileName = "document.odp";
                else if (document is SpreadsheetDocument) inputFileName = "document.ods";
                
                string inputFilePath = Path.Combine(sandboxDir, inputFileName);
                File.WriteAllBytes(inputFilePath, document.SaveToBytes());
                // Force soffice to launch in a completely isolated profile, allowing safe parallel processing.
                // Use standard forward slashes for the profile URI path.
                string profileUri = "file:///" + profileDir.Replace('\\', '/');
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = LibreOfficePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

#if NET10_0
                startInfo.ArgumentList.Add("--headless");
                startInfo.ArgumentList.Add($"-env:UserInstallation={profileUri}");
                startInfo.ArgumentList.Add("--convert-to");
                startInfo.ArgumentList.Add(format);
                startInfo.ArgumentList.Add("--outdir");
                startInfo.ArgumentList.Add(sandboxDir);
                startInfo.ArgumentList.Add(inputFilePath);
#else
                var args = new StringBuilder();
                args.Append("--headless");
                args.Append($" {EscapeArgument($"-env:UserInstallation={profileUri}")}");
                args.Append(" --convert-to");
                args.Append($" {EscapeArgument(format)}");
                args.Append(" --outdir");
                args.Append($" {EscapeArgument(sandboxDir)}");
                args.Append($" {EscapeArgument(inputFilePath)}");
                startInfo.Arguments = args.ToString();
#endif

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    // Read standard outputs to prevent output streams buffer lockups
                    var stdOutTask = process.StandardOutput.ReadToEndAsync();
                    var stdErrTask = process.StandardError.ReadToEndAsync();

                    bool exited = process.WaitForExit((int)Timeout.TotalMilliseconds);
                    if (!exited)
                    {
                        try
                        {
                            var killMethod = typeof(Process).GetMethod("Kill", new[] { typeof(bool) });
                            if (killMethod != null)
                            {
                                killMethod.Invoke(process, new object[] { true });
                                process.WaitForExit(5000);
                            }
                            else
                            {
                                process.Kill();
                                process.WaitForExit(5000);
                            }
                        }
                        catch { }
                        throw new TimeoutException("LibreOffice conversion process timed out.");
                    }

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"LibreOffice exited with code {process.ExitCode}.");
                    }
                }

                string expectedOutName = Path.GetFileNameWithoutExtension(inputFileName) + "." + format;
                string convertedFilePath = Path.Combine(sandboxDir, expectedOutName);

                if (!File.Exists(convertedFilePath))
                {
                    throw new FileNotFoundException("LibreOffice failed to generate target converted file.");
                }

                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
                
                string? outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                File.Move(convertedFilePath, outputPath);
            }
            finally
            {
                SafeCleanDirectory(sandboxDir);
            }
        }

        private static string DefaultLibreOfficePath()
        {
#if NET10_0
            if (OperatingSystem.IsWindows())
            {
                var paths = new[]
                {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
                };
                foreach (var path in paths)
                {
                    if (File.Exists(path)) return path;
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var path = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
                if (File.Exists(path)) return path;
            }
            else if (OperatingSystem.IsLinux())
            {
                var paths = new[]
                {
                    "/usr/bin/soffice",
                    "/usr/bin/libreoffice"
                };
                foreach (var path in paths)
                {
                    if (File.Exists(path)) return path;
                }
            }
#else
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                var paths = new[]
                {
                    @"C:\Program Files\LibreOffice\program\soffice.exe",
                    @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
                };
                foreach (var path in paths)
                {
                    if (File.Exists(path)) return path;
                }
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                var path = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
                if (File.Exists(path)) return path;
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                var paths = new[]
                {
                    "/usr/bin/soffice",
                    "/usr/bin/libreoffice"
                };
                foreach (var path in paths)
                {
                    if (File.Exists(path)) return path;
                }
            }
#endif
            return "soffice"; // Fallback to PATH resolution
        }

        private string EscapePath(string path)
        {
            return path.Replace("\"", "\\\"");
        }

        private static string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            bool needsQuotes = false;
            foreach (char c in arg)
            {
                if (char.IsWhiteSpace(c) || c == '\"')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
            {
                return arg;
            }

            var sb = new StringBuilder();
            sb.Append('\"');
            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (c == '\\')
                {
                    int backslashCount = 0;
                    while (i < arg.Length && arg[i] == '\\')
                    {
                        backslashCount++;
                        i++;
                    }

                    if (i == arg.Length)
                    {
                        sb.Append('\\', backslashCount * 2);
                        break;
                    }
                    else if (arg[i] == '\"')
                    {
                        sb.Append('\\', backslashCount * 2 + 1);
                        sb.Append('\"');
                    }
                    else
                    {
                        sb.Append('\\', backslashCount);
                        i--;
                    }
                }
                else if (c == '\"')
                {
                    sb.Append("\\\"");
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('\"');
            return sb.ToString();
        }

        private void SafeCleanDirectory(string? dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
            {
                return;
            }

            int attempts = 5;
            for (int i = 1; i <= attempts; i++)
            {
                try
                {
                    Directory.Delete(dirPath, true);
                    return;
                }
                catch (Exception ex)
                {
                    if (i == attempts)
                    {
                        OdfKitDiagnostics.Warn($"Failed to safely delete sandbox directory '{dirPath}' after {attempts} attempts: {ex.Message}");
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }
    }
}

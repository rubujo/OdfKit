using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;

namespace OdfKit.Extensions.Rendering;

/// <summary>
/// 提供將 ODF 文件轉檔與轉譯為其他格式的擴充方法。
/// </summary>
public static class LibreOfficeRendererExtensions
{
    /// <summary>
    /// 將指定文件轉換為 PDF 格式。
    /// </summary>
    /// <param name="document">要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">輸出 PDF 檔案的目標路徑</param>
    public static void ConvertToPdf(this OdfDocument document, string outputPath)
    {
        var renderer = new LibreOfficeRenderer();
        renderer.Convert(document, outputPath, "pdf");
    }

    /// <summary>
    /// 將指定文件轉換為指定的圖片格式。
    /// </summary>
    /// <param name="document">要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">輸出圖片檔案的目標路徑</param>
    /// <param name="format">輸出圖片的格式，預設為 png</param>
    public static void ConvertToImage(this OdfDocument document, string outputPath, string format = "png")
    {
        var renderer = new LibreOfficeRenderer();
        renderer.Convert(document, outputPath, format);
    }
}

/// <summary>
/// 使用系統安裝的 LibreOffice 執行檔進行背景文件格式轉換的渲染器。
/// </summary>
public class LibreOfficeRenderer
{
    /// <summary>
    /// 取得或設定 LibreOffice 的執行檔路徑。
    /// </summary>
    public string LibreOfficePath { get; set; } = DefaultLibreOfficePath();

    /// <summary>
    /// 取得或設定轉檔作業的超時時間限制。預設為 60 秒。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 使用 LibreOffice 將 ODF 文件轉換為指定的目標格式並輸出。
    /// </summary>
    /// <param name="document">要轉檔的 OdfDocument 來源文件</param>
    /// <param name="outputPath">輸出檔案的目標路徑</param>
    /// <param name="format">要轉換的目標格式，例如 pdf</param>
    /// <exception cref="ArgumentNullException">當來源文件或輸出路徑為 null 時擲出</exception>
    /// <exception cref="TimeoutException">當 LibreOffice 轉檔執行程序超時未回應時擲出</exception>
    /// <exception cref="InvalidOperationException">當 LibreOffice 進程結束但傳回非零的錯誤碼時擲出</exception>
    /// <exception cref="FileNotFoundException">當轉檔完成後找不到預期的目標檔案時擲出</exception>
    public void Convert(OdfDocument document, string outputPath, string format)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        if (string.IsNullOrEmpty(outputPath)) throw new ArgumentNullException(nameof(outputPath));
        if (format is null) throw new ArgumentNullException(nameof(format));

        string? sandboxDir = null;
        try
        {
            // Create a unique sandbox directory inside the OS temporary path, isolated by process ID
            string tempSandbox = Path.Combine(Path.GetTempPath(), "OdfKit_Render_" + Process.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N"));
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
                        var killMethod = typeof(Process).GetMethod("Kill", [typeof(bool)]);
                        if (killMethod is not null)
                        {
                            killMethod.Invoke(process, [true]);
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
            string[] paths = [
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            ];
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
            string[] paths = [
                "/usr/bin/soffice",
                "/usr/bin/libreoffice"
            ];
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
        }
#else
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            string[] paths = [
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            ];
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
            string[] paths = [
                "/usr/bin/soffice",
                "/usr/bin/libreoffice"
            ];
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

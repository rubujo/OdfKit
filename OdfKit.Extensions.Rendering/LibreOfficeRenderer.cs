using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
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
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentNullException(nameof(outputPath));
        if (format is null)
            throw new ArgumentNullException(nameof(format));

        string tempSandbox = Path.Combine(Path.GetTempPath(), "OdfKit_RenderDoc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSandbox);
        try
        {
            string inputFileName = "document.odt";
            if (document is PresentationDocument)
                inputFileName = "document.odp";
            else if (document is SpreadsheetDocument)
                inputFileName = "document.ods";

            string inputFilePath = Path.Combine(tempSandbox, inputFileName);
            File.WriteAllBytes(inputFilePath, document.SaveToBytes());

            ConvertFile(inputFilePath, outputPath, format);
        }
        finally
        {
            SafeCleanDirectory(tempSandbox);
        }
    }

    /// <summary>
    /// 使用系統安裝的 LibreOffice 將指定的實體檔案轉換為目標格式並輸出。
    /// </summary>
    /// <param name="inputFilePath">輸入檔案的實體絕對路徑。</param>
    /// <param name="outputPath">輸出檔案的目標絕對路徑。</param>
    /// <param name="format">要轉換的目標格式（例如 <c>pdf</c>）。</param>
    public void ConvertFile(string inputFilePath, string outputPath, string format)
    {
        ConvertFileAsync(inputFilePath, outputPath, format, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 使用系統安裝的 LibreOffice 將指定的實體檔案非同步轉換為目標格式並輸出。
    /// </summary>
    /// <param name="inputFilePath">輸入檔案的實體絕對路徑。</param>
    /// <param name="outputPath">輸出檔案的目標絕對路徑。</param>
    /// <param name="format">要轉換的目標格式（例如 <c>pdf</c>）。</param>
    /// <param name="cancellationToken">用於取消轉檔作業的權杖。</param>
    /// <returns>代表非同步轉檔作業的工作。</returns>
    /// <exception cref="ArgumentNullException">當輸入或輸出路徑為 null 或空字串時擲出。</exception>
    /// <exception cref="TimeoutException">當 LibreOffice 轉檔執行程序超時未回應時擲出。</exception>
    /// <exception cref="InvalidOperationException">當 LibreOffice 進程結束但傳回非零的錯誤碼時擲出。</exception>
    /// <exception cref="FileNotFoundException">當轉檔完成後找不到預期的目標檔案時擲出。</exception>
    /// <exception cref="OperationCanceledException">當作業因 <paramref name="cancellationToken"/> 取消時擲出。</exception>
    public async Task ConvertFileAsync(
        string inputFilePath,
        string outputPath,
        string format,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(inputFilePath))
            throw new ArgumentNullException(nameof(inputFilePath));
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentNullException(nameof(outputPath));
        if (string.IsNullOrEmpty(format))
            throw new ArgumentNullException(nameof(format));

        cancellationToken.ThrowIfCancellationRequested();

        string? sandboxDir = null;
        try
        {
            // 在系統臨時路徑中建立一個依行程 ID 隔離的唯一沙盒目錄
            string tempSandbox = Path.Combine(Path.GetTempPath(), "OdfKit_Render_" + Process.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N"));
            if (!Directory.Exists(tempSandbox))
            {
                Directory.CreateDirectory(tempSandbox);
            }
            sandboxDir = tempSandbox;

            string profileDir = Path.Combine(sandboxDir, "profile");
            if (!Directory.Exists(profileDir))
            {
                Directory.CreateDirectory(profileDir);
            }

            // 複製輸入檔案至沙盒中以防進程讀取衝突
            string sandboxInputPath = Path.Combine(sandboxDir, Path.GetFileName(inputFilePath));
            File.Copy(inputFilePath, sandboxInputPath, true);

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
            startInfo.ArgumentList.Add(sandboxInputPath);
#else
            var args = new StringBuilder();
            args.Append("--headless");
            args.Append($" {EscapeArgument($"-env:UserInstallation={profileUri}")}");
            args.Append(" --convert-to");
            args.Append($" {EscapeArgument(format)}");
            args.Append(" --outdir");
            args.Append($" {EscapeArgument(sandboxDir)}");
            args.Append($" {EscapeArgument(sandboxInputPath)}");
            startInfo.Arguments = args.ToString();
#endif

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

                bool exited = await WaitForProcessExitAsync(process, (int)Timeout.TotalMilliseconds, cancellationToken)
                    .ConfigureAwait(false);
                if (!exited)
                {
                    TryKillLibreOfficeProcess(process);
                    throw new TimeoutException("LibreOffice conversion process timed out.");
                }

                _ = await stdOutTask.ConfigureAwait(false);
                _ = await stdErrTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"LibreOffice exited with code {process.ExitCode}.");
                }
            }

            string expectedOutName = Path.GetFileNameWithoutExtension(sandboxInputPath) + "." + format;
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

    private static async Task<bool> WaitForProcessExitAsync(
        Process process,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMilliseconds);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillLibreOfficeProcess(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
#else
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                return false;

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return true;
#endif
    }

    private static void TryKillLibreOfficeProcess(Process process)
    {
        try
        {
            if (process.HasExited)
                return;

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
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"LibreOffice 逾時後終止程序失敗：{ex.Message}", ex);
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
                if (File.Exists(path))
                    return path;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var path = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
            if (File.Exists(path))
                return path;
        }
        else if (OperatingSystem.IsLinux())
        {
            string[] paths = [
                "/usr/bin/soffice",
                "/usr/bin/libreoffice"
            ];
            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
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

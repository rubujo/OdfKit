using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Extensions.Rendering;

/// <summary>
/// Defines common LibreOffice <c>--convert-to</c> output format names.
/// 提供 LibreOffice <c>--convert-to</c> 常用輸出格式常數。
/// </summary>
public static class LibreOfficeConversionFormats
{
    /// <summary>
    /// Portable Document Format
    /// </summary>
    public const string Pdf = "pdf";

    /// <summary>
    /// Rich Text Format
    /// </summary>
    public const string Rtf = "rtf";

    /// <summary>
    /// HTML 文件
    /// </summary>
    public const string Html = "html";

    /// <summary>
    /// Markdown 文件
    /// </summary>
    public const string Markdown = "md";

    /// <summary>
    /// Microsoft Word Open XML 文件
    /// </summary>
    public const string Docx = "docx";

    /// <summary>
    /// Microsoft Excel Open XML 活頁簿
    /// </summary>
    public const string Xlsx = "xlsx";

    /// <summary>
    /// Microsoft PowerPoint Open XML 簡報
    /// </summary>
    public const string Pptx = "pptx";

    /// <summary>
    /// Creates a renderer configured for CSV conversion.
    /// 逗號分隔值文字檔
    /// </summary>
    public const string Csv = "csv";

    /// <summary>
    /// Scalable Vector Graphics
    /// </summary>
    public const string Svg = "svg";

    /// <summary>
    /// PNG 點陣圖片
    /// </summary>
    public const string Png = "png";

    /// <summary>
    /// JPEG 點陣圖片
    /// </summary>
    public const string Jpeg = "jpg";
}

/// <summary>
/// Adds convenience methods for converting ODF documents through LibreOffice.
/// 提供將 ODF 文件轉檔與轉譯為其他格式的擴充方法。
/// </summary>
public static class LibreOfficeRendererExtensions
{
    /// <summary>
    /// Converts the document to a PDF file.
    /// 將指定文件轉換為 PDF 格式。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出 PDF 檔案的目標路徑</param>
    public static void ConvertToPdf(this OdfDocument document, string outputPath)
    {
        document.ConvertToLibreOfficeFormat(outputPath, LibreOfficeConversionFormats.Pdf);
    }

    /// <summary>
    /// Converts the document to PDF with the specified renderer.
    /// 使用指定的 LibreOffice renderer 將文件轉換為 PDF 格式。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出 PDF 檔案的目標路徑</param>
    /// <param name="renderer">The numeric value. / 要使用的 LibreOffice renderer</param>
    public static void ConvertToPdf(this OdfDocument document, string outputPath, LibreOfficeRenderer renderer)
    {
        document.ConvertToLibreOfficeFormat(outputPath, LibreOfficeConversionFormats.Pdf, renderer);
    }

    /// <summary>
    /// Converts the document to PDF asynchronously.
    /// 非同步將指定文件轉換為 PDF 格式。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出 PDF 檔案的目標路徑</param>
    /// <param name="cancellationToken">The cancellation token. / 用於取消轉檔作業的權杖</param>
    /// <returns>The result. / 代表非同步轉檔作業的工作</returns>
    public static Task ConvertToPdfAsync(
        this OdfDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        return document.ConvertToLibreOfficeFormatAsync(
            outputPath,
            LibreOfficeConversionFormats.Pdf,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Converts the document to an image file.
    /// 將指定文件轉換為指定的圖片格式。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出圖片檔案的目標路徑</param>
    /// <param name="format">The name or identifier. / 輸出圖片的格式，預設為 png</param>
    public static void ConvertToImage(this OdfDocument document, string outputPath, string format = "png")
    {
        document.ConvertToLibreOfficeFormat(outputPath, format);
    }

    /// <summary>
    /// Converts the document to any LibreOffice-supported output format.
    /// 使用 LibreOffice 將指定文件轉換為任意 <c>--convert-to</c> 目標格式。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出檔案的目標路徑</param>
    /// <param name="format">The name or identifier. / LibreOffice 目標格式，例如 <c>pdf</c>、<c>docx</c> 或 <c>html</c></param>
    /// <param name="renderer">The numeric value. / 可選用的自訂 renderer；未提供時使用預設 renderer</param>
    public static void ConvertToLibreOfficeFormat(
        this OdfDocument document,
        string outputPath,
        string format,
        LibreOfficeRenderer? renderer = null)
    {
        (renderer ?? new LibreOfficeRenderer()).Convert(document, outputPath, format);
    }

    /// <summary>
    /// Converts the document to any LibreOffice-supported output format asynchronously.
    /// 非同步使用 LibreOffice 將指定文件轉換為任意 <c>--convert-to</c> 目標格式。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉換的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出檔案的目標路徑</param>
    /// <param name="format">The name or identifier. / LibreOffice 目標格式，例如 <c>pdf</c>、<c>docx</c> 或 <c>html</c></param>
    /// <param name="renderer">The numeric value. / 可選用的自訂 renderer；未提供時使用預設 renderer</param>
    /// <param name="cancellationToken">The cancellation token. / 用於取消轉檔作業的權杖</param>
    /// <returns>The result. / 代表非同步轉檔作業的工作</returns>
    public static Task ConvertToLibreOfficeFormatAsync(
        this OdfDocument document,
        string outputPath,
        string format,
        LibreOfficeRenderer? renderer = null,
        CancellationToken cancellationToken = default)
    {
        return (renderer ?? new LibreOfficeRenderer()).ConvertAsync(document, outputPath, format, cancellationToken);
    }
}

/// <summary>
/// Converts ODF documents by invoking a local LibreOffice executable.
/// 使用系統安裝的 LibreOffice 執行檔進行背景文件格式轉換的渲染器。
/// </summary>
public class LibreOfficeRenderer
{
    /// <summary>
    /// Gets or sets the LibreOffice executable path.
    /// 取得或設定 LibreOffice 的執行檔路徑。
    /// </summary>
    public string LibreOfficePath { get; set; } = DefaultLibreOfficePath();

    /// <summary>
    /// Gets or sets the timeout for LibreOffice conversion operations.
    /// 取得或設定轉檔作業的超時時間限制。預設為 60 秒。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Converts a document to the requested output format.
    /// 使用 LibreOffice 將 ODF 文件轉換為指定的目標格式並輸出。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉檔的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出檔案的目標路徑</param>
    /// <param name="format">The name or identifier. / 要轉換的目標格式，例如 pdf</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當來源文件或輸出路徑為 null 時擲出</exception>
    /// <exception cref="TimeoutException">Thrown when the documented condition occurs. / 當 LibreOffice 轉檔執行程序超時未回應時擲出</exception>
    /// <exception cref="InvalidOperationException">Thrown when the documented condition occurs. / 當 LibreOffice 進程結束但傳回非零的錯誤碼時擲出</exception>
    /// <exception cref="FileNotFoundException">Thrown when the documented condition occurs. / 當轉檔完成後找不到預期的目標檔案時擲出</exception>
    public void Convert(OdfDocument document, string outputPath, string format)
    {
        ConvertAsync(document, outputPath, format, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Converts a document to the requested output format asynchronously.
    /// 使用 LibreOffice 將 ODF 文件非同步轉換為指定的目標格式並輸出。
    /// </summary>
    /// <param name="document">The source or target object. / 要轉檔的 OdfDocument 來源文件</param>
    /// <param name="outputPath">The path or URI. / 輸出檔案的目標路徑</param>
    /// <param name="format">The name or identifier. / 要轉換的目標格式，例如 <c>pdf</c></param>
    /// <param name="cancellationToken">The cancellation token. / 用於取消轉檔作業的權杖</param>
    /// <returns>The result. / 代表非同步轉檔作業的工作</returns>
    public virtual async Task ConvertAsync(
        OdfDocument document,
        string outputPath,
        string format,
        CancellationToken cancellationToken = default)
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
            string inputFileName = "document." + GetInputExtension(document);
            string inputFilePath = Path.Combine(tempSandbox, inputFileName);
            await WriteAllBytesAsync(inputFilePath, document.SaveToBytes(), cancellationToken).ConfigureAwait(false);

            await ConvertFileAsync(inputFilePath, outputPath, format, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SafeCleanDirectory(tempSandbox);
        }
    }

    /// <summary>
    /// Converts a file to the requested output format.
    /// 使用系統安裝的 LibreOffice 將指定的實體檔案轉換為目標格式並輸出。
    /// </summary>
    /// <param name="inputFilePath">The path or URI. / 輸入檔案的實體絕對路徑</param>
    /// <param name="outputPath">The path or URI. / 輸出檔案的目標絕對路徑</param>
    /// <param name="format">The name or identifier. / 要轉換的目標格式（例如 <c>pdf</c>）</param>
    public void ConvertFile(string inputFilePath, string outputPath, string format)
    {
        ConvertFileAsync(inputFilePath, outputPath, format, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Converts an input file through LibreOffice asynchronously.
    /// 使用系統安裝的 LibreOffice 將指定的實體檔案非同步轉換為目標格式並輸出。
    /// </summary>
    /// <param name="inputFilePath">The path or URI. / 輸入檔案的實體絕對路徑</param>
    /// <param name="outputPath">The path or URI. / 輸出檔案的目標絕對路徑</param>
    /// <param name="format">The name or identifier. / 要轉換的目標格式（例如 <c>pdf</c>）</param>
    /// <param name="cancellationToken">The cancellation token. / 用於取消轉檔作業的權杖</param>
    /// <returns>The result. / 代表非同步轉檔作業的工作</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當輸入或輸出路徑為 null 或空字串時擲出</exception>
    /// <exception cref="TimeoutException">Thrown when the documented condition occurs. / 當 LibreOffice 轉檔執行程序超時未回應時擲出</exception>
    /// <exception cref="InvalidOperationException">Thrown when the documented condition occurs. / 當 LibreOffice 進程結束但傳回非零的錯誤碼時擲出</exception>
    /// <exception cref="FileNotFoundException">Thrown when the documented condition occurs. / 當轉檔完成後找不到預期的目標檔案時擲出</exception>
    /// <exception cref="OperationCanceledException">Thrown when the documented condition occurs. / 當作業因 <paramref name="cancellationToken"/> 取消時擲出</exception>
    public virtual async Task ConvertFileAsync(
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
                    throw new TimeoutException(OdfLocalizer.GetMessage("Err_LibreOfficeRenderer_LibreofficeConversionProcessTimed"));
                }

                _ = await stdOutTask.ConfigureAwait(false);
                _ = await stdErrTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_LibreOfficeRenderer_LibreofficeExitedCode", process.ExitCode));
                }
            }

            string expectedOutName = Path.GetFileNameWithoutExtension(sandboxInputPath) + "." + GetOutputExtension(format);
            string convertedFilePath = Path.Combine(sandboxDir, expectedOutName);

            if (!File.Exists(convertedFilePath))
            {
                throw new FileNotFoundException(OdfLocalizer.GetMessage("Err_LibreOfficeRenderer_FailedToLibreofficeGenerateTarget"));
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
            OdfKitDiagnostics.Warn(OdfLocalizer.GetMessage("Diag_LibreOfficeRenderer_KillAfterTimeoutFailed", ex.Message), ex);
        }
    }

    private static string DefaultLibreOfficePath()
    {
        string? configured = ResolveConfiguredLibreOfficePath();
        if (!string.IsNullOrEmpty(configured))
        {
            return configured!;
        }

#if NET10_0
        if (OperatingSystem.IsWindows())
        {
            string[] paths = [
                @"C:\Program Files\LibreOffice\program\soffice.com",
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.com",
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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string[] paths = [
                @"C:\Program Files\LibreOffice\program\soffice.com",
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.com",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            ];
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var path = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
            if (File.Exists(path)) return path;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
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

    /// <summary>
    /// Gets get input extension.
    /// 取得 LibreOffice 轉檔時應使用的來源 ODF 副檔名。
    /// </summary>
    /// <param name="document">The source or target object. / 來源 ODF 文件</param>
    /// <returns>The result. / 不含前導句點的副檔名</returns>
    public static string GetInputExtension(OdfDocument document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        return OdfDocumentKindDetector.TryGetFormatByKind(document.DocumentKind, out OdfFormatInfo? format)
            ? format!.Extension.TrimStart('.')
            : "odt";
    }

    private static string GetOutputExtension(string format)
    {
        string trimmed = format.Trim();
        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0)
        {
            trimmed = trimmed.Substring(0, colonIndex);
        }

        return trimmed.Trim().TrimStart('.');
    }

    private static string? ResolveConfiguredLibreOfficePath()
    {
        foreach (string variable in new[] { "ODFKIT_SOFFICE_PATH", "LIBREOFFICE_PATH" })
        {
            foreach (string candidate in ExpandLibreOfficeCandidate(Environment.GetEnvironmentVariable(variable)))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string[] ExpandLibreOfficeCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        string candidate = value!.Trim().Trim('"');
        if (File.Exists(candidate))
        {
            return new[] { candidate };
        }

        if (!Directory.Exists(candidate))
        {
            return new[] { candidate };
        }

        return new[]
        {
            Path.Combine(candidate, "soffice.com"),
            Path.Combine(candidate, "soffice.exe"),
            Path.Combine(candidate, "program", "soffice.com"),
            Path.Combine(candidate, "program", "soffice.exe"),
            Path.Combine(candidate, "App", "libreoffice", "program", "soffice.com"),
            Path.Combine(candidate, "App", "libreoffice", "program", "soffice.exe")
        };
    }

    private static Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        return File.WriteAllBytesAsync(path, bytes, cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllBytes(path, bytes);
        return Task.CompletedTask;
#endif
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
                    OdfKitDiagnostics.Warn(OdfLocalizer.GetMessage("Diag_LibreOfficeRenderer_SandboxDeleteFailed", dirPath, attempts, ex.Message));
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}

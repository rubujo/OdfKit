using System.Runtime.InteropServices;
using OdfKit.Compliance;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Identifies the outcome reported by a script-content scanner.
/// 識別指令碼內容掃描器回報的結果。
/// </summary>
public enum OdfScriptScanVerdict
{
    /// <summary>
    /// The scanner did not find a threat.
    /// 掃描器未發現威脅。
    /// </summary>
    Clean,

    /// <summary>
    /// The scanner completed without a positive detection.
    /// 掃描器已完成，但未得到明確偵測結果。
    /// </summary>
    NotDetected,

    /// <summary>
    /// The scanner or an administrator policy marked the content as suspicious.
    /// 掃描器或系統管理員政策將內容標示為可疑。
    /// </summary>
    Suspicious,

    /// <summary>
    /// The scanner identified malware.
    /// 掃描器已識別惡意程式碼。
    /// </summary>
    Malware,

    /// <summary>
    /// The scanner is unavailable on the current host.
    /// 掃描器無法在目前主機上使用。
    /// </summary>
    Unavailable,

    /// <summary>
    /// The scanner failed without producing a security decision.
    /// 掃描器失敗，且未產生安全判定。
    /// </summary>
    Error
}

/// <summary>
/// Supplies normalized script content to a scanning provider.
/// 將正規化的指令碼內容提供給掃描 provider。
/// </summary>
public sealed class OdfScriptScanRequest
{
    /// <summary>
    /// Initializes a script scan request.
    /// 初始化指令碼掃描要求。
    /// </summary>
    /// <param name="path">The package-relative content name. / 封裝相對內容名稱。</param>
    /// <param name="language">The scripting language. / 指令碼語言。</param>
    /// <param name="source">The source text to scan. / 要掃描的原始碼文字。</param>
    public OdfScriptScanRequest(string path, OdfScriptSyntaxLanguage language, string source)
    {
        Path = path ?? throw new ArgumentNullException(
            nameof(path),
            OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(path)));
        Source = source ?? throw new ArgumentNullException(
            nameof(source),
            OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(source)));
        if (!OdfKit.Internal.OdfEnumHelper.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(
                nameof(language),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(language)));
        }

        Language = language;
    }

    /// <summary>
    /// Gets the package-relative content name.
    /// 取得封裝相對內容名稱。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the scripting language.
    /// 取得指令碼語言。
    /// </summary>
    public OdfScriptSyntaxLanguage Language { get; }

    /// <summary>
    /// Gets the source text.
    /// 取得原始碼文字。
    /// </summary>
    public string Source { get; }
}

/// <summary>
/// Describes one provider's script scan decision.
/// 描述單一 provider 的指令碼掃描判定。
/// </summary>
public sealed class OdfScriptScanResult
{
    /// <summary>
    /// Initializes a script scan result.
    /// 初始化指令碼掃描結果。
    /// </summary>
    /// <param name="providerName">The provider name. / provider 名稱。</param>
    /// <param name="verdict">The scan verdict. / 掃描判定。</param>
    /// <param name="nativeResult">The optional provider-native result value. / 選用的 provider 原生結果值。</param>
    public OdfScriptScanResult(string providerName, OdfScriptScanVerdict verdict, int? nativeResult)
    {
        ProviderName = providerName ?? throw new ArgumentNullException(
            nameof(providerName),
            OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(providerName)));
        if (!OdfKit.Internal.OdfEnumHelper.IsDefined(verdict))
        {
            throw new ArgumentOutOfRangeException(
                nameof(verdict),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(verdict)));
        }

        Verdict = verdict;
        NativeResult = nativeResult;
    }

    /// <summary>
    /// Gets the provider name.
    /// 取得 provider 名稱。
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the scan verdict.
    /// 取得掃描判定。
    /// </summary>
    public OdfScriptScanVerdict Verdict { get; }

    /// <summary>
    /// Gets the optional provider-native result value.
    /// 取得選用的 provider 原生結果值。
    /// </summary>
    public int? NativeResult { get; }
}

/// <summary>
/// Defines a replaceable antimalware or sandbox scanning provider.
/// 定義可替換的防毒或沙箱掃描 provider。
/// </summary>
public interface IOdfScriptScanner
{
    /// <summary>
    /// Scans script content without executing it.
    /// 在不執行指令碼的情況下掃描其內容。
    /// </summary>
    /// <param name="request">The scan request. / 掃描要求。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>The provider decision. / provider 判定。</returns>
    Task<OdfScriptScanResult> ScanAsync(
        OdfScriptScanRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Runs multiple scanners and preserves every independent decision.
/// 執行多個掃描器，並保留每一項獨立判定。
/// </summary>
public sealed class OdfScriptScannerPipeline
{
    private readonly IOdfScriptScanner[] _scanners;

    /// <summary>
    /// Initializes a scanner pipeline.
    /// 初始化掃描器管線。
    /// </summary>
    /// <param name="scanners">The scanners to invoke in order. / 依序呼叫的掃描器。</param>
    public OdfScriptScannerPipeline(IEnumerable<IOdfScriptScanner> scanners)
    {
        if (scanners is null)
        {
            throw new ArgumentNullException(
                nameof(scanners),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(scanners)));
        }

        _scanners = scanners.ToArray();
        if (_scanners.Length == 0 || _scanners.Any(scanner => scanner is null))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(scanners)),
                nameof(scanners));
        }
    }

    /// <summary>
    /// Scans one request with every configured provider.
    /// 使用每個已設定的 provider 掃描一項要求。
    /// </summary>
    /// <param name="request">The scan request. / 掃描要求。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>All independent provider decisions. / 所有獨立的 provider 判定。</returns>
    public async Task<IReadOnlyList<OdfScriptScanResult>> ScanAsync(
        OdfScriptScanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(request)));
        }

        List<OdfScriptScanResult> results = [];
        foreach (IOdfScriptScanner scanner in _scanners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await scanner.ScanAsync(request, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }
}

/// <summary>
/// Scans script text through the Windows Antimalware Scan Interface.
/// 透過 Windows Antimalware Scan Interface 掃描指令碼文字。
/// </summary>
public sealed class OdfAmsiScriptScanner : IOdfScriptScanner
{
    private const int AmsiResultDetected = 32768;
    private const int AmsiResultBlockedByAdminStart = 0x4000;
    private const int AmsiResultBlockedByAdminEnd = 0x4FFF;

    /// <summary>
    /// Gets whether AMSI is available on the current operating system.
    /// 取得 AMSI 是否可在目前作業系統上使用。
    /// </summary>
    public static bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Scans script text through Windows AMSI.
    /// 透過 Windows AMSI 掃描指令碼文字。
    /// </summary>
    /// <param name="request">The script scan request. / 指令碼掃描要求。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>The AMSI scan result. / AMSI 掃描結果。</returns>
    public Task<OdfScriptScanResult> ScanAsync(
        OdfScriptScanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(request)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(new OdfScriptScanResult(
                "Windows AMSI",
                OdfScriptScanVerdict.Unavailable,
                nativeResult: null));
        }

        IntPtr context = IntPtr.Zero;
        IntPtr session = IntPtr.Zero;
        try
        {
            int initializeResult = AmsiInitialize("OdfKit.Extensions.Scripting", out context);
            if (initializeResult < 0)
                return Task.FromResult(CreateError(initializeResult));

            int openResult = AmsiOpenSession(context, out session);
            if (openResult < 0)
                return Task.FromResult(CreateError(openResult));

            cancellationToken.ThrowIfCancellationRequested();
            byte[] content = System.Text.Encoding.Unicode.GetBytes(request.Source);
            int scanResult = AmsiScanBuffer(
                context,
                content,
                checked((uint)content.Length),
                request.Path,
                session,
                out int amsiResult);
            if (scanResult < 0)
                return Task.FromResult(CreateError(scanResult));

            OdfScriptScanVerdict verdict = amsiResult switch
            {
                >= AmsiResultDetected => OdfScriptScanVerdict.Malware,
                >= AmsiResultBlockedByAdminStart and <= AmsiResultBlockedByAdminEnd =>
                    OdfScriptScanVerdict.Suspicious,
                0 => OdfScriptScanVerdict.Clean,
                _ => OdfScriptScanVerdict.NotDetected
            };
            return Task.FromResult(new OdfScriptScanResult("Windows AMSI", verdict, amsiResult));
        }
        catch (DllNotFoundException)
        {
            return Task.FromResult(new OdfScriptScanResult(
                "Windows AMSI",
                OdfScriptScanVerdict.Unavailable,
                nativeResult: null));
        }
        catch (EntryPointNotFoundException)
        {
            return Task.FromResult(new OdfScriptScanResult(
                "Windows AMSI",
                OdfScriptScanVerdict.Unavailable,
                nativeResult: null));
        }
        finally
        {
            if (session != IntPtr.Zero && context != IntPtr.Zero)
                AmsiCloseSession(context, session);
            if (context != IntPtr.Zero)
                AmsiUninitialize(context);
        }
    }

    private static OdfScriptScanResult CreateError(int result) =>
        new("Windows AMSI", OdfScriptScanVerdict.Error, result);

    [DllImport("amsi.dll", CharSet = CharSet.Unicode)]
    private static extern int AmsiInitialize(string applicationName, out IntPtr context);

    [DllImport("amsi.dll")]
    private static extern int AmsiOpenSession(IntPtr context, out IntPtr session);

    [DllImport("amsi.dll", CharSet = CharSet.Unicode)]
    private static extern int AmsiScanBuffer(
        IntPtr context,
        byte[] buffer,
        uint length,
        string contentName,
        IntPtr session,
        out int result);

    [DllImport("amsi.dll")]
    private static extern void AmsiCloseSession(IntPtr context, IntPtr session);

    [DllImport("amsi.dll")]
    private static extern void AmsiUninitialize(IntPtr context);
}

/// <summary>
/// Associates one package script with all provider decisions.
/// 將一個封裝指令碼與所有 provider 判定建立關聯。
/// </summary>
public sealed class OdfPackageScriptScanReport
{
    internal OdfPackageScriptScanReport(
        string path,
        OdfScriptSyntaxLanguage language,
        IReadOnlyList<OdfScriptScanResult> results)
    {
        Path = path;
        Language = language;
        Results = results;
    }

    /// <summary>
    /// Gets the package-relative script path.
    /// 取得封裝相對指令碼路徑。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the scripting language.
    /// 取得指令碼語言。
    /// </summary>
    public OdfScriptSyntaxLanguage Language { get; }

    /// <summary>
    /// Gets every independent scanner decision.
    /// 取得每一項獨立掃描器判定。
    /// </summary>
    public IReadOnlyList<OdfScriptScanResult> Results { get; }
}

/// <summary>
/// Provides package-script scanning operations.
/// 提供封裝指令碼掃描作業。
/// </summary>
public sealed partial class OdfScriptManager
{
    /// <summary>
    /// Scans every recognized package script through a provider pipeline.
    /// 透過 provider 管線掃描每個已辨識的封裝指令碼。
    /// </summary>
    /// <param name="pipeline">The scanner pipeline. / 掃描器管線。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>One report for each recognized package script. / 每個已辨識封裝指令碼各一份報告。</returns>
    public async Task<IReadOnlyList<OdfPackageScriptScanReport>> ScanPackageScriptsAsync(
        OdfScriptScannerPipeline pipeline,
        CancellationToken cancellationToken = default)
    {
        if (pipeline is null)
        {
            throw new ArgumentNullException(
                nameof(pipeline),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(pipeline)));
        }

        List<OdfPackageScriptScanReport> reports = [];
        foreach (OdfPackageScriptEntry entry in GetPackageScripts())
        {
            cancellationToken.ThrowIfCancellationRequested();
            (OdfScriptSyntaxLanguage language, string source) = ReadPackageScriptSource(entry);
            var request = new OdfScriptScanRequest(entry.Path, language, source);
            IReadOnlyList<OdfScriptScanResult> results = await pipeline
                .ScanAsync(request, cancellationToken)
                .ConfigureAwait(false);
            reports.Add(new OdfPackageScriptScanReport(entry.Path, language, results));
        }

        return reports;
    }
}

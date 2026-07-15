namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Configures the trusted FontTools executable and registered font sources.
/// 設定受信任的 FontTools 執行檔與已註冊字型來源。
/// </summary>
public sealed class FontToolsWebFontEngineOptions
{
    /// <summary>
    /// Gets or sets the trusted pyftsubset executable path or command name.
    /// 取得或設定受信任的 pyftsubset 執行檔路徑或命令名稱。
    /// </summary>
    public string ExecutablePath { get; set; } = "pyftsubset";

    /// <summary>
    /// Gets trusted arguments inserted before the source font path.
    /// 取得插入來源字型路徑之前的受信任引數。
    /// </summary>
    public IList<string> ExecutablePrefixArguments { get; } = new List<string>();

    /// <summary>
    /// Gets trusted environment variables supplied to the isolated process.
    /// 取得提供給隔離處理程序的受信任環境變數。
    /// </summary>
    public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the opaque source identifiers mapped to trusted local font paths.
    /// 取得對應至受信任本機字型路徑的不透明來源識別碼。
    /// </summary>
    public IDictionary<string, string> FontSources { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the maximum source font size in bytes.
    /// 取得或設定來源字型的最大位元組數。
    /// </summary>
    public long MaxSourceBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum generated asset size in bytes.
    /// 取得或設定產生資產的最大位元組數。
    /// </summary>
    public long MaxOutputBytes { get; set; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of Unicode scalars per request.
    /// 取得或設定每個要求的 Unicode 純量值數上限。
    /// </summary>
    public int MaxUnicodeScalars { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the process timeout.
    /// 取得或設定處理程序逾時時間。
    /// </summary>
    public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromMinutes(2);
}

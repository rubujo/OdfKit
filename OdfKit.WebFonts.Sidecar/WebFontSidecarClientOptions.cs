namespace OdfKit.WebFonts.Sidecar;

/// <summary>
/// Configures authenticated communication with a local OdfKit WebFont sidecar.
/// 設定與本機 OdfKit WebFont sidecar 之間經驗證的通訊。
/// </summary>
public sealed class WebFontSidecarClientOptions
{
    /// <summary>
    /// Gets or sets the operating-system pipe name without a server or path prefix.
    /// 取得或設定不含伺服器或路徑前綴的作業系統 pipe 名稱。
    /// </summary>
    public string PipeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pre-shared authentication token sent inside every bounded request.
    /// 取得或設定每個有界要求內傳送的預先共用驗證權杖。
    /// </summary>
    public string AuthenticationToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trusted asset root shared with the sidecar process.
    /// 取得或設定與 sidecar 處理程序共用的受信任資產根目錄。
    /// </summary>
    public string AssetRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum time allowed to connect to the local pipe.
    /// 取得或設定連線至本機 pipe 的最長時間。
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum end-to-end time for one sidecar operation.
    /// 取得或設定單一 sidecar 作業的端對端最長時間。
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Gets or sets the maximum request or response frame size in bytes.
    /// 取得或設定單一要求或回應 frame 的最大位元組數。
    /// </summary>
    public int MaxMessageBytes { get; set; } = 4 * 1024 * 1024;
}

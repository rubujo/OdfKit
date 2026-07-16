namespace OdfKit.WebFonts.Hosting.AspNetCore;

/// <summary>
/// Configures the opt-in authenticated WebFont generation endpoint.
/// 設定選擇啟用且須經授權的 WebFont 動態產生 endpoint。
/// </summary>
public sealed class OdfWebFontGenerationOptions
{
    /// <summary>
    /// Gets or sets the required ASP.NET Core authorization policy name.
    /// 取得或設定必要的 ASP.NET Core 授權原則名稱。
    /// </summary>
    public string AuthorizationPolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the required ASP.NET Core rate-limiter policy name.
    /// 取得或設定必要的 ASP.NET Core 速率限制原則名稱。
    /// </summary>
    public string RateLimiterPolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum JSON request body size in bytes.
    /// 取得或設定 JSON 要求本文的位元組大小上限。
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of text sequences in one request.
    /// 取得或設定單一要求可包含的文字序列數量上限。
    /// </summary>
    public int MaxSequenceCount { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum total Unicode scalar count in one request.
    /// 取得或設定單一要求可包含的 Unicode 純量值總數上限。
    /// </summary>
    public int MaxUnicodeScalarCount { get; set; } = 4096;

    /// <summary>
    /// Gets the exact trusted font faces clients may request.
    /// 取得用戶端可要求的精確受信任字型 face。
    /// </summary>
    public ICollection<WebFontFaceIdentity> AllowedFaces { get; } = new List<WebFontFaceIdentity>();

    /// <summary>
    /// Gets the exact profile and mapping version identifiers clients may request.
    /// 取得用戶端可要求的精確 profile 與 mapping 版本識別碼。
    /// </summary>
    public ICollection<string> AllowedProfileIds { get; } = new List<string>();

    /// <summary>
    /// Gets the output formats clients may request.
    /// 取得用戶端可要求的輸出格式。
    /// </summary>
    public ICollection<WebFontFormat> AllowedFormats { get; } = new List<WebFontFormat> { WebFontFormat.Woff2 };
}

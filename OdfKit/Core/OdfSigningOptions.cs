using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace OdfKit.Core;

/// <summary>
/// Defines supported XAdES signature levels.
/// 定義支援的 XAdES 簽章層級。
/// </summary>
public enum XadesLevel
{
    /// <summary>
    /// Uses plain W3C XMLDSig without XAdES extensions.
    /// 使用不含 XAdES 擴充的純 W3C XMLDSig 簽章。
    /// </summary>
    None,

    /// <summary>
    /// Uses XAdES Basic Electronic Signature semantics.
    /// 使用 XAdES 基本電子簽章語意。
    /// </summary>
    BES,

    /// <summary>
    /// Uses XAdES with a trusted timestamp.
    /// 使用含可信時間戳記的 XAdES 簽章。
    /// </summary>
    T,

    /// <summary>
    /// Uses XAdES long-term validation data.
    /// 使用含長效驗證資料的 XAdES 簽章。
    /// </summary>
    LT,

    /// <summary>
    /// Uses archival XAdES validation data.
    /// 使用封存等級的 XAdES 驗證資料。
    /// </summary>
    A
}

/// <summary>
/// Defines signature levels supported for ODF package signatures.
/// 定義 ODF 封裝簽章支援的簽章層級。
/// </summary>
public enum OdfSignatureLevel
{
    /// <summary>
    /// Uses plain XMLDSig without XAdES extensions.
    /// 使用不含 XAdES 擴充的純 XMLDSig 簽章。
    /// </summary>
    None = 0,

    /// <summary>
    /// Uses XAdES Basic Electronic Signature semantics.
    /// 使用 XAdES 基本電子簽章語意。
    /// </summary>
    XadesBes = 1,

    /// <summary>
    /// Uses XAdES with a trusted timestamp.
    /// 使用含可信時間戳記的 XAdES 簽章。
    /// </summary>
    XadesT = 2,

    /// <summary>
    /// Uses XAdES long-term validation data.
    /// 使用含長效驗證資料的 XAdES 簽章。
    /// </summary>
    XadesLT = 3,

    /// <summary>
    /// Uses archival XAdES validation data.
    /// 使用封存等級的 XAdES 驗證資料。
    /// </summary>
    XadesA = 4
}

/// <summary>
/// Controls digital signature and XAdES behavior for ODF package signing and validation.
/// 控制 ODF 封裝簽署與驗證時的數位簽章與 XAdES 行為。
/// </summary>
public class OdfSigningOptions
{
    /// <summary>
    /// Gets or sets the ODF signature level to create or validate.
    /// 取得或設定要建立或驗證的 ODF 簽章層級。
    /// </summary>
    public OdfSignatureLevel SignatureLevel { get; set; } = OdfSignatureLevel.None;

    /// <summary>
    /// Gets or sets the XAdES level mapped to <see cref="SignatureLevel"/>.
    /// 取得或設定對應至 <see cref="SignatureLevel"/> 的 XAdES 層級。
    /// </summary>
    public XadesLevel Level
    {
        get
        {
            return SignatureLevel switch
            {
                OdfSignatureLevel.None => XadesLevel.None,
                OdfSignatureLevel.XadesBes => XadesLevel.BES,
                OdfSignatureLevel.XadesT => XadesLevel.T,
                OdfSignatureLevel.XadesLT => XadesLevel.LT,
                OdfSignatureLevel.XadesA => XadesLevel.A,
                _ => XadesLevel.None
            };
        }
        set
        {
            SignatureLevel = value switch
            {
                XadesLevel.None => OdfSignatureLevel.None,
                XadesLevel.BES => OdfSignatureLevel.XadesBes,
                XadesLevel.T => OdfSignatureLevel.XadesT,
                XadesLevel.LT => OdfSignatureLevel.XadesLT,
                XadesLevel.A => OdfSignatureLevel.XadesA,
                _ => OdfSignatureLevel.None
            };
        }
    }

    /// <summary>
    /// Gets or sets the RFC 3161 timestamp authority URL.
    /// 取得或設定 RFC 3161 時間戳記授權機構 URL。
    /// </summary>
    public string? TsaUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether certificate revocation is checked.
    /// 取得或設定是否檢查憑證撤銷狀態。
    /// </summary>
    public bool CheckRevocation { get; set; }

    /// <summary>
    /// Gets or sets the HTTP client used to fetch CRLs and contact timestamp authorities.
    /// 取得或設定用於擷取 CRL 與查詢時間戳記授權機構的 HTTP client。
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether untrusted root certificates are accepted during validation.
    /// 取得或設定驗證期間是否接受不受信任的根憑證。
    /// </summary>
    public bool AllowUntrustedRoot { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether untrusted timestamp certificates are accepted.
    /// 取得或設定是否接受不受信任的時間戳記憑證。
    /// </summary>
    public bool AllowUntrustedTimestamp { get; set; }

    /// <summary>
    /// Gets additional certificates used when building signing or validation chains.
    /// 取得建立簽署或驗證憑證鏈時使用的額外憑證。
    /// </summary>
    public X509Certificate2Collection ExtraCertificates { get; } = new();
}

using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace OdfKit.Core;

/// <summary>
/// XAdES 標準層級。
/// </summary>
public enum XadesLevel
{
    /// <summary>
    /// 不使用 XAdES 擴充的純 W3C XMLDSig 簽章。
    /// </summary>
    None,

    /// <summary>
    /// XAdES 基本電子簽章 (XAdES-BES)。
    /// </summary>
    BES,

    /// <summary>
    /// 含時間戳記的 XAdES 簽章 (XAdES-T)。
    /// </summary>
    T,

    /// <summary>
    /// 含憑證鏈與撤銷資訊的長效驗證 XAdES 簽章 (XAdES-LT)。
    /// </summary>
    LT,

    /// <summary>
    /// 封存/長期驗證 XAdES 簽章 (XAdES-A)。
    /// </summary>
    A
}

/// <summary>
/// ODF 文件支援的簽章層級。
/// </summary>
public enum OdfSignatureLevel
{
    /// <summary>
    /// 不使用 XAdES 擴充的純 XMLDSig 簽章。
    /// </summary>
    None = 0,

    /// <summary>
    /// XAdES 基本電子簽章 (XAdES-BES)。
    /// </summary>
    XadesBes = 1,

    /// <summary>
    /// 含時間戳記的 XAdES 簽章 (XAdES-T)。
    /// </summary>
    XadesT = 2,

    /// <summary>
    /// 含憑證鏈與撤銷資訊的長效驗證 XAdES 簽章 (XAdES-LT)。
    /// </summary>
    XadesLT = 3,

    /// <summary>
    /// 封存/長期驗證 XAdES 簽章 (XAdES-A)。
    /// </summary>
    XadesA = 4
}

/// <summary>
/// 用於以數位簽章/ XAdES 簽署與驗證 ODF 封裝的組態選項。
/// </summary>
public class OdfSigningOptions
{
    /// <summary>
    /// 取得或設定簽章層級。
    /// </summary>
    public OdfSignatureLevel SignatureLevel { get; set; } = OdfSignatureLevel.None;

    /// <summary>
    /// 取得或設定 XAdES 標準層級（ None/XMLDSig, BES, T, LT, A ）。
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
    /// 取得或設定 RFC 3161 時間戳記授權機構（TSA）的 URL 。
    /// </summary>
    public string? TsaUrl { get; set; }

    /// <summary>
    /// 取得或設定是否檢查憑證撤銷狀態（透過 CRL ）。
    /// </summary>
    public bool CheckRevocation { get; set; } = false;

    /// <summary>
    /// 取得或設定自訂的 HttpClient ，用於擷取 CRL 與查詢 TSA ；可用於離線模擬測試。
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// 取得或設定在驗證簽章時，是否允許不受信任的根憑證。
    /// </summary>
    public bool AllowUntrustedRoot { get; set; } = false;

    /// <summary>
    /// 取得或設定在驗證簽章時，是否允許不受信任的時間戳記憑證。
    /// </summary>
    public bool AllowUntrustedTimestamp { get; set; } = false;

    /// <summary>
    /// 取得額外的憑證，用於建立簽署或驗證憑證鏈。
    /// </summary>
    public X509Certificate2Collection ExtraCertificates { get; } = new();
}

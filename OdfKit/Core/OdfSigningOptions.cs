using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

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
    /// <remarks>
    /// A supplied client is a trusted transport policy boundary. For CRL downloads, configure it to disable
    /// automatic redirects and to prevent private, loopback, link-local, and rebinding destinations. When this
    /// property is <see langword="null"/>, OdfKit applies its built-in fail-closed CRL transport policy.
    /// 呼叫端提供的 client 屬於受信任傳輸政策邊界。用於 CRL 下載時，應停用自動重新導向，並阻止
    /// 私有、loopback、link-local 與 DNS rebinding 目的地。netstandard2.0 若需使用 DNS 主機名稱，
    /// 必須提供具備上述政策的 client，並建議同時填入 <see cref="AllowedCrlHosts"/>。此屬性為
    /// <see langword="null"/> 時，OdfKit 會套用內建的 fail-closed CRL 傳輸政策。
    /// </remarks>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// Gets the exact DNS host names permitted for CRL downloads. An empty set permits any public destination.
    /// 取得允許下載 CRL 的確切 DNS 主機名稱；空集合表示允許任何公用網路目的地。
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive and applies to every redirect handled by OdfKit. Wildcards and parent-domain
    /// suffix matching are intentionally unsupported. A custom <see cref="HttpClient"/> must disable automatic
    /// redirects so OdfKit can validate every target. Populate this set when the issuing authorities are known.
    /// 比對不區分大小寫，且會套用至 OdfKit 處理的每個重新導向。刻意不支援萬用字元與父網域後綴
    /// 比對。自訂 <see cref="HttpClient"/> 必須停用自動重新導向，OdfKit 才能驗證每個目的地；已知
    /// 憑證發行者時應填入此集合。
    /// </remarks>
    public ISet<string> AllowedCrlHosts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

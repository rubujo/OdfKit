#pragma warning restore CS1591

namespace OdfKit.Core;

/// <summary>
/// Provides the OdfLoadOptions API.
/// 提供載入 ODF 文件時的組態選項。
/// </summary>
public class OdfLoadOptions
{
    /// <summary>
    /// Gets a value indicating the StrictXmlParsing state.
    /// 取得或設定是否啟用嚴格 XML 解析模式（Strict XML Parsing）。
    /// </summary>
    /// <remarks>
    /// 設為 <see langword="true"/> 時在 XML 解析錯誤或結構不合規時立即拋出例外；設為 <see langword="false"/> （預設，Lax 容錯模式）則在遇到損毀或非標準 ODF 時自動進行容錯與修復。
    /// </remarks>
    public bool StrictXmlParsing { get; set; } = false;

    /// <summary>
    /// Gets a value indicating the ValidateMimeType state.
    /// 取得或設定是否在載入時驗證 ZIP 最前方的 mimetype 檔案內容符合 ODF 規範。
    /// </summary>
    public bool ValidateMimeType { get; set; } = true;

    /// <summary>
    /// Gets the MaxZipEntries value.
    /// 取得或設定 ZIP 封裝中的最大專案（Entries）數量限制（防禦 Zip DoS）。
    /// </summary>
    public int MaxZipEntries { get; set; } = 5000;

    /// <summary>
    /// Gets the MaxEntrySize value.
    /// 取得或設定單個專案解壓後的最大位元組數限制（預設 500MB ，防禦 Zip Bomb）。
    /// </summary>
    public long MaxEntrySize { get; set; } = 500 * 1024 * 1024;

    /// <summary>
    /// Gets the MaxTotalUncompressedSize value.
    /// 取得或設定整個 ZIP 封裝解壓後的總位元組數限制（預設 1GB ，防禦 Zip Bomb）。
    /// </summary>
    public long MaxTotalUncompressedSize { get; set; } = 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum raw package byte count for non-seekable input streams.
    /// 取得或設定不可搜尋輸入串流的原始封裝位元組數上限。
    /// </summary>
    /// <remarks>
    /// This limit applies before ZIP entry expansion and is separate from <see cref="MaxTotalUncompressedSize"/>.
    /// 此限制套用於 ZIP 項目展開之前，且與 <see cref="MaxTotalUncompressedSize"/> 分開計算。
    /// </remarks>
    public long MaxPackageSize { get; set; } = 1024 * 1024 * 1024;

    /// <summary>
    /// Gets the MaxXmlCharactersInDocument value.
    /// 取得或設定單一 XML 文件可讀取的最大字元數限制（預設 64 MB，防禦 XML DoS）。
    /// </summary>
    /// <remarks>
    /// 設為 0 或負值時停用此限制；一般應維持預設值，僅在受信任的大型文件情境中調整。
    /// </remarks>
    public long MaxXmlCharactersInDocument { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Gets the Password value.
    /// 取得或設定用於解密加密 ODF 文件的密碼。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets the CryptographyProvider value.
    /// 取得或設定自訂的密碼學提供者，用於解密文件專案。
    /// </summary>
    public IOdfCryptographyProvider? CryptographyProvider { get; set; }

    /// <summary>
    /// Provides the member member.
    /// 取得或設定用於解密 ODF 1.3 OpenPGP 加密文件的金鑰提供者。
    /// </summary>
    public IOdfOpenPgpKeyProvider? OpenPgpKeyProvider
    {
        get => _openPgpKeyProvider;
        set
        {
            _openPgpKeyProvider = value;
            if (value is not null)
            {
                CryptographyProvider = new OdfOpenPgpCryptographyProvider(value);
            }
        }
    }

    private IOdfOpenPgpKeyProvider? _openPgpKeyProvider;

    /// <summary>
    /// Gets a value indicating the AllowLazyLoading state.
    /// 取得或設定一個值，指出是否允許延遲解析 XML 子樹節點。
    /// </summary>
    public bool AllowLazyLoading { get; set; } = true;

    /// <summary>
    /// Gets a value indicating the EnableDirectIo state.
    /// 取得或設定一個值，指出是否啟用作業系統的 Direct I/O 進行高效檔案讀取。
    /// </summary>
    public bool EnableDirectIo { get; set; } = false;

    /// <summary>
    /// Executes the Default operation.
    /// 取得預設的載入選項執行個體。
    /// </summary>
    public static OdfLoadOptions Default => new();
}

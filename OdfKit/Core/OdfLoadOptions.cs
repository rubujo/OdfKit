#pragma warning restore CS1591

namespace OdfKit.Core;

/// <summary>
/// Controls security, compatibility, and resource limits used when loading ODF documents.
/// 控制載入 ODF 文件時使用的安全性、相容性與資源限制。
/// </summary>
public class OdfLoadOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether XML parsing should fail on non-conforming input.
    /// 取得或設定是否在 XML 輸入不符合規範時直接失敗。
    /// </summary>
    /// <remarks>
    /// When set to <see langword="true"/>, malformed XML or non-conforming structure throws immediately.
    /// 設為 <see langword="true"/> 時，XML 解析錯誤或結構不合規會立即擲出例外；設為 <see langword="false"/>（預設）時，會對損毀或非標準 ODF 輸入採取容錯處理。
    /// </remarks>
    public bool StrictXmlParsing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the package mimetype entry is validated during load.
    /// 取得或設定載入時是否驗證 ZIP 最前方的 mimetype 專案內容符合 ODF 規範。
    /// </summary>
    public bool ValidateMimeType { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of ZIP entries allowed in a package.
    /// 取得或設定 ZIP 封裝中允許的最大專案數量。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1. / 當值小於 1 時擲出。</exception>
    public int MaxZipEntries
    {
        get => _maxZipEntries;
        set => _maxZipEntries = OdfOptionGuard.EnsurePositive(value, nameof(MaxZipEntries));
    }

    private int _maxZipEntries = 5000;

    /// <summary>
    /// Gets or sets the maximum uncompressed byte count allowed for a single package entry.
    /// 取得或設定單一封裝項目解壓縮後允許的最大位元組數。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1. / 當值小於 1 時擲出。</exception>
    public long MaxEntrySize
    {
        get => _maxEntrySize;
        set => _maxEntrySize = OdfOptionGuard.EnsurePositive(value, nameof(MaxEntrySize));
    }

    private long _maxEntrySize = 500 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum total uncompressed byte count allowed for the package.
    /// 取得或設定整個 ZIP 封裝解壓後允許的總位元組數上限。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1. / 當值小於 1 時擲出。</exception>
    public long MaxTotalUncompressedSize
    {
        get => _maxTotalUncompressedSize;
        set => _maxTotalUncompressedSize = OdfOptionGuard.EnsurePositive(value, nameof(MaxTotalUncompressedSize));
    }

    private long _maxTotalUncompressedSize = 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum raw package byte count for non-seekable input streams.
    /// 取得或設定不可搜尋輸入串流的原始封裝位元組數上限。
    /// </summary>
    /// <remarks>
    /// This limit applies before ZIP entry expansion and is separate from <see cref="MaxTotalUncompressedSize"/>.
    /// 此限制套用於 ZIP 項目展開之前，且與 <see cref="MaxTotalUncompressedSize"/> 分開計算。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1. / 當值小於 1 時擲出。</exception>
    public long MaxPackageSize
    {
        get => _maxPackageSize;
        set => _maxPackageSize = OdfOptionGuard.EnsurePositive(value, nameof(MaxPackageSize));
    }

    private long _maxPackageSize = 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of XML characters allowed in a single XML document.
    /// 取得或設定單一 XML 文件允許讀取的最大字元數。
    /// </summary>
    /// <remarks>
    /// Set this value to zero to disable the limit.
    /// 設為 0 時會停用此限制；一般應維持預設值，僅在受信任的大型文件情境中調整。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative. / 當值為負數時擲出。</exception>
    public long MaxXmlCharactersInDocument
    {
        get => _maxXmlCharactersInDocument;
        set => _maxXmlCharactersInDocument = OdfOptionGuard.EnsureNonNegative(value, nameof(MaxXmlCharactersInDocument));
    }

    private long _maxXmlCharactersInDocument = 64 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the password used to decrypt password-protected ODF documents.
    /// 取得或設定用於解密受密碼保護 ODF 文件的密碼。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the cryptography provider used to decrypt encrypted package entries.
    /// 取得或設定用於解密加密封裝項目的密碼學提供者。
    /// </summary>
    public IOdfCryptographyProvider? CryptographyProvider { get; set; }

    /// <summary>
    /// Gets or sets the OpenPGP key provider used for ODF 1.3 package decryption.
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
    /// Gets or sets a value indicating whether XML subtrees may be parsed lazily.
    /// 取得或設定是否允許延遲解析 XML 子樹節點。
    /// </summary>
    public bool AllowLazyLoading { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Direct I/O is used for high-throughput file reads.
    /// 取得或設定是否啟用 Direct I/O 進行高吞吐量檔案讀取。
    /// </summary>
    public bool EnableDirectIo { get; set; }

    /// <summary>
    /// Gets a new instance with the default load settings.
    /// 取得使用預設載入設定的新執行個體。
    /// </summary>
    public static OdfLoadOptions Default => new();

}

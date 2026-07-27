#pragma warning restore CS1591

using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Defines encryption algorithms available when saving encrypted ODF packages.
/// 定義儲存加密 ODF 封裝時可使用的加密演算法。
/// </summary>
public enum OdfEncryptionAlgorithm
{
    /// <summary>
    /// Uses the ODF AES-256 encryption profile.
    /// 使用 ODF AES-256 加密設定檔。
    /// </summary>
    Aes256,

    /// <summary>
    /// Uses the legacy Blowfish encryption profile.
    /// 使用舊版 Blowfish 加密設定檔。
    /// </summary>
    Blowfish,

    /// <summary>
    /// Uses OpenPGP encryption through a custom cryptography provider.
    /// 透過自訂密碼學提供者使用 OpenPGP 加密。
    /// </summary>
    OpenPgp,

    /// <summary>
    /// Uses LibreOffice-compatible wholesome encryption with AES-256-GCM and Argon2id.
    /// 使用與 LibreOffice 相容的 AES-256-GCM 與 Argon2id 整包加密。
    /// </summary>
    Aes256Gcm
}

/// <summary>
/// Controls package layout, compatibility, and encryption behavior used when saving ODF documents.
/// 控制儲存 ODF 文件時使用的封裝配置、相容性與加密行為。
/// </summary>
public class OdfSaveOptions
{
    /// <summary>
    /// Gets or sets the ZIP compression level used for package entries.
    /// 取得或設定封裝項目使用的 ZIP 壓縮等級。
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// Gets or sets the algorithm used when password or OpenPGP encryption is enabled.
    /// 取得或設定啟用密碼或 OpenPGP 加密時使用的演算法。
    /// </summary>
    public OdfEncryptionAlgorithm EncryptionAlgorithm { get; set; } = OdfEncryptionAlgorithm.Aes256;

    /// <summary>
    /// Gets or sets the culture used for user-facing document formatting decisions.
    /// 取得或設定用於面向使用者之文件格式化決策的文化語系。
    /// </summary>
    /// <remarks>
    /// The invariant culture is still used for raw XML numeric and date serialization.
    /// 此設定用於轉譯貨幣、日期時間等格式化字串；底層 XML 的浮點數與日期序列化仍一律使用不變文化。
    /// </remarks>
    public CultureInfo DocumentCulture { get; set; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// Gets or sets a value indicating whether XML entries should be indented for diagnostics.
    /// 取得或設定是否為了診斷目的縮排 XML 專案。
    /// </summary>
    public bool IndentXml { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether repeat saves should produce deterministic ZIP metadata.
    /// 取得或設定重複儲存是否產生確定性的 ZIP 中繼資料。
    /// </summary>
    /// <remarks>
    /// When enabled, ZIP entry timestamps are pinned so unchanged content produces repeatable binary hashes.
    /// 啟用時，所有 ZIP 封裝項目的 LastWriteTime 會固定，讓內容不變時產生可重複的二進位雜湊值。
    /// </remarks>
    public bool Deterministic { get; set; }

    /// <summary>
    /// Gets or sets the ODF version to force into saved package metadata.
    /// 取得或設定儲存時要強制寫入封裝中繼資料的 ODF 版本。
    /// </summary>
    public OdfVersion? ForceVersion { get; set; }

    /// <summary>
    /// Gets or sets a callback that receives structured diagnostics before a version-targeted save.
    /// 取得或設定回呼，以在指定版本儲存前接收結構化診斷。
    /// </summary>
    /// <remarks>
    /// The callback receives an empty report when the conversion is safe. The document also retains the report in
    /// <see cref="OdfDocument.LastVersionCompatibilityReport"/> after the save pipeline runs.
    /// 若轉換安全，回呼仍會收到不含問題的報告；儲存管線執行後，文件也會將報告保留於
    /// <see cref="OdfDocument.LastVersionCompatibilityReport"/>。
    /// </remarks>
    public Action<OdfVersionCompatibilityReport>? VersionCompatibilityReportHandler { get; set; }

    /// <summary>
    /// Gets or sets the temporary directory used by atomic save operations.
    /// 取得或設定原子化儲存作業使用的暫存目錄。
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the system temporary directory is used.
    /// 若為 <see langword="null"/>，則使用系統暫存目錄。
    /// </remarks>
    public string? TemporaryDirectory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether unreferenced <c>Pictures/</c> media entries are removed on save.
    /// 取得或設定儲存時是否自動移除未被目前 DOM 參照的 <c>Pictures/</c> 媒體專案。
    /// </summary>
    /// <remarks>
    /// 此選項只影響高階 <see cref="OdfDocument"/> 儲存管線；直接使用 <see cref="OdfPackage"/> 儲存時，
    /// 請改用 <see cref="OdfPackage.PruneUnusedMedia(IEnumerable{string})"/> 手動傳入目前文件實際參照的媒體路徑清單。
    /// </remarks>
    public bool PruneUnusedMedia { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether fonts referenced by the document are embedded on save.
    /// 取得或設定儲存時是否內嵌文件參照的字型。
    /// </summary>
    public bool EmbedUsedFonts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether spreadsheet formulas are evaluated before saving.
    /// 取得或設定儲存前是否計算試算表公式。
    /// </summary>
    public bool EvaluateFormulasOnSave { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Direct I/O is used for uncached package writes.
    /// 取得或設定是否啟用 Direct I/O 進行非快取封裝寫入。
    /// </summary>
    public bool EnableDirectIo { get; set; }

    /// <summary>
    /// Gets or sets the password used to encrypt the saved ODF document.
    /// 取得或設定用於加密已儲存 ODF 文件的密碼。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the cryptography provider used to encrypt package entries.
    /// 取得或設定用於加密封裝項目的密碼學提供者。
    /// </summary>
    public IOdfCryptographyProvider? CryptographyProvider { get; set; }

    /// <summary>
    /// Gets or sets the OpenPGP key provider used for ODF 1.3 package encryption.
    /// 取得或設定用於加密 ODF 1.3 OpenPGP 文件的金鑰提供者。
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
    /// Gets the OpenPGP recipients supplied to the encryption provider.
    /// 取得提供給加密提供者的 OpenPGP 收件者描述。
    /// </summary>
    public IList<OdfOpenPgpRecipient> OpenPgpRecipients { get; } = [];

    /// <summary>
    /// Gets a new instance with the default save settings.
    /// 取得使用預設儲存設定的新執行個體。
    /// </summary>
    public static OdfSaveOptions Default => new();
}


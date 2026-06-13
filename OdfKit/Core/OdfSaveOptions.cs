#pragma warning restore CS1591

using System.Globalization;
using System.IO.Compression;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 加密文件時使用的對稱加密演算法。
/// </summary>
public enum OdfEncryptionAlgorithm
{
    /// <summary>AES-256 加密演算法</summary>
    Aes256,

    /// <summary>Blowfish 加密演算法</summary>
    Blowfish,

    /// <summary>OpenPGP 加密，須搭配自訂密碼學提供者</summary>
    OpenPgp
}

/// <summary>
/// 提供儲存 ODF 文件時的組態選項。
/// </summary>
public class OdfSaveOptions
{
    /// <summary>
    /// 取得或設定 ZIP 檔案壓縮等級。
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// 取得或設定加密文件時使用的對稱加密演算法。預設為 ODF 1.3 標準的 AES-256。
    /// </summary>
    public OdfEncryptionAlgorithm EncryptionAlgorithm { get; set; } = OdfEncryptionAlgorithm.Aes256;

    /// <summary>
    /// 取得或設定文件文化語系設定。
    /// </summary>
    /// <remarks>
    /// 用於在轉譯貨幣、日期時間等格式化字串時進行本地化識別（如貨幣符號、日期排列順序）。
    /// 預設為目前執行緒的 Culture 。而底層 XML 的 float/double 與日期序列化一律維持 Culture-Invariant 。
    /// </remarks>
    public CultureInfo DocumentCulture { get; set; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// 取得或設定是否排版 XML （Indent XML）以利於偵錯。預設為 <see langword="false"/> （緊湊格式輸出，效能最佳）。
    /// </summary>
    public bool IndentXml { get; set; } = false;

    /// <summary>
    /// 取得或設定是否裝載確定性輸出（Deterministic Save）。
    /// </summary>
    /// <remarks>
    /// 啟用時，將所有 ZIP 封裝項目的 LastWriteTime 設為固定值（2026-01-01T00:00:00Z），
    /// 確保內容不變時，產出的二進位 ZIP 雜湊值（MD5/SHA256）完全相同。
    /// </remarks>
    public bool Deterministic { get; set; } = false;

    /// <summary>
    /// 取得或設定儲存時要強制寫入的 ODF 版本。若為 <see langword="null"/>，則保留文件目前宣告的版本。
    /// </summary>
    public OdfVersion? ForceVersion { get; set; }

    /// <summary>
    /// 取得或設定自訂原子化儲存的磁碟暫存路徑。
    /// </summary>
    /// <remarks>
    /// 若為 <see langword="null"/> ，則預設使用系統暫存目錄（對小於 50MB 檔案，系統會優先於記憶體中重構以防權限不足）。
    /// </remarks>
    public string? TemporaryDirectory { get; set; }

    /// <summary>
    /// 取得或設定是否在儲存時自動清理未被 DOM 節點參照的 Pictures 媒體檔案。預設為 <see langword="true"/> 。
    /// </summary>
    public bool PruneUnusedMedia { get; set; } = true;

    /// <summary>
    /// 取得或設定一個值，指出是否在儲存時於文件中內嵌所使用的字型。預設為 <see langword="false"/> 。
    /// </summary>
    public bool EmbedUsedFonts { get; set; } = false;

    /// <summary>
    /// 取得或設定一個值，指出是否在儲存時計算文件中的公式。預設為 <see langword="false"/> 。
    /// </summary>
    public bool EvaluateFormulasOnSave { get; set; } = false;

    /// <summary>
    /// 取得或設定用於加密 ODF 文件的密碼。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 取得或設定自訂的密碼學提供者，用於加密文件項目。
    /// </summary>
    public IOdfCryptographyProvider? CryptographyProvider { get; set; }

    /// <summary>
    /// 取得 OpenPGP 加密收件者描述，供自訂密碼學提供者使用。
    /// </summary>
    public IList<OdfOpenPgpRecipient> OpenPgpRecipients { get; } = [];

    /// <summary>
    /// 取得預設的儲存選項執行個體。
    /// </summary>
    public static OdfSaveOptions Default => new();
}


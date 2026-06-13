#pragma warning restore CS1591

namespace OdfKit.Core;

/// <summary>
/// 提供載入 ODF 文件時的組態選項。
/// </summary>
public class OdfLoadOptions
{
    /// <summary>
    /// 取得或設定是否啟用嚴格 XML 解析模式（Strict XML Parsing）。
    /// </summary>
    /// <remarks>
    /// 設為 <see langword="true"/> 時在 XML 解析錯誤或結構不合規時立即拋出例外；設為 <see langword="false"/> （預設，Lax 容錯模式）則在遇到損毀或非標準 ODF 時自動進行容錯與修復。
    /// </remarks>
    public bool StrictXmlParsing { get; set; } = false;

    /// <summary>
    /// 取得或設定是否在載入時驗證 ZIP 最前方的 mimetype 檔案內容符合 ODF 規範。
    /// </summary>
    public bool ValidateMimeType { get; set; } = true;

    /// <summary>
    /// 取得或設定 ZIP 封裝中的最大項目（Entries）數量限制（防禦 Zip DoS）。
    /// </summary>
    public int MaxZipEntries { get; set; } = 5000;

    /// <summary>
    /// 取得或設定單個項目解壓後的最大位元組數限制（預設 500MB ，防禦 Zip Bomb）。
    /// </summary>
    public long MaxEntrySize { get; set; } = 500 * 1024 * 1024;

    /// <summary>
    /// 取得或設定整個 ZIP 封裝解壓後的總位元組數限制（預設 1GB ，防禦 Zip Bomb）。
    /// </summary>
    public long MaxTotalUncompressedSize { get; set; } = 1024 * 1024 * 1024;

    /// <summary>
    /// 取得或設定用於解密加密 ODF 文件的密碼。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 取得或設定自訂的密碼學提供者，用於解密文件項目。
    /// </summary>
    public IOdfCryptographyProvider? CryptographyProvider { get; set; }

    /// <summary>
    /// 取得預設的載入選項執行個體。
    /// </summary>
    public static OdfLoadOptions Default => new();
}

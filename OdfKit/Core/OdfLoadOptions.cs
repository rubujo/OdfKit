namespace OdfKit.Core
{
    public class OdfLoadOptions
    {
        /// <summary>
        /// 是否啟用嚴格 XML 解析模式（Strict XML Parsing）。
        /// 設為 true 時在 XML 解析錯誤或結構不合規時立即拋出例外；設為 false（預設，Lax 容錯模式）則在遇到損毀或非標準 ODF 時自動進行容錯與修復。
        /// </summary>
        public bool StrictXmlParsing { get; set; } = false;

        /// <summary>
        /// 是否在載入時驗證 ZIP 最前方的 mimetype 檔案內容符合 ODF 規範。
        /// </summary>
        public bool ValidateMimeType { get; set; } = true;

        /// <summary>
        /// Zip 封裝中的最大 Entries 數量限制（防禦 Zip DoS）。
        /// </summary>
        public int MaxZipEntries { get; set; } = 5000;

        /// <summary>
        /// 單個 Entry 解壓後的最大位元組數限制（預設 500MB，防禦 Zip Bomb）。
        /// </summary>
        public long MaxEntrySize { get; set; } = 500 * 1024 * 1024;

        /// <summary>
        /// 整個 ZIP 封裝解壓後的總位元組數限制（預設 1GB，防禦 Zip Bomb）。
        /// </summary>
        public long MaxTotalUncompressedSize { get; set; } = 1024 * 1024 * 1024;

        public string? Password { get; set; }

        public IOdfCryptographyProvider? CryptographyProvider { get; set; }

        public static OdfLoadOptions Default => new();
    }
}

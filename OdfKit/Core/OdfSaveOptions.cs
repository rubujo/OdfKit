using System.Globalization;
using System.IO.Compression;

namespace OdfKit.Core
{
    public enum OdfEncryptionAlgorithm
    {
        Aes256,
        Blowfish
    }

    public class OdfSaveOptions
    {
        /// <summary>
        /// ZIP 檔案壓縮等級。
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

        /// <summary>
        /// 加密文件時使用的對稱加密演算法。預設為 ODF 1.3 標準的 AES-256。
        /// </summary>
        public OdfEncryptionAlgorithm EncryptionAlgorithm { get; set; } = OdfEncryptionAlgorithm.Aes256;

        /// <summary>
        /// 文件文化語系設定。
        /// 用於在轉譯貨幣、日期時間等格式化字串時進行本地化識別（如貨幣符號、日期排列順序）。
        /// 預設為目前執行緒的 Culture。而底層 XML 的 float/double 與日期序列化一律維持 Culture-Invariant。
        /// </summary>
        public CultureInfo DocumentCulture { get; set; } = CultureInfo.CurrentCulture;

        /// <summary>
        /// 是否排版 XML (Indent XML) 以利於偵錯。預設為 false (緊湊格式輸出，效能最佳)。
        /// </summary>
        public bool IndentXml { get; set; } = false;

        /// <summary>
        /// 是否啟用確定性輸出（Deterministic Save）。
        /// 啟用時，將所有 ZIP 封裝項目的 LastWriteTime 設為固定值（2026-01-01T00:00:00Z），
        /// 確保內容不變時，產出的二進位 ZIP 雜湊值（MD5/SHA256）完全相同。
        /// </summary>
        public bool Deterministic { get; set; } = false;

        /// <summary>
        /// 自訂原子化儲存的磁碟暫存路徑。若為 null，則預設使用系統暫存目錄（對小於 50MB 檔案，系統會優先於記憶體中重構以防權限不足）。
        /// </summary>
        public string? TemporaryDirectory { get; set; }

        /// <summary>
        /// 是否在儲存時自動清理未被 DOM 節點參照的 Pictures 媒體檔案。預設為 true。
        /// </summary>
        public bool PruneUnusedMedia { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to embed used fonts in the document on save.
        /// </summary>
        public bool EmbedUsedFonts { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to evaluate formulas in the document on save.
        /// </summary>
        public bool EvaluateFormulasOnSave { get; set; } = false;

        public static OdfSaveOptions Default => new();
    }
}

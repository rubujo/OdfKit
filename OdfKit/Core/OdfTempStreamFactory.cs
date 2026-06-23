using System.IO;

namespace OdfKit.Core;

/// <summary>
/// 提供依預估大小選擇記憶體或暫存檔資料流的共用工廠（內部協作者）。
/// </summary>
internal static class OdfTempStreamFactory
{
    /// <summary>
    /// 預設的暫存檔門檻（位元組）；超過此大小時改用暫存檔而非記憶體，避免大型文件耗盡記憶體。
    /// </summary>
    internal const long DefaultThresholdBytes = 50L * 1024 * 1024;

    /// <summary>
    /// 依預估大小建立暫存資料流：小於門檻時使用記憶體資料流，否則改用隨關閉自動刪除的暫存檔資料流。
    /// </summary>
    /// <param name="estimatedSize">預估的內容大小（位元組）；無法預估時可傳入 <c>0</c> 或負數，將一律使用記憶體資料流</param>
    /// <param name="temporaryDirectory">暫存檔目錄；為 <see langword="null"/> 時使用系統暫存目錄</param>
    /// <param name="async">是否需要支援非同步讀寫的暫存檔（套用 <see cref="FileOptions.Asynchronous"/>）</param>
    /// <param name="thresholdBytes">暫存檔門檻（位元組），預設為 <see cref="DefaultThresholdBytes"/></param>
    /// <returns>記憶體或暫存檔資料流</returns>
    internal static Stream Create(long estimatedSize, string? temporaryDirectory, bool async = false, long thresholdBytes = DefaultThresholdBytes)
    {
        if (estimatedSize < thresholdBytes)
        {
            return new MemoryStream();
        }

        string tempDir = temporaryDirectory ?? Path.GetTempPath();
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        string tempFilePath = Path.Combine(tempDir, "odfkit_" + Path.GetRandomFileName());
        FileOptions options = FileOptions.DeleteOnClose;
        if (async)
        {
            options |= FileOptions.Asynchronous;
        }

        return new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, options);
    }
}

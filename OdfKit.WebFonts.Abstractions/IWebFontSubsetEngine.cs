namespace OdfKit.WebFonts;

/// <summary>
/// Generates bounded WebFont subsets from registered font sources.
/// 從已註冊的字型來源產生有界 WebFont 子集。
/// </summary>
public interface IWebFontSubsetEngine
{
    /// <summary>
    /// Generates immutable assets in a trusted destination directory.
    /// 在受信任的目的目錄產生不可變資產。
    /// </summary>
    /// <param name="request">The canonical subset request. / 標準化的子集要求。</param>
    /// <param name="destinationDirectory">The trusted destination directory. / 受信任的目的目錄。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The generated manifest. / 產生的 manifest。</returns>
    Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default);
}

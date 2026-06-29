namespace OdfKit.Text;

/// <summary>
/// Optimizes a single media item during media compaction.
/// 提供單一媒體項目的最佳化委派。
/// </summary>
/// <param name="request">The media optimization request. / 媒體最佳化請求。</param>
/// <returns>The optimized new media; returning <see langword="null"/> keeps the original media. / 最佳化後的新媒體；若回傳 <see langword="null"/> 則保留原始媒體。</returns>
public delegate OdfOptimizedMedia? OdfMediaOptimizer(OdfMediaOptimizationRequest request);

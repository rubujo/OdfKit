namespace OdfKit.Text;

/// <summary>
/// 提供單一媒體項目的最佳化委派。
/// </summary>
/// <param name="request">媒體最佳化請求</param>
/// <returns>最佳化後的新媒體；若回傳 <see langword="null"/> 則保留原始媒體</returns>
public delegate OdfOptimizedMedia? OdfMediaOptimizer(OdfMediaOptimizationRequest request);

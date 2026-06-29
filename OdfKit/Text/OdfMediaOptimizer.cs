namespace OdfKit.Text;

/// <summary>
/// Provides APIs for odf media optimizer.
/// 提供單一媒體項目的最佳化委派。
/// </summary>
/// <param name="request">The value to use. / 媒體最佳化請求</param>
/// <returns>The result. / 最佳化後的新媒體；若回傳 <see langword="null"/> 則保留原始媒體</returns>
public delegate OdfOptimizedMedia? OdfMediaOptimizer(OdfMediaOptimizationRequest request);

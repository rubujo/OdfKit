namespace OdfKit.Text;

/// <summary>
/// Represents odf optimized media.
/// 表示媒體最佳化後的新內容。
/// </summary>
/// <param name="Bytes">The value to use. / 最佳化後的媒體內容</param>
/// <param name="MediaType">The value to use. / 最佳化後的媒體類型</param>
/// <param name="Extension">The value to use. / 最佳化後建議使用的副檔名，包含前導句點</param>
public sealed record OdfOptimizedMedia(byte[] Bytes, string MediaType, string Extension);

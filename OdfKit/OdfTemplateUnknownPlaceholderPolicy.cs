namespace OdfKit;

/// <summary>
/// Defines how unresolved template placeholders are handled during binding.
/// 定義模板繫結期間如何處理未解析占位符。
/// </summary>
public enum OdfTemplateUnknownPlaceholderPolicy
{
    /// <summary>
    /// Keeps the original placeholder text.
    /// 保留原始占位符文字。
    /// </summary>
    Keep,

    /// <summary>
    /// Replaces unresolved placeholders with an empty string.
    /// 以空字串取代未解析占位符。
    /// </summary>
    EmptyString
}

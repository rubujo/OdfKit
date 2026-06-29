using System.Collections.Generic;

namespace OdfKit.Image;

/// <summary>
/// Represents the filter/effect settings of an image frame.
/// 表示影像框架的濾鏡／特效設定。
/// </summary>
/// <param name="filterName">The filter name. / 濾鏡名稱。</param>
/// <param name="parameters">The filter parameter key-value collection. / 濾鏡參數鍵值集合。</param>
public sealed class OdfImageFilterInfo(string filterName, IReadOnlyDictionary<string, string>? parameters = null)
{
    /// <summary>
    /// Gets the filter name.
    /// 取得濾鏡名稱。
    /// </summary>
    public string FilterName { get; } = filterName;

    /// <summary>
    /// Gets the filter parameter key-value collection.
    /// 取得濾鏡參數鍵值集合。
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; } = parameters ?? new Dictionary<string, string>();
}

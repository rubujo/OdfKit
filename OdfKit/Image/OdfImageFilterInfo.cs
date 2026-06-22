using System.Collections.Generic;

namespace OdfKit.Image;

/// <summary>
/// 表示影像框架的濾鏡／特效設定。
/// </summary>
/// <param name="filterName">濾鏡名稱。</param>
/// <param name="parameters">濾鏡參數鍵值集合。</param>
public sealed class OdfImageFilterInfo(string filterName, IReadOnlyDictionary<string, string>? parameters = null)
{
    /// <summary>
    /// 取得濾鏡名稱。
    /// </summary>
    public string FilterName { get; } = filterName;

    /// <summary>
    /// 取得濾鏡參數鍵值集合。
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; } = parameters ?? new Dictionary<string, string>();
}

using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf index mark info.
/// 表示索引標記的資訊。
/// </summary>
/// <param name="term">The value to use. / 索引詞彙</param>
/// <param name="key1">The name or identifier. / 主要鍵值</param>
/// <param name="key2">The name or identifier. / 次要鍵值</param>
public class OdfIndexMarkInfo(string term, string? key1, string? key2)
{
    /// <summary>
    /// Gets term.
    /// 取得索引詞彙。
    /// </summary>
    public string Term { get; } = term;

    /// <summary>
    /// Gets key1.
    /// 取得主要鍵值。
    /// </summary>
    public string? Key1 { get; } = key1;

    /// <summary>
    /// Gets key2.
    /// 取得次要鍵值。
    /// </summary>
    public string? Key2 { get; } = key2;
}


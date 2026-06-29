using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents summary information for an index mark.
/// 表示索引標記的資訊。
/// </summary>
/// <param name="term">The index term. / 索引詞彙。</param>
/// <param name="key1">The primary key. / 主要鍵值。</param>
/// <param name="key2">The secondary key. / 次要鍵值。</param>
public class OdfIndexMarkInfo(string term, string? key1, string? key2)
{
    /// <summary>
    /// Gets the index term.
    /// 取得索引詞彙。
    /// </summary>
    public string Term { get; } = term;

    /// <summary>
    /// Gets the primary key.
    /// 取得主要鍵值。
    /// </summary>
    public string? Key1 { get; } = key1;

    /// <summary>
    /// Gets the secondary key.
    /// 取得次要鍵值。
    /// </summary>
    public string? Key2 { get; } = key2;
}


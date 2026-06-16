using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示索引標記的資訊。
/// </summary>
/// <param name="term">索引詞彙</param>
/// <param name="key1">主要鍵值</param>
/// <param name="key2">次要鍵值</param>
public class OdfIndexMarkInfo(string term, string? key1, string? key2)
{
    /// <summary>
    /// 取得索引詞彙。
    /// </summary>
    public string Term { get; } = term;

    /// <summary>
    /// 取得主要鍵值。
    /// </summary>
    public string? Key1 { get; } = key1;

    /// <summary>
    /// 取得次要鍵值。
    /// </summary>
    public string? Key2 { get; } = key2;
}


using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents information for a bibliography index mark.
/// 表示文獻目錄標記的資訊。
/// </summary>
/// <param name="id">The identifier. / 識別碼。</param>
/// <param name="type">The bibliography type. / 文獻類型。</param>
/// <param name="meta">The attribute metadata dictionary. / 屬性詮釋資料字典。</param>
public class OdfBibliographyMarkInfo(string id, string type, Dictionary<string, string> meta)
{
    /// <summary>
    /// Gets the bibliography entry identifier.
    /// 取得識別碼。
    /// </summary>
    public string Identifier { get; } = id;

    /// <summary>
    /// Gets the bibliography type.
    /// 取得文獻類型。
    /// </summary>
    public string Type { get; } = type;

    /// <summary>
    /// Gets the attribute metadata dictionary.
    /// 取得屬性詮釋資料字典。
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = meta;
}

using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf bibliography mark info.
/// 表示文獻目錄標記的資訊。
/// </summary>
/// <param name="id">The name or identifier. / 識別碼</param>
/// <param name="type">The value to use. / 文獻類型</param>
/// <param name="meta">The value to use. / 屬性詮釋資料字典</param>
public class OdfBibliographyMarkInfo(string id, string type, Dictionary<string, string> meta)
{
    /// <summary>
    /// Gets identifier.
    /// 取得識別碼。
    /// </summary>
    public string Identifier { get; } = id;

    /// <summary>
    /// Gets type.
    /// 取得文獻類型。
    /// </summary>
    public string Type { get; } = type;

    /// <summary>
    /// Gets metadata.
    /// 取得屬性詮釋資料字典。
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = meta;
}


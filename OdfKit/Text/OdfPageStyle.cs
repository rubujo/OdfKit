using System;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents a named page style (master-page) in a text document.
/// 代表文字文件中的一個具名頁面樣式（master-page）。
/// </summary>
public sealed class OdfPageStyle
{
    /// <summary>
    /// Gets the master page style name.
    /// 取得主頁面樣式名稱。
    /// </summary>
    public string Name { get; }

    internal OdfPageStyle(string name) { Name = name; }
}

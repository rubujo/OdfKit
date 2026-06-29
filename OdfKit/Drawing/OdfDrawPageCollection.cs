using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// Provides indexing, enumeration, and add entry points for drawing pages.
/// 提供繪圖頁面的索引、列舉與新增入口。
/// </summary>
public sealed class OdfDrawPageCollection : IReadOnlyList<OdfDrawPage>
{
    private readonly DrawingDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfDrawPageCollection"/> class.
    /// 初始化 <see cref="OdfDrawPageCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning drawing document. / 所屬繪圖文件。</param>
    public OdfDrawPageCollection(DrawingDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets the number of drawing pages.
    /// 取得繪圖頁面數量。
    /// </summary>
    public int Count => _document.GetPagesSnapshot().Count;

    /// <summary>
    /// Gets the drawing page at the specified index.
    /// 依索引取得繪圖頁面。
    /// </summary>
    /// <param name="index">The zero-based page index. / 以 0 為基準的頁面索引。</param>
    /// <returns>The specified drawing page. / 指定的繪圖頁面。</returns>
    public OdfDrawPage this[int index] => _document.GetPagesSnapshot()[index];

    /// <summary>
    /// Adds a drawing page.
    /// 新增繪圖頁面。
    /// </summary>
    /// <param name="name">The optional page name. / 選用的頁面名稱。</param>
    /// <returns>The newly added drawing page. / 新增完成的繪圖頁面。</returns>
    public OdfDrawPage Add(string? name = null)
    {
        return _document.AddPage(name);
    }

    /// <summary>
    /// Gets an enumerator for the drawing pages.
    /// 取得繪圖頁面列舉器。
    /// </summary>
    /// <returns>The drawing page enumerator. / 繪圖頁面列舉器。</returns>
    public IEnumerator<OdfDrawPage> GetEnumerator()
    {
        return _document.GetPagesSnapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}


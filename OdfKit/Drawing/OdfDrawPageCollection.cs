using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// 提供繪圖頁面的索引、列舉與新增入口。
/// </summary>
public sealed class OdfDrawPageCollection : IReadOnlyList<OdfDrawPage>
{
    private readonly DrawingDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfDrawPageCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬繪圖文件</param>
    public OdfDrawPageCollection(DrawingDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得繪圖頁面數量。
    /// </summary>
    public int Count => _document.GetPagesSnapshot().Count;

    /// <summary>
    /// 依索引取得繪圖頁面。
    /// </summary>
    /// <param name="index">以 0 為基準的頁面索引</param>
    /// <returns>指定的繪圖頁面</returns>
    public OdfDrawPage this[int index] => _document.GetPagesSnapshot()[index];

    /// <summary>
    /// 新增繪圖頁面。
    /// </summary>
    /// <param name="name">選用的頁面名稱</param>
    /// <returns>新增完成的繪圖頁面</returns>
    public OdfDrawPage Add(string? name = null)
    {
        return _document.AddPage(name);
    }

    /// <summary>
    /// 取得繪圖頁面列舉器。
    /// </summary>
    /// <returns>繪圖頁面列舉器</returns>
    public IEnumerator<OdfDrawPage> GetEnumerator()
    {
        return _document.GetPagesSnapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}


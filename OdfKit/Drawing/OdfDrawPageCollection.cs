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
    /// Short overload of Add that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：Add 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfDrawPage Add() => Add(null);


    /// <summary>
    /// Adds a drawing page.
    /// 新增繪圖頁面。
    /// </summary>
    /// <param name="name">The optional page name. / 選用的頁面名稱。</param>
    /// <returns>The newly added drawing page. / 新增完成的繪圖頁面。</returns>
    public OdfDrawPage Add(string? name)
    {
        return _document.AddPage(name);
    }

    /// <summary>
    /// Finds a drawing page by name.
    /// 依名稱查找繪圖頁面。
    /// </summary>
    /// <param name="name">The drawing page name. / 繪圖頁面名稱。</param>
    /// <returns>The matching page, or <see langword="null"/> when no match exists. / 符合的頁面；若找不到則為 <see langword="null"/>。</returns>
    public OdfDrawPage? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(null, nameof(name));
        }
        return _document.GetPagesSnapshot().FirstOrDefault(
            page => string.Equals(page.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Removes the specified drawing page from this collection.
    /// 從此集合移除指定繪圖頁面。
    /// </summary>
    /// <param name="page">The drawing page to remove. / 要移除的繪圖頁面。</param>
    /// <returns><see langword="true"/> if the page was removed; otherwise, <see langword="false"/>. / 若已移除頁面則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool Remove(OdfDrawPage page) => _document.RemovePage(page);

    /// <summary>
    /// Removes the drawing page at the specified index.
    /// 移除指定索引的繪圖頁面。
    /// </summary>
    /// <param name="index">The zero-based page index. / 採 0 為基準的頁面索引。</param>
    public void RemoveAt(int index) => _document.RemovePage(index);


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


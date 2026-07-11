using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Provides indexing, enumeration, and creation entry points for presentation slides.
/// 提供簡報投影片的索引、列舉與新增入口。
/// </summary>
public sealed class OdfSlideCollection : IReadOnlyList<OdfSlide>
{
    private readonly PresentationDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSlideCollection"/> class.
    /// 初始化 <see cref="OdfSlideCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning presentation document. / 所屬簡報文件。</param>
    public OdfSlideCollection(PresentationDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets the slide count.
    /// 取得投影片數量。
    /// </summary>
    public int Count => _document.GetSlidesSnapshot().Count;

    /// <summary>
    /// Gets a slide by index.
    /// 依索引取得投影片。
    /// </summary>
    /// <param name="index">The zero-based slide index. / 採 0 為基準的投影片索引。</param>
    /// <returns>The specified slide. / 指定投影片。</returns>
    public OdfSlide this[int index] => _document.GetSlidesSnapshot()[index];
    /// <summary>
    /// Short overload of Add that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：Add 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfSlide Add() => Add(null);


    /// <summary>
    /// Adds a slide.
    /// 新增投影片。
    /// </summary>
    /// <param name="name">The optional slide name. / 選用的投影片名稱。</param>
    /// <returns>The added slide. / 新增完成的投影片。</returns>
    public OdfSlide Add(string? name)
    {
        return _document.AddSlide(name);
    }

    /// <summary>
    /// Finds a slide by name.
    /// 依名稱查找投影片。
    /// </summary>
    /// <param name="name">The slide name. / 投影片名稱。</param>
    /// <returns>The matching slide, or <see langword="null"/> when no match exists. / 符合的投影片；若找不到則為 <see langword="null"/>。</returns>
    public OdfSlide? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(null, nameof(name));
        }
        return _document.GetSlidesSnapshot().FirstOrDefault(
            slide => string.Equals(slide.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Removes the specified slide from this collection.
    /// 從此集合移除指定投影片。
    /// </summary>
    /// <param name="slide">The slide to remove. / 要移除的投影片。</param>
    /// <returns><see langword="true"/> if the slide was removed; otherwise, <see langword="false"/>. / 若已移除投影片則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool Remove(OdfSlide slide) => _document.RemoveSlide(slide);

    /// <summary>
    /// Removes the slide at the specified index.
    /// 移除指定索引的投影片。
    /// </summary>
    /// <param name="index">The zero-based slide index. / 採 0 為基準的投影片索引。</param>
    public void RemoveAt(int index) => _document.RemoveSlide(index);


    /// <summary>
    /// Gets the slide enumerator.
    /// 取得投影片列舉器。
    /// </summary>
    /// <returns>The slide enumerator. / 投影片列舉器。</returns>
    public IEnumerator<OdfSlide> GetEnumerator()
    {
        return _document.GetSlidesSnapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

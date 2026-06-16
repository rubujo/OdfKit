using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 提供簡報投影片的索引、列舉與新增入口。
/// </summary>
public sealed class OdfSlideCollection : IReadOnlyList<OdfSlide>
{
    private readonly PresentationDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfSlideCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬簡報文件。</param>
    public OdfSlideCollection(PresentationDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得投影片數量。
    /// </summary>
    public int Count => _document.GetSlidesSnapshot().Count;

    /// <summary>
    /// 依索引取得投影片。
    /// </summary>
    /// <param name="index">以 0 為基準的投影片索引。</param>
    /// <returns>指定投影片。</returns>
    public OdfSlide this[int index] => _document.GetSlidesSnapshot()[index];

    /// <summary>
    /// 新增投影片。
    /// </summary>
    /// <param name="name">選用的投影片名稱。</param>
    /// <returns>新增完成的投影片。</returns>
    public OdfSlide Add(string? name = null)
    {
        return _document.AddSlide(name);
    }

    /// <summary>
    /// 取得投影片列舉器。
    /// </summary>
    /// <returns>投影片列舉器。</returns>
    public IEnumerator<OdfSlide> GetEnumerator()
    {
        return _document.GetSlidesSnapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

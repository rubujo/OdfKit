using System;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf page header footer.
/// 表示主頁面樣式中的一個頁首或頁尾區域，支援純文字與欄位混排。
/// </summary>
public sealed class OdfPageHeaderFooter
{
    private readonly TextDocument _doc;
    private readonly OdfPageSetup _setup;
    private readonly string _localName;

    internal OdfPageHeaderFooter(TextDocument doc, OdfPageSetup setup, string localName)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _localName = localName ?? throw new ArgumentNullException(nameof(localName));
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定此區域的純文字內容；設定時會取代既有子節點。
    /// </summary>
    public string? Text
    {
        get => _setup.GetHeaderFooterRegionText(_localName);
        set => _setup.SetHeaderFooterRegionText(_localName, value);
    }

    /// <summary>
    /// Gets get or create paragraph.
    /// 取得或建立此區域的段落節點，供進階混排編輯。
    /// </summary>
    /// <returns>The result. / 可追加欄位或文字的段落物件</returns>
    public OdfParagraph GetOrCreateParagraph() =>
        _setup.GetOrCreateHeaderFooterParagraph(_localName);

    /// <summary>
    /// Removes clear.
    /// 清除此區域的所有內容。
    /// </summary>
    public void Clear() => _setup.SetHeaderFooterRegionText(_localName, null);

    /// <summary>
    /// Provides add page number field.
    /// 在段落尾端新增頁碼欄位。
    /// </summary>
    public void AddPageNumberField() => _doc.AddPageNumberField(GetOrCreateParagraph());

    /// <summary>
    /// Provides add page count field.
    /// 在段落尾端新增總頁數欄位。
    /// </summary>
    public void AddPageCountField() => _doc.AddPageCountField(GetOrCreateParagraph());

    /// <summary>
    /// Provides add date field.
    /// 在段落尾端新增日期欄位。
    /// </summary>
    public void AddDateField() => _doc.AddDateField(GetOrCreateParagraph());
}

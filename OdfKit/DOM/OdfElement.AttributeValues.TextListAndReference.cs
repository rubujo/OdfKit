using System;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values - Text List & Reference

    /// <summary>
    /// 取得具有 schema awareness 的文字清單標籤後接屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字清單標籤後接設定；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextLabelFollowedBy? GetTextLabelFollowedByAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextLabelFollowedBy(value, out OdfTextLabelFollowedBy labelFollowedBy) ? labelFollowedBy : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字清單標籤後接屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字清單標籤後接設定。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextLabelFollowedByAttributeValue(string localName, string namespaceUri, OdfTextLabelFollowedBy value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextLabelFollowedBy(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字清單層級定位模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字清單層級定位模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextListLevelPositionMode? GetTextListLevelPositionModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextListLevelPositionMode(value, out OdfTextListLevelPositionMode mode) ? mode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字清單層級定位模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字清單層級定位模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextListLevelPositionModeAttributeValue(string localName, string namespaceUri, OdfTextListLevelPositionMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextListLevelPositionMode(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字索引範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字索引範圍；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextIndexScope? GetTextIndexScopeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextIndexScope(value, out OdfTextIndexScope scope) ? scope : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字索引範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字索引範圍。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextIndexScopeAttributeValue(string localName, string namespaceUri, OdfTextIndexScope value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextIndexScope(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字資料表來源類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字資料表來源類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextTableType? GetTextTableTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextTableType(value, out OdfTextTableType tableType) ? tableType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字資料表來源類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字資料表來源類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextTableTypeAttributeValue(string localName, string namespaceUri, OdfTextTableType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextTableType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字錨定類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字錨定類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextAnchorType? GetTextAnchorTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextAnchorType(value, out OdfTextAnchorType anchorType) ? anchorType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字錨定類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字錨定類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextAnchorTypeAttributeValue(string localName, string namespaceUri, OdfTextAnchorType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextAnchorType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字註解類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字註解類別；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextNoteClass? GetTextNoteClassAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextNoteClass(value, out OdfTextNoteClass noteClass) ? noteClass : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字註解類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字註解類別。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextNoteClassAttributeValue(string localName, string namespaceUri, OdfTextNoteClass value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextNoteClass(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字頁面選取屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字頁面選取；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextSelectPage? GetTextSelectPageAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextSelectPage(value, out OdfTextSelectPage selectPage) ? selectPage : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字頁面選取屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字頁面選取。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextSelectPageAttributeValue(string localName, string namespaceUri, OdfTextSelectPage value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextSelectPage(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字參照格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字參照格式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextReferenceFormat? GetTextReferenceFormatAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextReferenceFormat(value, out OdfTextReferenceFormat format) ? format : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字參照格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字參照格式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextReferenceFormatAttributeValue(string localName, string namespaceUri, OdfTextReferenceFormat value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextReferenceFormat(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字起始編號範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字起始編號範圍；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextStartNumberingAt? GetTextStartNumberingAtAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextStartNumberingAt(value, out OdfTextStartNumberingAt startNumberingAt) ? startNumberingAt : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字起始編號範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字起始編號範圍。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextStartNumberingAtAttributeValue(string localName, string namespaceUri, OdfTextStartNumberingAt value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextStartNumberingAt(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字註腳位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字註腳位置；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextFootnotesPosition? GetTextFootnotesPositionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextFootnotesPosition(value, out OdfTextFootnotesPosition position) ? position : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字註腳位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字註腳位置。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextFootnotesPositionAttributeValue(string localName, string namespaceUri, OdfTextFootnotesPosition value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextFootnotesPosition(value), prefix, version);
    }


    #endregion
}

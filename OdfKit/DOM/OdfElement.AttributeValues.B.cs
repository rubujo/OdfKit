using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values (B)

    /// <summary>
    /// 取得具有 schema awareness 的繪圖無連結屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖無連結；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawNoHref? GetDrawNoHrefAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDrawNoHref noHref) ? noHref : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖無連結屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖無連結。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawNoHrefAttributeValue(string localName, string namespaceUri, OdfDrawNoHref value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 繪圖無連結。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格彙總函式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格彙總函式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableFunction? GetTableFunctionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfTableFunction function) ? function : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格彙總函式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格彙總函式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableFunctionAttributeValue(string localName, string namespaceUri, OdfTableFunction value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 表格彙總函式。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的繪圖線條接合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖線條接合；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawStrokeLineJoin? GetDrawStrokeLineJoinAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDrawStrokeLineJoin lineJoin) ? lineJoin : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖線條接合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖線條接合。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawStrokeLineJoinAttributeValue(string localName, string namespaceUri, OdfDrawStrokeLineJoin value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 繪圖線條接合。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 SVG 線端樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 SVG 線端樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfSvgStrokeLineCap? GetSvgStrokeLineCapAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfSvgStrokeLineCap lineCap) ? lineCap : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 SVG 線端樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 SVG 線端樣式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetSvgStrokeLineCapAttributeValue(string localName, string namespaceUri, OdfSvgStrokeLineCap value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF SVG 線端樣式。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 FO 分頁保持屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 FO 分頁保持設定；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFoKeepTogether? GetFoKeepTogetherAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFoKeepTogether(value, out OdfFoKeepTogether keepTogether) ? keepTogether : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 FO 分頁保持屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 FO 分頁保持設定。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFoKeepTogetherAttributeValue(string localName, string namespaceUri, OdfFoKeepTogether value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFoKeepTogether(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 FO 換行選項屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 FO 換行選項；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFoWrapOption? GetFoWrapOptionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFoWrapOption(value, out OdfFoWrapOption wrapOption) ? wrapOption : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 FO 換行選項屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 FO 換行選項。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFoWrapOptionAttributeValue(string localName, string namespaceUri, OdfFoWrapOption value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFoWrapOption(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 3D 投影屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 3D 投影；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDr3dProjection? GetDr3dProjectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseDr3dProjection(value, out OdfDr3dProjection projection) ? projection : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 3D 投影屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 3D 投影。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDr3dProjectionAttributeValue(string localName, string namespaceUri, OdfDr3dProjection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatDr3dProjection(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 3D 著色模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 3D 著色模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDr3dShadeMode? GetDr3dShadeModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseDr3dShadeMode(value, out OdfDr3dShadeMode shadeMode) ? shadeMode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 3D 著色模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 3D 著色模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDr3dShadeModeAttributeValue(string localName, string namespaceUri, OdfDr3dShadeMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatDr3dShadeMode(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 SVG 填滿規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 SVG 填滿規則；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfSvgFillRule? GetSvgFillRuleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseSvgFillRule(value, out OdfSvgFillRule fillRule) ? fillRule : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 SVG 填滿規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 SVG 填滿規則。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetSvgFillRuleAttributeValue(string localName, string namespaceUri, OdfSvgFillRule value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatSvgFillRule(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格邊框模型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格邊框模型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableBorderModel? GetTableBorderModelAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTableBorderModel(value, out OdfTableBorderModel borderModel) ? borderModel : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格邊框模型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格邊框模型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableBorderModelAttributeValue(string localName, string namespaceUri, OdfTableBorderModel value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTableBorderModel(value), prefix, version);
    }

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

    /// <summary>
    /// 取得具有 schema awareness 的文字標號序列格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字標號序列格式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextCaptionSequenceFormat? GetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextCaptionSequenceFormat(value, out OdfTextCaptionSequenceFormat format) ? format : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字標號序列格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字標號序列格式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfTextCaptionSequenceFormat value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextCaptionSequenceFormat(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字編號位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字編號位置；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextNumberPosition? GetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextNumberPosition(value, out OdfTextNumberPosition position) ? position : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字編號位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字編號位置。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfTextNumberPosition value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextNumberPosition(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字預留位置類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字預留位置類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextPlaceholderType? GetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextPlaceholderType(value, out OdfTextPlaceholderType placeholderType) ? placeholderType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字預留位置類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字預留位置類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfTextPlaceholderType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextPlaceholderType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字動畫屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字動畫；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextAnimation? GetTextAnimationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextAnimation(value, out OdfTextAnimation animation) ? animation : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字動畫屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字動畫。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextAnimationAttributeValue(string localName, string namespaceUri, OdfTextAnimation value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextAnimation(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字動畫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字動畫方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextAnimationDirection? GetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextAnimationDirection(value, out OdfTextAnimationDirection direction) ? direction : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字動畫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字動畫方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfTextAnimationDirection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextAnimationDirection(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字索引項目種類屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字索引項目種類；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextKind? GetTextKindAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextKind(value, out OdfTextKind kind) ? kind : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字索引項目種類屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字索引項目種類。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextKindAttributeValue(string localName, string namespaceUri, OdfTextKind value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextKind(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 0 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的百分比；若屬性不存在或超出 0 到 100 範圍則為 <see langword="null"/>。</returns>
    public OdfPercent? GetPercentAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPercent.TryParse(value, out OdfPercent percent) ? percent : null;
    }

    /// <summary>
    /// 取得具有 schema awareness 的 -100 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的百分比；若屬性不存在或超出 -100 到 100 範圍則為 <see langword="null"/>。</returns>
    public OdfPercent? GetSignedPercentAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPercent.TryParse(value, allowNegative: true, out OdfPercent percent) ? percent : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 0 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的百分比。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <exception cref="ArgumentOutOfRangeException">當百分比值為負數時擲回。</exception>
    public void SetPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        if (value.Percent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value.Percent, "百分比值不可為負數。");
        }

        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 設定具有 schema awareness 的 -100 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的百分比。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetSignedPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的儲存格位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的儲存格位址；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCellAddressReference? GetCellAddressAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCellAddressReference.TryParse(value, out OdfCellAddressReference cellAddress) ? cellAddress : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的儲存格位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的儲存格位址。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCellAddressAttributeValue(string localName, string namespaceUri, OdfCellAddressReference value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的儲存格範圍位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的儲存格範圍位址；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCellRangeAddress? GetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCellRangeAddress.TryParse(value, out OdfCellRangeAddress cellRangeAddress) ? cellRangeAddress : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的儲存格範圍位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的儲存格範圍位址。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfCellRangeAddress value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的儲存格範圍位址清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的儲存格範圍位址清單；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCellRangeAddressList? GetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCellRangeAddressList.TryParse(value, out OdfCellRangeAddressList cellRangeAddressList) ? cellRangeAddressList : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的儲存格範圍位址清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的儲存格範圍位址清單。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfCellRangeAddressList value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的三維向量屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的三維向量；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfVector3D? GetVector3DAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfVector3D.TryParse(value, out OdfVector3D vector) ? vector : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的三維向量屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的三維向量。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetVector3DAttributeValue(string localName, string namespaceUri, OdfVector3D value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的三維點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的三維點；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfPoint3D? GetPoint3DAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPoint3D.TryParse(value, out OdfPoint3D point) ? point : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的三維點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的三維點。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPoint3DAttributeValue(string localName, string namespaceUri, OdfPoint3D value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的二維座標清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的二維座標清單；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfPointList? GetPointListAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPointList.TryParse(value, out OdfPointList pointList) ? pointList : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的二維座標清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的二維座標清單。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPointListAttributeValue(string localName, string namespaceUri, OdfPointList value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 XML 名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 XML 名稱；若屬性不存在或不是有效 XML <c>NCName</c> 則為 <see langword="null"/>。</returns>
    public OdfXmlName? GetXmlNameAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfXmlName.TryParse(value, out OdfXmlName xmlName) ? xmlName : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 XML 名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 XML 名稱。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetXmlNameAttributeValue(string localName, string namespaceUri, OdfXmlName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的語言代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的語言代碼；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfLanguageCode? GetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfLanguageCode.TryParse(value, out OdfLanguageCode languageCode) ? languageCode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的語言代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的語言代碼。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfLanguageCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的國別代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的國別代碼；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCountryCode? GetCountryCodeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCountryCode.TryParse(value, out OdfCountryCode countryCode) ? countryCode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的國別代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的國別代碼。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCountryCodeAttributeValue(string localName, string namespaceUri, OdfCountryCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字系統代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字系統代碼；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfScriptCode? GetScriptCodeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfScriptCode.TryParse(value, out OdfScriptCode scriptCode) ? scriptCode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字系統代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字系統代碼。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetScriptCodeAttributeValue(string localName, string namespaceUri, OdfScriptCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的語言標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的語言標記；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfLanguageTag? GetLanguageTagAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfLanguageTag.TryParse(value, out OdfLanguageTag languageTag) ? languageTag : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的語言標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的語言標記。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLanguageTagAttributeValue(string localName, string namespaceUri, OdfLanguageTag value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的命名空間 token 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的命名空間 token；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfNamespacedToken? GetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfNamespacedToken.TryParse(value, out OdfNamespacedToken namespacedToken) ? namespacedToken : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的命名空間 token 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的命名空間 token。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfNamespacedToken value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的單一字元屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的單一字元；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCharacter? GetCharacterAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCharacter.TryParse(value, out OdfCharacter character) ? character : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的單一字元屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的單一字元。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCharacterAttributeValue(string localName, string namespaceUri, OdfCharacter value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字編碼名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字編碼名稱；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfTextEncoding? GetTextEncodingAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfTextEncoding.TryParse(value, out OdfTextEncoding textEncoding) ? textEncoding : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字編碼名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字編碼名稱。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextEncodingAttributeValue(string localName, string namespaceUri, OdfTextEncoding value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的目標框架名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的目標框架名稱；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfTargetFrameName? GetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfTargetFrameName.TryParse(value, out OdfTargetFrameName targetFrameName) ? targetFrameName : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的目標框架名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的目標框架名稱。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfTargetFrameName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfLineStyle? GetLineStyleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseLineStyle(value, out OdfLineStyle lineStyle) ? lineStyle : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條樣式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineStyleAttributeValue(string localName, string namespaceUri, OdfLineStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatLineStyle(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfLineType? GetLineTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseLineType(value, out OdfLineType lineType) ? lineType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineTypeAttributeValue(string localName, string namespaceUri, OdfLineType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatLineType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條寬度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條寬度；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfLineWidth? GetLineWidthAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfLineWidth.TryParse(value, out OdfLineWidth lineWidth) ? lineWidth : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條寬度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條寬度。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineWidthAttributeValue(string localName, string namespaceUri, OdfLineWidth value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfLineMode? GetLineModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseLineMode(value, out OdfLineMode lineMode) ? lineMode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineModeAttributeValue(string localName, string namespaceUri, OdfLineMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatLineMode(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontStyle? GetFontStyleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontStyle(value, out OdfFontStyle fontStyle) ? fontStyle : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型樣式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontStyleAttributeValue(string localName, string namespaceUri, OdfFontStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontStyle(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型變體屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型變體；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontVariant? GetFontVariantAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontVariant(value, out OdfFontVariant fontVariant) ? fontVariant : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型變體屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型變體。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontVariantAttributeValue(string localName, string namespaceUri, OdfFontVariant value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontVariant(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型粗細屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型粗細；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontWeight? GetFontWeightAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontWeight(value, out OdfFontWeight fontWeight) ? fontWeight : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型粗細屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型粗細。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontWeightAttributeValue(string localName, string namespaceUri, OdfFontWeight value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontWeight(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的通用字型家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的通用字型家族；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontFamilyGeneric? GetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontFamilyGeneric(value, out OdfFontFamilyGeneric family) ? family : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的通用字型家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的通用字型家族。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfFontFamilyGeneric value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontFamilyGeneric(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型間距屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型間距；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontPitch? GetFontPitchAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontPitch(value, out OdfFontPitch pitch) ? pitch : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型間距屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型間距。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontPitchAttributeValue(string localName, string namespaceUri, OdfFontPitch value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontPitch(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型浮雕屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型浮雕；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontRelief? GetFontReliefAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontRelief(value, out OdfFontRelief relief) ? relief : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型浮雕屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型浮雕。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontReliefAttributeValue(string localName, string namespaceUri, OdfFontRelief value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontRelief(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型伸縮屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型伸縮；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontStretch? GetFontStretchAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontStretch(value, out OdfFontStretch stretch) ? stretch : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型伸縮屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型伸縮。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontStretchAttributeValue(string localName, string namespaceUri, OdfFontStretch value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontStretch(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的斷行規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的斷行規則；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleLineBreak? GetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleLineBreak(value, out OdfStyleLineBreak lineBreak) ? lineBreak : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的斷行規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的斷行規則。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfStyleLineBreak value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleLineBreak(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的背景重複屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的背景重複；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleRepeat? GetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleRepeat(value, out OdfStyleRepeat repeat) ? repeat : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的背景重複屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的背景重複。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfStyleRepeat value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleRepeat(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleDirection? GetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleDirection(value, out OdfStyleDirection direction) ? direction : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfStyleDirection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleDirection(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表單方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表單方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFormOrientation? GetFormOrientationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFormOrientation(value, out OdfFormOrientation orientation) ? orientation : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表單方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表單方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFormOrientationAttributeValue(string localName, string namespaceUri, OdfFormOrientation value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFormOrientation(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableDirection? GetTableDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTableDirection(value, out OdfTableDirection direction) ? direction : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableDirectionAttributeValue(string localName, string namespaceUri, OdfTableDirection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTableDirection(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格方位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格方位；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableOrientation? GetTableOrientationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTableOrientation(value, out OdfTableOrientation orientation) ? orientation : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格方位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格方位。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableOrientationAttributeValue(string localName, string namespaceUri, OdfTableOrientation value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTableOrientation(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式家族；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleFamily? GetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleFamily(value, out OdfStyleFamily family) ? family : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式家族。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfStyleFamily value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleFamily(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 ODF 版本屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 ODF 版本；若屬性不存在或不是已知版本則為 <see langword="null"/>。</returns>
    public OdfVersion? GetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfVersionInfo.TryParseVersionString(value, out OdfVersion parsed) ? parsed : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 ODF 版本屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 ODF 版本。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfVersionInfo.ToVersionString(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 MIME 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 MIME 類型；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfMediaType? GetMediaTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfMediaType.TryParse(value, out OdfMediaType mediaType) ? mediaType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 MIME 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 MIME 類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetMediaTypeAttributeValue(string localName, string namespaceUri, OdfMediaType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }


    #endregion
}

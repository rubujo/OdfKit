using System;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values - Draw, FO & Stroke

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
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDrawNoHref>(value);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖無連結。"), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableFunction>(value);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格彙總函式。"), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDrawStrokeLineJoin>(value);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖線條接合。"), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfSvgStrokeLineCap>(value);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF SVG 線端樣式。"), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFoKeepTogether>(value, OdfElementSchemaRegistry.TryParseFoKeepTogether);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFoKeepTogether(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFoWrapOption>(value, OdfElementSchemaRegistry.TryParseFoWrapOption);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFoWrapOption(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfDr3dProjection>(value, OdfElementSchemaRegistry.TryParseDr3dProjection);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatDr3dProjection(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfDr3dShadeMode>(value, OdfElementSchemaRegistry.TryParseDr3dShadeMode);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatDr3dShadeMode(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfSvgFillRule>(value, OdfElementSchemaRegistry.TryParseSvgFillRule);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatSvgFillRule(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableBorderModel>(value, OdfElementSchemaRegistry.TryParseTableBorderModel);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableBorderModel(value), prefix, version);
    }

    #endregion
}

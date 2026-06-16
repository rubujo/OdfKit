using System;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Accessors - FO, Draw & Position

    /// <summary>
    /// 取得具有 schema awareness 的 FO 文字轉換屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 FO 文字轉換；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFoTextTransform? GetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfFoTextTransform textTransform) ? textTransform : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 FO 文字轉換屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 FO 文字轉換。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfFoTextTransform value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF FO 文字轉換。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 FO 文字對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 FO 文字對齊；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFoTextAlign? GetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfFoTextAlign textAlign) ? textAlign : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 FO 文字對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 FO 文字對齊。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfFoTextAlign value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF FO 文字對齊。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式文字旋轉縮放屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式文字旋轉縮放；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleTextRotationScale? GetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleTextRotationScale scale) ? scale : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式文字旋轉縮放屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式文字旋轉縮放。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfStyleTextRotationScale value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文字旋轉縮放。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式文字組合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式文字組合；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleTextCombine? GetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleTextCombine textCombine) ? textCombine : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式文字組合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式文字組合。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfStyleTextCombine value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文字組合。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的繪圖填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖填滿；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawFill? GetDrawFillAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfDrawFill fill) ? fill : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖填滿。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawFillAttributeValue(string localName, string namespaceUri, OdfDrawFill value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖填滿。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 SMIL 動畫填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 SMIL 動畫填滿；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfSmilFill? GetSmilFillAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfSmilFill fill) ? fill : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 SMIL 動畫填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 SMIL 動畫填滿。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetSmilFillAttributeValue(string localName, string namespaceUri, OdfSmilFill value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF SMIL 動畫填滿。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的繪圖填滿圖片參照點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖填滿圖片參照點；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawFillImageRefPoint? GetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfDrawFillImageRefPoint refPoint) ? refPoint : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖填滿圖片參照點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖填滿圖片參照點。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfDrawFillImageRefPoint value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖填滿圖片參照點。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的繪圖色彩模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖色彩模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawColorMode? GetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfDrawColorMode colorMode) ? colorMode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖色彩模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖色彩模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfDrawColorMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖色彩模式。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式垂直對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式垂直對齊；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleVerticalAlign? GetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleVerticalAlign verticalAlign) ? verticalAlign : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式垂直對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式垂直對齊。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfStyleVerticalAlign value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式垂直對齊。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式垂直位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式垂直位置；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleVerticalPos? GetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleVerticalPos pos) ? pos : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式垂直位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式垂直位置。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfStyleVerticalPos value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式垂直位置。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式垂直相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式垂直相對基準；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleVerticalRel? GetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleVerticalRel rel) ? rel : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式垂直相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式垂直相對基準。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfStyleVerticalRel value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式垂直相對基準。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式水平位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式水平位置；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleHorizontalPos? GetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleHorizontalPos pos) ? pos : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式水平位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式水平位置。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalPos value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式水平位置。"), prefix, version);
    }

    #endregion
}

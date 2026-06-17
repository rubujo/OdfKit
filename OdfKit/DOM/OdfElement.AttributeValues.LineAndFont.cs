using System;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values - Line & Font

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
        return OdfElementEnumAttributeAccess.GetNullable<OdfLineMode>(value, OdfElementSchemaRegistry.TryParseLineMode);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatLineMode(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontStyle>(value, OdfElementSchemaRegistry.TryParseFontStyle);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontStyle(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontVariant>(value, OdfElementSchemaRegistry.TryParseFontVariant);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontVariant(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontWeight>(value, OdfElementSchemaRegistry.TryParseFontWeight);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontWeight(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontFamilyGeneric>(value, OdfElementSchemaRegistry.TryParseFontFamilyGeneric);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontFamilyGeneric(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontPitch>(value, OdfElementSchemaRegistry.TryParseFontPitch);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontPitch(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontRelief>(value, OdfElementSchemaRegistry.TryParseFontRelief);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontRelief(value), prefix, version);
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
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontStretch>(value, OdfElementSchemaRegistry.TryParseFontStretch);
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontStretch(value), prefix, version);
    }

    #endregion
}

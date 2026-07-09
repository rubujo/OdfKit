using System;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfElement API.
/// 提供 OdfElement API。
/// </summary>

public partial class OdfElement
{
    #region Attribute Values - Line & Font
    /// <summary>
    /// Short overload of GetLineWidthAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetLineWidthAttributeValue 多載。
    /// </summary>
    public OdfLineWidth? GetLineWidthAttributeValue(string localName, string namespaceUri) => GetLineWidthAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets line width attribute value.
    /// 取得具有 schema awareness 的線條寬度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的線條寬度；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfLineWidth? GetLineWidthAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfLineWidth>(value, OdfLineWidth.TryParse);
    }
    /// <summary>
    /// Short overload of SetLineWidthAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetLineWidthAttributeValue 多載。
    /// </summary>
    public void SetLineWidthAttributeValue(string localName, string namespaceUri, OdfLineWidth value) => SetLineWidthAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetLineWidthAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetLineWidthAttributeValue 多載。
    /// </summary>
    public void SetLineWidthAttributeValue(string localName, string namespaceUri, OdfLineWidth value, string? prefix) => SetLineWidthAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets line width attribute value.
    /// 設定具有 schema awareness 的線條寬度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的線條寬度</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLineWidthAttributeValue(string localName, string namespaceUri, OdfLineWidth value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetLineModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetLineModeAttributeValue 多載。
    /// </summary>
    public OdfLineMode? GetLineModeAttributeValue(string localName, string namespaceUri) => GetLineModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets line mode attribute value.
    /// 取得具有 schema awareness 的線條模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的線條模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfLineMode? GetLineModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfLineMode>(value, OdfElementSchemaRegistry.TryParseLineMode);
    }
    /// <summary>
    /// Short overload of SetLineModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetLineModeAttributeValue 多載。
    /// </summary>
    public void SetLineModeAttributeValue(string localName, string namespaceUri, OdfLineMode value) => SetLineModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetLineModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetLineModeAttributeValue 多載。
    /// </summary>
    public void SetLineModeAttributeValue(string localName, string namespaceUri, OdfLineMode value, string? prefix) => SetLineModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets line mode attribute value.
    /// 設定具有 schema awareness 的線條模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的線條模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLineModeAttributeValue(string localName, string namespaceUri, OdfLineMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatLineMode(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFontStyleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFontStyleAttributeValue 多載。
    /// </summary>
    public OdfFontStyle? GetFontStyleAttributeValue(string localName, string namespaceUri) => GetFontStyleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets font style attribute value.
    /// 取得具有 schema awareness 的字型樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的字型樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFontStyle? GetFontStyleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontStyle>(value, OdfElementSchemaRegistry.TryParseFontStyle);
    }
    /// <summary>
    /// Short overload of SetFontStyleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFontStyleAttributeValue 多載。
    /// </summary>
    public void SetFontStyleAttributeValue(string localName, string namespaceUri, OdfFontStyle value) => SetFontStyleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFontStyleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFontStyleAttributeValue 多載。
    /// </summary>
    public void SetFontStyleAttributeValue(string localName, string namespaceUri, OdfFontStyle value, string? prefix) => SetFontStyleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets font style attribute value.
    /// 設定具有 schema awareness 的字型樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的字型樣式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFontStyleAttributeValue(string localName, string namespaceUri, OdfFontStyle value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontStyle(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFontVariantAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFontVariantAttributeValue 多載。
    /// </summary>
    public OdfFontVariant? GetFontVariantAttributeValue(string localName, string namespaceUri) => GetFontVariantAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets font variant attribute value.
    /// 取得具有 schema awareness 的字型變體屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的字型變體；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFontVariant? GetFontVariantAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontVariant>(value, OdfElementSchemaRegistry.TryParseFontVariant);
    }
    /// <summary>
    /// Short overload of SetFontVariantAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFontVariantAttributeValue 多載。
    /// </summary>
    public void SetFontVariantAttributeValue(string localName, string namespaceUri, OdfFontVariant value) => SetFontVariantAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFontVariantAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFontVariantAttributeValue 多載。
    /// </summary>
    public void SetFontVariantAttributeValue(string localName, string namespaceUri, OdfFontVariant value, string? prefix) => SetFontVariantAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets font variant attribute value.
    /// 設定具有 schema awareness 的字型變體屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的字型變體</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFontVariantAttributeValue(string localName, string namespaceUri, OdfFontVariant value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontVariant(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFontWeightAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFontWeightAttributeValue 多載。
    /// </summary>
    public OdfFontWeight? GetFontWeightAttributeValue(string localName, string namespaceUri) => GetFontWeightAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets font weight attribute value.
    /// 取得具有 schema awareness 的字型粗細屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的字型粗細；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFontWeight? GetFontWeightAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontWeight>(value, OdfElementSchemaRegistry.TryParseFontWeight);
    }
    /// <summary>
    /// Short overload of SetFontWeightAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFontWeightAttributeValue 多載。
    /// </summary>
    public void SetFontWeightAttributeValue(string localName, string namespaceUri, OdfFontWeight value) => SetFontWeightAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFontWeightAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFontWeightAttributeValue 多載。
    /// </summary>
    public void SetFontWeightAttributeValue(string localName, string namespaceUri, OdfFontWeight value, string? prefix) => SetFontWeightAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets font weight attribute value.
    /// 設定具有 schema awareness 的字型粗細屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的字型粗細</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFontWeightAttributeValue(string localName, string namespaceUri, OdfFontWeight value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontWeight(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFontFamilyGenericAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFontFamilyGenericAttributeValue 多載。
    /// </summary>
    public OdfFontFamilyGeneric? GetFontFamilyGenericAttributeValue(string localName, string namespaceUri) => GetFontFamilyGenericAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets font family generic attribute value.
    /// 取得具有 schema awareness 的通用字型家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的通用字型家族；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFontFamilyGeneric? GetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontFamilyGeneric>(value, OdfElementSchemaRegistry.TryParseFontFamilyGeneric);
    }
    /// <summary>
    /// Short overload of SetFontFamilyGenericAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFontFamilyGenericAttributeValue 多載。
    /// </summary>
    public void SetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfFontFamilyGeneric value) => SetFontFamilyGenericAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFontFamilyGenericAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFontFamilyGenericAttributeValue 多載。
    /// </summary>
    public void SetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfFontFamilyGeneric value, string? prefix) => SetFontFamilyGenericAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets font family generic attribute value.
    /// 設定具有 schema awareness 的通用字型家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的通用字型家族</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfFontFamilyGeneric value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontFamilyGeneric(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFontPitchAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFontPitchAttributeValue 多載。
    /// </summary>
    public OdfFontPitch? GetFontPitchAttributeValue(string localName, string namespaceUri) => GetFontPitchAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets font pitch attribute value.
    /// 取得具有 schema awareness 的字型間距屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的字型間距；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFontPitch? GetFontPitchAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontPitch>(value, OdfElementSchemaRegistry.TryParseFontPitch);
    }
    /// <summary>
    /// Short overload of SetFontPitchAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFontPitchAttributeValue 多載。
    /// </summary>
    public void SetFontPitchAttributeValue(string localName, string namespaceUri, OdfFontPitch value) => SetFontPitchAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFontPitchAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFontPitchAttributeValue 多載。
    /// </summary>
    public void SetFontPitchAttributeValue(string localName, string namespaceUri, OdfFontPitch value, string? prefix) => SetFontPitchAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets font pitch attribute value.
    /// 設定具有 schema awareness 的字型間距屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的字型間距</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFontPitchAttributeValue(string localName, string namespaceUri, OdfFontPitch value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontPitch(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFontReliefAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFontReliefAttributeValue 多載。
    /// </summary>
    public OdfFontRelief? GetFontReliefAttributeValue(string localName, string namespaceUri) => GetFontReliefAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets font relief attribute value.
    /// 取得具有 schema awareness 的字型浮雕屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的字型浮雕；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFontRelief? GetFontReliefAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontRelief>(value, OdfElementSchemaRegistry.TryParseFontRelief);
    }
    /// <summary>
    /// Short overload of SetFontReliefAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFontReliefAttributeValue 多載。
    /// </summary>
    public void SetFontReliefAttributeValue(string localName, string namespaceUri, OdfFontRelief value) => SetFontReliefAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFontReliefAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFontReliefAttributeValue 多載。
    /// </summary>
    public void SetFontReliefAttributeValue(string localName, string namespaceUri, OdfFontRelief value, string? prefix) => SetFontReliefAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets font relief attribute value.
    /// 設定具有 schema awareness 的字型浮雕屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的字型浮雕</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFontReliefAttributeValue(string localName, string namespaceUri, OdfFontRelief value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontRelief(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFontStretchAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFontStretchAttributeValue 多載。
    /// </summary>
    public OdfFontStretch? GetFontStretchAttributeValue(string localName, string namespaceUri) => GetFontStretchAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets font stretch attribute value.
    /// 取得具有 schema awareness 的字型伸縮屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的字型伸縮；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFontStretch? GetFontStretchAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFontStretch>(value, OdfElementSchemaRegistry.TryParseFontStretch);
    }
    /// <summary>
    /// Short overload of SetFontStretchAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFontStretchAttributeValue 多載。
    /// </summary>
    public void SetFontStretchAttributeValue(string localName, string namespaceUri, OdfFontStretch value) => SetFontStretchAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFontStretchAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFontStretchAttributeValue 多載。
    /// </summary>
    public void SetFontStretchAttributeValue(string localName, string namespaceUri, OdfFontStretch value, string? prefix) => SetFontStretchAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets font stretch attribute value.
    /// 設定具有 schema awareness 的字型伸縮屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的字型伸縮</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFontStretchAttributeValue(string localName, string namespaceUri, OdfFontStretch value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFontStretch(value), prefix, version);
    }


    #endregion
}

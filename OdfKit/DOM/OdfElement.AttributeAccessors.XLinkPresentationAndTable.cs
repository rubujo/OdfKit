using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfElement API.
/// 提供 OdfElement API。
/// </summary>

public partial class OdfElement
{
    #region Attribute Accessors - XLink, Presentation & Table
    /// <summary>
    /// Short overload of GetIriReferenceAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetIriReferenceAttributeValue 多載。
    /// </summary>
    public OdfIriReference? GetIriReferenceAttributeValue(string localName, string namespaceUri) => GetIriReferenceAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets iri reference attribute value.
    /// 取得具有 schema awareness 的 IRI 參照屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 IRI 參照；若屬性不存在或包含控制字元則為 <see langword="null"/></returns>
    public OdfIriReference? GetIriReferenceAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfIriReference>(value, OdfIriReference.TryParse);
    }
    /// <summary>
    /// Short overload of SetIriReferenceAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetIriReferenceAttributeValue 多載。
    /// </summary>
    public void SetIriReferenceAttributeValue(string localName, string namespaceUri, OdfIriReference value) => SetIriReferenceAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetIriReferenceAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetIriReferenceAttributeValue 多載。
    /// </summary>
    public void SetIriReferenceAttributeValue(string localName, string namespaceUri, OdfIriReference value, string? prefix) => SetIriReferenceAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets iri reference attribute value.
    /// 設定具有 schema awareness 的 IRI 參照屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 IRI 參照</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetIriReferenceAttributeValue(string localName, string namespaceUri, OdfIriReference value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetXLinkTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetXLinkTypeAttributeValue 多載。
    /// </summary>
    public OdfXLinkType? GetXLinkTypeAttributeValue(string localName, string namespaceUri) => GetXLinkTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets X link type attribute value.
    /// 取得具有 schema awareness 的 XLink 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 XLink 類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfXLinkType? GetXLinkTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfXLinkType>(value, OdfElementSchemaRegistry.TryParseXLinkType);
    }
    /// <summary>
    /// Short overload of SetXLinkTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetXLinkTypeAttributeValue 多載。
    /// </summary>
    public void SetXLinkTypeAttributeValue(string localName, string namespaceUri, OdfXLinkType value) => SetXLinkTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetXLinkTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetXLinkTypeAttributeValue 多載。
    /// </summary>
    public void SetXLinkTypeAttributeValue(string localName, string namespaceUri, OdfXLinkType value, string? prefix) => SetXLinkTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets X link type attribute value.
    /// 設定具有 schema awareness 的 XLink 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 XLink 類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetXLinkTypeAttributeValue(string localName, string namespaceUri, OdfXLinkType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatXLinkType(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetXLinkShowAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetXLinkShowAttributeValue 多載。
    /// </summary>
    public OdfXLinkShow? GetXLinkShowAttributeValue(string localName, string namespaceUri) => GetXLinkShowAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets X link show attribute value.
    /// 取得具有 schema awareness 的 XLink 顯示行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 XLink 顯示行為；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfXLinkShow? GetXLinkShowAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfXLinkShow>(value, OdfElementSchemaRegistry.TryParseXLinkShow);
    }
    /// <summary>
    /// Short overload of SetXLinkShowAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetXLinkShowAttributeValue 多載。
    /// </summary>
    public void SetXLinkShowAttributeValue(string localName, string namespaceUri, OdfXLinkShow value) => SetXLinkShowAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetXLinkShowAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetXLinkShowAttributeValue 多載。
    /// </summary>
    public void SetXLinkShowAttributeValue(string localName, string namespaceUri, OdfXLinkShow value, string? prefix) => SetXLinkShowAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets X link show attribute value.
    /// 設定具有 schema awareness 的 XLink 顯示行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 XLink 顯示行為</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetXLinkShowAttributeValue(string localName, string namespaceUri, OdfXLinkShow value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatXLinkShow(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetXLinkActuateAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetXLinkActuateAttributeValue 多載。
    /// </summary>
    public OdfXLinkActuate? GetXLinkActuateAttributeValue(string localName, string namespaceUri) => GetXLinkActuateAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets X link actuate attribute value.
    /// 取得具有 schema awareness 的 XLink 觸發行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 XLink 觸發行為；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfXLinkActuate? GetXLinkActuateAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfXLinkActuate>(value, OdfElementSchemaRegistry.TryParseXLinkActuate);
    }
    /// <summary>
    /// Short overload of SetXLinkActuateAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetXLinkActuateAttributeValue 多載。
    /// </summary>
    public void SetXLinkActuateAttributeValue(string localName, string namespaceUri, OdfXLinkActuate value) => SetXLinkActuateAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetXLinkActuateAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetXLinkActuateAttributeValue 多載。
    /// </summary>
    public void SetXLinkActuateAttributeValue(string localName, string namespaceUri, OdfXLinkActuate value, string? prefix) => SetXLinkActuateAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets X link actuate attribute value.
    /// 設定具有 schema awareness 的 XLink 觸發行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 XLink 觸發行為</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetXLinkActuateAttributeValue(string localName, string namespaceUri, OdfXLinkActuate value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatXLinkActuate(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetNumberStyleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetNumberStyleAttributeValue 多載。
    /// </summary>
    public OdfNumberStyle? GetNumberStyleAttributeValue(string localName, string namespaceUri) => GetNumberStyleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets number style attribute value.
    /// 取得具有 schema awareness 的數字樣式長短屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的數字樣式長短；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfNumberStyle? GetNumberStyleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfNumberStyle>(value, OdfElementSchemaRegistry.TryParseNumberStyle);
    }
    /// <summary>
    /// Short overload of SetNumberStyleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetNumberStyleAttributeValue 多載。
    /// </summary>
    public void SetNumberStyleAttributeValue(string localName, string namespaceUri, OdfNumberStyle value) => SetNumberStyleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetNumberStyleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetNumberStyleAttributeValue 多載。
    /// </summary>
    public void SetNumberStyleAttributeValue(string localName, string namespaceUri, OdfNumberStyle value, string? prefix) => SetNumberStyleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets number style attribute value.
    /// 設定具有 schema awareness 的數字樣式長短屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的數字樣式長短</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetNumberStyleAttributeValue(string localName, string namespaceUri, OdfNumberStyle value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatNumberStyle(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableOrderAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableOrderAttributeValue 多載。
    /// </summary>
    public OdfTableOrder? GetTableOrderAttributeValue(string localName, string namespaceUri) => GetTableOrderAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table order attribute value.
    /// 取得具有 schema awareness 的表格排序方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格排序方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableOrder? GetTableOrderAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableOrder>(value, OdfElementSchemaRegistry.TryParseTableOrder);
    }
    /// <summary>
    /// Short overload of SetTableOrderAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableOrderAttributeValue 多載。
    /// </summary>
    public void SetTableOrderAttributeValue(string localName, string namespaceUri, OdfTableOrder value) => SetTableOrderAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableOrderAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableOrderAttributeValue 多載。
    /// </summary>
    public void SetTableOrderAttributeValue(string localName, string namespaceUri, OdfTableOrder value, string? prefix) => SetTableOrderAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table order attribute value.
    /// 設定具有 schema awareness 的表格排序方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格排序方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableOrderAttributeValue(string localName, string namespaceUri, OdfTableOrder value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableOrder(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableTypeAttributeValue 多載。
    /// </summary>
    public OdfTableType? GetTableTypeAttributeValue(string localName, string namespaceUri) => GetTableTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table type attribute value.
    /// 取得具有 schema awareness 的表格類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableType? GetTableTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableType>(value, OdfElementSchemaRegistry.TryParseTableType);
    }
    /// <summary>
    /// Short overload of SetTableTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableTypeAttributeValue 多載。
    /// </summary>
    public void SetTableTypeAttributeValue(string localName, string namespaceUri, OdfTableType value) => SetTableTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableTypeAttributeValue 多載。
    /// </summary>
    public void SetTableTypeAttributeValue(string localName, string namespaceUri, OdfTableType value, string? prefix) => SetTableTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table type attribute value.
    /// 設定具有 schema awareness 的表格類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableTypeAttributeValue(string localName, string namespaceUri, OdfTableType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableType(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetPresentationEffectAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPresentationEffectAttributeValue 多載。
    /// </summary>
    public OdfPresentationEffect? GetPresentationEffectAttributeValue(string localName, string namespaceUri) => GetPresentationEffectAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets presentation effect attribute value.
    /// 取得具有 schema awareness 的簡報效果屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的簡報效果；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfPresentationEffect? GetPresentationEffectAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationEffect>(value, OdfElementSchemaRegistry.TryParsePresentationEffect);
    }
    /// <summary>
    /// Short overload of SetPresentationEffectAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPresentationEffectAttributeValue 多載。
    /// </summary>
    public void SetPresentationEffectAttributeValue(string localName, string namespaceUri, OdfPresentationEffect value) => SetPresentationEffectAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPresentationEffectAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPresentationEffectAttributeValue 多載。
    /// </summary>
    public void SetPresentationEffectAttributeValue(string localName, string namespaceUri, OdfPresentationEffect value, string? prefix) => SetPresentationEffectAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets presentation effect attribute value.
    /// 設定具有 schema awareness 的簡報效果屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的簡報效果</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPresentationEffectAttributeValue(string localName, string namespaceUri, OdfPresentationEffect value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationEffect(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetPresentationSpeedAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPresentationSpeedAttributeValue 多載。
    /// </summary>
    public OdfPresentationSpeed? GetPresentationSpeedAttributeValue(string localName, string namespaceUri) => GetPresentationSpeedAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets presentation speed attribute value.
    /// 取得具有 schema awareness 的簡報速度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的簡報速度；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfPresentationSpeed? GetPresentationSpeedAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationSpeed>(value, OdfElementSchemaRegistry.TryParsePresentationSpeed);
    }
    /// <summary>
    /// Short overload of SetPresentationSpeedAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPresentationSpeedAttributeValue 多載。
    /// </summary>
    public void SetPresentationSpeedAttributeValue(string localName, string namespaceUri, OdfPresentationSpeed value) => SetPresentationSpeedAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPresentationSpeedAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPresentationSpeedAttributeValue 多載。
    /// </summary>
    public void SetPresentationSpeedAttributeValue(string localName, string namespaceUri, OdfPresentationSpeed value, string? prefix) => SetPresentationSpeedAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets presentation speed attribute value.
    /// 設定具有 schema awareness 的簡報速度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的簡報速度</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPresentationSpeedAttributeValue(string localName, string namespaceUri, OdfPresentationSpeed value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationSpeed(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetPresentationActionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPresentationActionAttributeValue 多載。
    /// </summary>
    public OdfPresentationAction? GetPresentationActionAttributeValue(string localName, string namespaceUri) => GetPresentationActionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets presentation action attribute value.
    /// 取得具有 schema awareness 的簡報動作屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的簡報動作；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfPresentationAction? GetPresentationActionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationAction>(value, OdfElementSchemaRegistry.TryParsePresentationAction);
    }
    /// <summary>
    /// Short overload of SetPresentationActionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPresentationActionAttributeValue 多載。
    /// </summary>
    public void SetPresentationActionAttributeValue(string localName, string namespaceUri, OdfPresentationAction value) => SetPresentationActionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPresentationActionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPresentationActionAttributeValue 多載。
    /// </summary>
    public void SetPresentationActionAttributeValue(string localName, string namespaceUri, OdfPresentationAction value, string? prefix) => SetPresentationActionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets presentation action attribute value.
    /// 設定具有 schema awareness 的簡報動作屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的簡報動作</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPresentationActionAttributeValue(string localName, string namespaceUri, OdfPresentationAction value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationAction(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetPresentationTransitionTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPresentationTransitionTypeAttributeValue 多載。
    /// </summary>
    public OdfPresentationTransitionType? GetPresentationTransitionTypeAttributeValue(string localName, string namespaceUri) => GetPresentationTransitionTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets presentation transition type attribute value.
    /// 取得具有 schema awareness 的簡報轉場類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的簡報轉場類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfPresentationTransitionType? GetPresentationTransitionTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationTransitionType>(value, OdfElementSchemaRegistry.TryParsePresentationTransitionType);
    }
    /// <summary>
    /// Short overload of SetPresentationTransitionTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPresentationTransitionTypeAttributeValue 多載。
    /// </summary>
    public void SetPresentationTransitionTypeAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionType value) => SetPresentationTransitionTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPresentationTransitionTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPresentationTransitionTypeAttributeValue 多載。
    /// </summary>
    public void SetPresentationTransitionTypeAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionType value, string? prefix) => SetPresentationTransitionTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets presentation transition type attribute value.
    /// 設定具有 schema awareness 的簡報轉場類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的簡報轉場類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPresentationTransitionTypeAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationTransitionType(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetPresentationTransitionStyleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPresentationTransitionStyleAttributeValue 多載。
    /// </summary>
    public OdfPresentationTransitionStyle? GetPresentationTransitionStyleAttributeValue(string localName, string namespaceUri) => GetPresentationTransitionStyleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets presentation transition style attribute value.
    /// 取得具有 schema awareness 的簡報轉場樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的簡報轉場樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfPresentationTransitionStyle? GetPresentationTransitionStyleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationTransitionStyle>(value, OdfElementSchemaRegistry.TryParsePresentationTransitionStyle);
    }
    /// <summary>
    /// Short overload of SetPresentationTransitionStyleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPresentationTransitionStyleAttributeValue 多載。
    /// </summary>
    public void SetPresentationTransitionStyleAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionStyle value) => SetPresentationTransitionStyleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPresentationTransitionStyleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPresentationTransitionStyleAttributeValue 多載。
    /// </summary>
    public void SetPresentationTransitionStyleAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionStyle value, string? prefix) => SetPresentationTransitionStyleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets presentation transition style attribute value.
    /// 設定具有 schema awareness 的簡報轉場樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的簡報轉場樣式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPresentationTransitionStyleAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionStyle value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationTransitionStyle(value), prefix, version);
    }


    #endregion
}

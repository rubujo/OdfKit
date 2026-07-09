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
    #region Attribute Values - Style, Form, Table & Media
    /// <summary>
    /// Short overload of GetStyleLineBreakAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleLineBreakAttributeValue 多載。
    /// </summary>
    public OdfStyleLineBreak? GetStyleLineBreakAttributeValue(string localName, string namespaceUri) => GetStyleLineBreakAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style line break attribute value.
    /// 取得具有 schema awareness 的斷行規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的斷行規則；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleLineBreak? GetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfStyleLineBreak>(value, OdfElementSchemaRegistry.TryParseStyleLineBreak);
    }
    /// <summary>
    /// Short overload of SetStyleLineBreakAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleLineBreakAttributeValue 多載。
    /// </summary>
    public void SetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfStyleLineBreak value) => SetStyleLineBreakAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleLineBreakAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleLineBreakAttributeValue 多載。
    /// </summary>
    public void SetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfStyleLineBreak value, string? prefix) => SetStyleLineBreakAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style line break attribute value.
    /// 設定具有 schema awareness 的斷行規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的斷行規則</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfStyleLineBreak value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatStyleLineBreak(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleRepeatAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleRepeatAttributeValue 多載。
    /// </summary>
    public OdfStyleRepeat? GetStyleRepeatAttributeValue(string localName, string namespaceUri) => GetStyleRepeatAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style repeat attribute value.
    /// 取得具有 schema awareness 的背景重複屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的背景重複；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleRepeat? GetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfStyleRepeat>(value, OdfElementSchemaRegistry.TryParseStyleRepeat);
    }
    /// <summary>
    /// Short overload of SetStyleRepeatAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleRepeatAttributeValue 多載。
    /// </summary>
    public void SetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfStyleRepeat value) => SetStyleRepeatAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleRepeatAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleRepeatAttributeValue 多載。
    /// </summary>
    public void SetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfStyleRepeat value, string? prefix) => SetStyleRepeatAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style repeat attribute value.
    /// 設定具有 schema awareness 的背景重複屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的背景重複</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfStyleRepeat value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatStyleRepeat(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleDirectionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleDirectionAttributeValue 多載。
    /// </summary>
    public OdfStyleDirection? GetStyleDirectionAttributeValue(string localName, string namespaceUri) => GetStyleDirectionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style direction attribute value.
    /// 取得具有 schema awareness 的樣式方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleDirection? GetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfStyleDirection>(value, OdfElementSchemaRegistry.TryParseStyleDirection);
    }
    /// <summary>
    /// Short overload of SetStyleDirectionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleDirectionAttributeValue 多載。
    /// </summary>
    public void SetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfStyleDirection value) => SetStyleDirectionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleDirectionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleDirectionAttributeValue 多載。
    /// </summary>
    public void SetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfStyleDirection value, string? prefix) => SetStyleDirectionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style direction attribute value.
    /// 設定具有 schema awareness 的樣式方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfStyleDirection value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatStyleDirection(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFormOrientationAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFormOrientationAttributeValue 多載。
    /// </summary>
    public OdfFormOrientation? GetFormOrientationAttributeValue(string localName, string namespaceUri) => GetFormOrientationAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets form orientation attribute value.
    /// 取得具有 schema awareness 的表單方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表單方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFormOrientation? GetFormOrientationAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFormOrientation>(value, OdfElementSchemaRegistry.TryParseFormOrientation);
    }
    /// <summary>
    /// Short overload of SetFormOrientationAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFormOrientationAttributeValue 多載。
    /// </summary>
    public void SetFormOrientationAttributeValue(string localName, string namespaceUri, OdfFormOrientation value) => SetFormOrientationAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFormOrientationAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFormOrientationAttributeValue 多載。
    /// </summary>
    public void SetFormOrientationAttributeValue(string localName, string namespaceUri, OdfFormOrientation value, string? prefix) => SetFormOrientationAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets form orientation attribute value.
    /// 設定具有 schema awareness 的表單方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表單方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFormOrientationAttributeValue(string localName, string namespaceUri, OdfFormOrientation value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFormOrientation(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableDirectionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableDirectionAttributeValue 多載。
    /// </summary>
    public OdfTableDirection? GetTableDirectionAttributeValue(string localName, string namespaceUri) => GetTableDirectionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table direction attribute value.
    /// 取得具有 schema awareness 的表格方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableDirection? GetTableDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableDirection>(value, OdfElementSchemaRegistry.TryParseTableDirection);
    }
    /// <summary>
    /// Short overload of SetTableDirectionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableDirectionAttributeValue 多載。
    /// </summary>
    public void SetTableDirectionAttributeValue(string localName, string namespaceUri, OdfTableDirection value) => SetTableDirectionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableDirectionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableDirectionAttributeValue 多載。
    /// </summary>
    public void SetTableDirectionAttributeValue(string localName, string namespaceUri, OdfTableDirection value, string? prefix) => SetTableDirectionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table direction attribute value.
    /// 設定具有 schema awareness 的表格方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableDirectionAttributeValue(string localName, string namespaceUri, OdfTableDirection value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableDirection(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableOrientationAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableOrientationAttributeValue 多載。
    /// </summary>
    public OdfTableOrientation? GetTableOrientationAttributeValue(string localName, string namespaceUri) => GetTableOrientationAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table orientation attribute value.
    /// 取得具有 schema awareness 的表格方位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格方位；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableOrientation? GetTableOrientationAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableOrientation>(value, OdfElementSchemaRegistry.TryParseTableOrientation);
    }
    /// <summary>
    /// Short overload of SetTableOrientationAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableOrientationAttributeValue 多載。
    /// </summary>
    public void SetTableOrientationAttributeValue(string localName, string namespaceUri, OdfTableOrientation value) => SetTableOrientationAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableOrientationAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableOrientationAttributeValue 多載。
    /// </summary>
    public void SetTableOrientationAttributeValue(string localName, string namespaceUri, OdfTableOrientation value, string? prefix) => SetTableOrientationAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table orientation attribute value.
    /// 設定具有 schema awareness 的表格方位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格方位</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableOrientationAttributeValue(string localName, string namespaceUri, OdfTableOrientation value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableOrientation(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleFamilyAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleFamilyAttributeValue 多載。
    /// </summary>
    public OdfStyleFamily? GetStyleFamilyAttributeValue(string localName, string namespaceUri) => GetStyleFamilyAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style family attribute value.
    /// 取得具有 schema awareness 的樣式家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式家族；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleFamily? GetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfStyleFamily>(value, OdfElementSchemaRegistry.TryParseStyleFamily);
    }
    /// <summary>
    /// Short overload of SetStyleFamilyAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleFamilyAttributeValue 多載。
    /// </summary>
    public void SetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfStyleFamily value) => SetStyleFamilyAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleFamilyAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleFamilyAttributeValue 多載。
    /// </summary>
    public void SetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfStyleFamily value, string? prefix) => SetStyleFamilyAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style family attribute value.
    /// 設定具有 schema awareness 的樣式家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式家族</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfStyleFamily value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatStyleFamily(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetOdfVersionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetOdfVersionAttributeValue 多載。
    /// </summary>
    public OdfVersion? GetOdfVersionAttributeValue(string localName, string namespaceUri) => GetOdfVersionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets odf version attribute value.
    /// 取得具有 schema awareness 的 ODF 版本屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 ODF 版本；若屬性不存在或不是已知版本則為 <see langword="null"/></returns>
    public OdfVersion? GetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetVersion(value);
    }
    /// <summary>
    /// Short overload of SetOdfVersionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetOdfVersionAttributeValue 多載。
    /// </summary>
    public void SetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion value) => SetOdfVersionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetOdfVersionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetOdfVersionAttributeValue 多載。
    /// </summary>
    public void SetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion value, string? prefix) => SetOdfVersionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets odf version attribute value.
    /// 設定具有 schema awareness 的 ODF 版本屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 ODF 版本</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementDomainAttributeAccess.FormatVersion(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetMediaTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetMediaTypeAttributeValue 多載。
    /// </summary>
    public OdfMediaType? GetMediaTypeAttributeValue(string localName, string namespaceUri) => GetMediaTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets media type attribute value.
    /// 取得具有 schema awareness 的 MIME 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 MIME 類型；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfMediaType? GetMediaTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfMediaType>(value, OdfMediaType.TryParse);
    }
    /// <summary>
    /// Short overload of SetMediaTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetMediaTypeAttributeValue 多載。
    /// </summary>
    public void SetMediaTypeAttributeValue(string localName, string namespaceUri, OdfMediaType value) => SetMediaTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetMediaTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetMediaTypeAttributeValue 多載。
    /// </summary>
    public void SetMediaTypeAttributeValue(string localName, string namespaceUri, OdfMediaType value, string? prefix) => SetMediaTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets media type attribute value.
    /// 設定具有 schema awareness 的 MIME 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 MIME 類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetMediaTypeAttributeValue(string localName, string namespaceUri, OdfMediaType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }




    #endregion
}

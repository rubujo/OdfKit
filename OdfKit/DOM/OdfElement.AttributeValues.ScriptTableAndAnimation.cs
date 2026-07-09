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
    #region Attribute Values - Script, Table & Animation
    /// <summary>
    /// Short overload of GetStyleScriptTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleScriptTypeAttributeValue 多載。
    /// </summary>
    public OdfStyleScriptType? GetStyleScriptTypeAttributeValue(string localName, string namespaceUri) => GetStyleScriptTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style script type attribute value.
    /// 取得具有 schema awareness 的樣式文字系統類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式文字系統類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleScriptType? GetStyleScriptTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleScriptType>(value);
    }
    /// <summary>
    /// Short overload of SetStyleScriptTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleScriptTypeAttributeValue 多載。
    /// </summary>
    public void SetStyleScriptTypeAttributeValue(string localName, string namespaceUri, OdfStyleScriptType value) => SetStyleScriptTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleScriptTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleScriptTypeAttributeValue 多載。
    /// </summary>
    public void SetStyleScriptTypeAttributeValue(string localName, string namespaceUri, OdfStyleScriptType value, string? prefix) => SetStyleScriptTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style script type attribute value.
    /// 設定具有 schema awareness 的樣式文字系統類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式文字系統類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleScriptTypeAttributeValue(string localName, string namespaceUri, OdfStyleScriptType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文字系統類型。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleTextEmphasizeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleTextEmphasizeAttributeValue 多載。
    /// </summary>
    public OdfStyleTextEmphasize? GetStyleTextEmphasizeAttributeValue(string localName, string namespaceUri) => GetStyleTextEmphasizeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style text emphasize attribute value.
    /// 取得具有 schema awareness 的樣式文字強調標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式文字強調標記；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleTextEmphasize? GetStyleTextEmphasizeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleTextEmphasize>(value);
    }
    /// <summary>
    /// Short overload of SetStyleTextEmphasizeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleTextEmphasizeAttributeValue 多載。
    /// </summary>
    public void SetStyleTextEmphasizeAttributeValue(string localName, string namespaceUri, OdfStyleTextEmphasize value) => SetStyleTextEmphasizeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleTextEmphasizeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleTextEmphasizeAttributeValue 多載。
    /// </summary>
    public void SetStyleTextEmphasizeAttributeValue(string localName, string namespaceUri, OdfStyleTextEmphasize value, string? prefix) => SetStyleTextEmphasizeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style text emphasize attribute value.
    /// 設定具有 schema awareness 的樣式文字強調標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式文字強調標記</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleTextEmphasizeAttributeValue(string localName, string namespaceUri, OdfStyleTextEmphasize value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文字強調標記。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetNumberCalendarAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetNumberCalendarAttributeValue 多載。
    /// </summary>
    public OdfNumberCalendar? GetNumberCalendarAttributeValue(string localName, string namespaceUri) => GetNumberCalendarAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets number calendar attribute value.
    /// 取得具有 schema awareness 的數字曆法屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的數字曆法；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfNumberCalendar? GetNumberCalendarAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfNumberCalendar>(value, OdfElementSchemaRegistry.TryParseNumberCalendar);
    }
    /// <summary>
    /// Short overload of SetNumberCalendarAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetNumberCalendarAttributeValue 多載。
    /// </summary>
    public void SetNumberCalendarAttributeValue(string localName, string namespaceUri, OdfNumberCalendar value) => SetNumberCalendarAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetNumberCalendarAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetNumberCalendarAttributeValue 多載。
    /// </summary>
    public void SetNumberCalendarAttributeValue(string localName, string namespaceUri, OdfNumberCalendar value, string? prefix) => SetNumberCalendarAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets number calendar attribute value.
    /// 設定具有 schema awareness 的數字曆法屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的數字曆法</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetNumberCalendarAttributeValue(string localName, string namespaceUri, OdfNumberCalendar value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatNumberCalendar(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableMemberTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableMemberTypeAttributeValue 多載。
    /// </summary>
    public OdfTableMemberType? GetTableMemberTypeAttributeValue(string localName, string namespaceUri) => GetTableMemberTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table member type attribute value.
    /// 取得具有 schema awareness 的表格成員類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格成員類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableMemberType? GetTableMemberTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableMemberType>(value);
    }
    /// <summary>
    /// Short overload of SetTableMemberTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableMemberTypeAttributeValue 多載。
    /// </summary>
    public void SetTableMemberTypeAttributeValue(string localName, string namespaceUri, OdfTableMemberType value) => SetTableMemberTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableMemberTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableMemberTypeAttributeValue 多載。
    /// </summary>
    public void SetTableMemberTypeAttributeValue(string localName, string namespaceUri, OdfTableMemberType value, string? prefix) => SetTableMemberTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table member type attribute value.
    /// 設定具有 schema awareness 的表格成員類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格成員類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableMemberTypeAttributeValue(string localName, string namespaceUri, OdfTableMemberType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格成員類型。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableGroupedByAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableGroupedByAttributeValue 多載。
    /// </summary>
    public OdfTableGroupedBy? GetTableGroupedByAttributeValue(string localName, string namespaceUri) => GetTableGroupedByAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table grouped by attribute value.
    /// 取得具有 schema awareness 的表格分組單位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格分組單位；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableGroupedBy? GetTableGroupedByAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableGroupedBy>(value);
    }
    /// <summary>
    /// Short overload of SetTableGroupedByAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableGroupedByAttributeValue 多載。
    /// </summary>
    public void SetTableGroupedByAttributeValue(string localName, string namespaceUri, OdfTableGroupedBy value) => SetTableGroupedByAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableGroupedByAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableGroupedByAttributeValue 多載。
    /// </summary>
    public void SetTableGroupedByAttributeValue(string localName, string namespaceUri, OdfTableGroupedBy value, string? prefix) => SetTableGroupedByAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table grouped by attribute value.
    /// 設定具有 schema awareness 的表格分組單位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格分組單位</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableGroupedByAttributeValue(string localName, string namespaceUri, OdfTableGroupedBy value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格分組單位。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableSortModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableSortModeAttributeValue 多載。
    /// </summary>
    public OdfTableSortMode? GetTableSortModeAttributeValue(string localName, string namespaceUri) => GetTableSortModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table sort mode attribute value.
    /// 取得具有 schema awareness 的表格排序模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格排序模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableSortMode? GetTableSortModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableSortMode>(value);
    }
    /// <summary>
    /// Short overload of SetTableSortModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableSortModeAttributeValue 多載。
    /// </summary>
    public void SetTableSortModeAttributeValue(string localName, string namespaceUri, OdfTableSortMode value) => SetTableSortModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableSortModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableSortModeAttributeValue 多載。
    /// </summary>
    public void SetTableSortModeAttributeValue(string localName, string namespaceUri, OdfTableSortMode value, string? prefix) => SetTableSortModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table sort mode attribute value.
    /// 設定具有 schema awareness 的表格排序模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格排序模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableSortModeAttributeValue(string localName, string namespaceUri, OdfTableSortMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格排序模式。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableConditionSourceAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableConditionSourceAttributeValue 多載。
    /// </summary>
    public OdfTableConditionSource? GetTableConditionSourceAttributeValue(string localName, string namespaceUri) => GetTableConditionSourceAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table condition source attribute value.
    /// 取得具有 schema awareness 的表格條件來源屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格條件來源；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableConditionSource? GetTableConditionSourceAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableConditionSource>(value);
    }
    /// <summary>
    /// Short overload of SetTableConditionSourceAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableConditionSourceAttributeValue 多載。
    /// </summary>
    public void SetTableConditionSourceAttributeValue(string localName, string namespaceUri, OdfTableConditionSource value) => SetTableConditionSourceAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableConditionSourceAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableConditionSourceAttributeValue 多載。
    /// </summary>
    public void SetTableConditionSourceAttributeValue(string localName, string namespaceUri, OdfTableConditionSource value, string? prefix) => SetTableConditionSourceAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table condition source attribute value.
    /// 設定具有 schema awareness 的表格條件來源屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格條件來源</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableConditionSourceAttributeValue(string localName, string namespaceUri, OdfTableConditionSource value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格條件來源。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetAnimationColorInterpolationAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetAnimationColorInterpolationAttributeValue 多載。
    /// </summary>
    public OdfAnimationColorInterpolation? GetAnimationColorInterpolationAttributeValue(string localName, string namespaceUri) => GetAnimationColorInterpolationAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets animation color interpolation attribute value.
    /// 取得具有 schema awareness 的動畫色彩插值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的動畫色彩插值；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfAnimationColorInterpolation? GetAnimationColorInterpolationAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfAnimationColorInterpolation>(value);
    }
    /// <summary>
    /// Short overload of SetAnimationColorInterpolationAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetAnimationColorInterpolationAttributeValue 多載。
    /// </summary>
    public void SetAnimationColorInterpolationAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolation value) => SetAnimationColorInterpolationAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetAnimationColorInterpolationAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetAnimationColorInterpolationAttributeValue 多載。
    /// </summary>
    public void SetAnimationColorInterpolationAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolation value, string? prefix) => SetAnimationColorInterpolationAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets animation color interpolation attribute value.
    /// 設定具有 schema awareness 的動畫色彩插值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的動畫色彩插值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetAnimationColorInterpolationAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolation value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 動畫色彩插值。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetAnimationColorInterpolationDirectionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetAnimationColorInterpolationDirectionAttributeValue 多載。
    /// </summary>
    public OdfAnimationColorInterpolationDirection? GetAnimationColorInterpolationDirectionAttributeValue(string localName, string namespaceUri) => GetAnimationColorInterpolationDirectionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets animation color interpolation direction attribute value.
    /// 取得具有 schema awareness 的動畫色彩插值方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的動畫色彩插值方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfAnimationColorInterpolationDirection? GetAnimationColorInterpolationDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfAnimationColorInterpolationDirection>(value);
    }
    /// <summary>
    /// Short overload of SetAnimationColorInterpolationDirectionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetAnimationColorInterpolationDirectionAttributeValue 多載。
    /// </summary>
    public void SetAnimationColorInterpolationDirectionAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolationDirection value) => SetAnimationColorInterpolationDirectionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetAnimationColorInterpolationDirectionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetAnimationColorInterpolationDirectionAttributeValue 多載。
    /// </summary>
    public void SetAnimationColorInterpolationDirectionAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolationDirection value, string? prefix) => SetAnimationColorInterpolationDirectionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets animation color interpolation direction attribute value.
    /// 設定具有 schema awareness 的動畫色彩插值方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的動畫色彩插值方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetAnimationColorInterpolationDirectionAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolationDirection value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 動畫色彩插值方向。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDatabaseIsNullableAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDatabaseIsNullableAttributeValue 多載。
    /// </summary>
    public OdfDatabaseIsNullable? GetDatabaseIsNullableAttributeValue(string localName, string namespaceUri) => GetDatabaseIsNullableAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets database is nullable attribute value.
    /// 取得具有 schema awareness 的資料庫可空性屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的資料庫可空性；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDatabaseIsNullable? GetDatabaseIsNullableAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDatabaseIsNullable>(value);
    }
    /// <summary>
    /// Short overload of SetDatabaseIsNullableAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDatabaseIsNullableAttributeValue 多載。
    /// </summary>
    public void SetDatabaseIsNullableAttributeValue(string localName, string namespaceUri, OdfDatabaseIsNullable value) => SetDatabaseIsNullableAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDatabaseIsNullableAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDatabaseIsNullableAttributeValue 多載。
    /// </summary>
    public void SetDatabaseIsNullableAttributeValue(string localName, string namespaceUri, OdfDatabaseIsNullable value, string? prefix) => SetDatabaseIsNullableAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets database is nullable attribute value.
    /// 設定具有 schema awareness 的資料庫可空性屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的資料庫可空性</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDatabaseIsNullableAttributeValue(string localName, string namespaceUri, OdfDatabaseIsNullable value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 資料庫可空性。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDatabaseDataSourceSettingTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDatabaseDataSourceSettingTypeAttributeValue 多載。
    /// </summary>
    public OdfDatabaseDataSourceSettingType? GetDatabaseDataSourceSettingTypeAttributeValue(string localName, string namespaceUri) => GetDatabaseDataSourceSettingTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets database data source setting type attribute value.
    /// 取得具有 schema awareness 的資料庫資料來源設定型別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的資料庫資料來源設定型別；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDatabaseDataSourceSettingType? GetDatabaseDataSourceSettingTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDatabaseDataSourceSettingType>(value);
    }
    /// <summary>
    /// Short overload of SetDatabaseDataSourceSettingTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDatabaseDataSourceSettingTypeAttributeValue 多載。
    /// </summary>
    public void SetDatabaseDataSourceSettingTypeAttributeValue(string localName, string namespaceUri, OdfDatabaseDataSourceSettingType value) => SetDatabaseDataSourceSettingTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDatabaseDataSourceSettingTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDatabaseDataSourceSettingTypeAttributeValue 多載。
    /// </summary>
    public void SetDatabaseDataSourceSettingTypeAttributeValue(string localName, string namespaceUri, OdfDatabaseDataSourceSettingType value, string? prefix) => SetDatabaseDataSourceSettingTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets database data source setting type attribute value.
    /// 設定具有 schema awareness 的資料庫資料來源設定型別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的資料庫資料來源設定型別</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDatabaseDataSourceSettingTypeAttributeValue(string localName, string namespaceUri, OdfDatabaseDataSourceSettingType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 資料庫資料來源設定型別。"), prefix, version);
    }



    #endregion
}

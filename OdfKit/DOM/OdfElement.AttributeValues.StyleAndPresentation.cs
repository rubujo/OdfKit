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
    #region Attribute Values - Style & Presentation
    /// <summary>
    /// Short overload of GetStyleHorizontalRelAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleHorizontalRelAttributeValue 多載。
    /// </summary>
    public OdfStyleHorizontalRel? GetStyleHorizontalRelAttributeValue(string localName, string namespaceUri) => GetStyleHorizontalRelAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style horizontal rel attribute value.
    /// 取得具有 schema awareness 的樣式水平相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式水平相對基準；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleHorizontalRel? GetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleHorizontalRel>(value);
    }
    /// <summary>
    /// Short overload of SetStyleHorizontalRelAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleHorizontalRelAttributeValue 多載。
    /// </summary>
    public void SetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalRel value) => SetStyleHorizontalRelAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleHorizontalRelAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleHorizontalRelAttributeValue 多載。
    /// </summary>
    public void SetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalRel value, string? prefix) => SetStyleHorizontalRelAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style horizontal rel attribute value.
    /// 設定具有 schema awareness 的樣式水平相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式水平相對基準</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalRel value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式水平相對基準。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleWritingModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleWritingModeAttributeValue 多載。
    /// </summary>
    public OdfStyleWritingMode? GetStyleWritingModeAttributeValue(string localName, string namespaceUri) => GetStyleWritingModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style writing mode attribute value.
    /// 取得具有 schema awareness 的樣式書寫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式書寫方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleWritingMode? GetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleWritingMode>(value);
    }
    /// <summary>
    /// Short overload of SetStyleWritingModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleWritingModeAttributeValue 多載。
    /// </summary>
    public void SetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfStyleWritingMode value) => SetStyleWritingModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleWritingModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleWritingModeAttributeValue 多載。
    /// </summary>
    public void SetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfStyleWritingMode value, string? prefix) => SetStyleWritingModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style writing mode attribute value.
    /// 設定具有 schema awareness 的樣式書寫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式書寫方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfStyleWritingMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式書寫方向。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleWrapAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleWrapAttributeValue 多載。
    /// </summary>
    public OdfStyleWrap? GetStyleWrapAttributeValue(string localName, string namespaceUri) => GetStyleWrapAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style wrap attribute value.
    /// 取得具有 schema awareness 的樣式文繞圖屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式文繞圖；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleWrap? GetStyleWrapAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleWrap>(value);
    }
    /// <summary>
    /// Short overload of SetStyleWrapAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleWrapAttributeValue 多載。
    /// </summary>
    public void SetStyleWrapAttributeValue(string localName, string namespaceUri, OdfStyleWrap value) => SetStyleWrapAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleWrapAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleWrapAttributeValue 多載。
    /// </summary>
    public void SetStyleWrapAttributeValue(string localName, string namespaceUri, OdfStyleWrap value, string? prefix) => SetStyleWrapAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style wrap attribute value.
    /// 設定具有 schema awareness 的樣式文繞圖屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式文繞圖</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleWrapAttributeValue(string localName, string namespaceUri, OdfStyleWrap value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文繞圖。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleRunThroughAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleRunThroughAttributeValue 多載。
    /// </summary>
    public OdfStyleRunThrough? GetStyleRunThroughAttributeValue(string localName, string namespaceUri) => GetStyleRunThroughAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style run through attribute value.
    /// 取得具有 schema awareness 的樣式穿越排列屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式穿越排列；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleRunThrough? GetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleRunThrough>(value);
    }
    /// <summary>
    /// Short overload of SetStyleRunThroughAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleRunThroughAttributeValue 多載。
    /// </summary>
    public void SetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfStyleRunThrough value) => SetStyleRunThroughAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleRunThroughAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleRunThroughAttributeValue 多載。
    /// </summary>
    public void SetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfStyleRunThrough value, string? prefix) => SetStyleRunThroughAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style run through attribute value.
    /// 設定具有 schema awareness 的樣式穿越排列屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式穿越排列</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfStyleRunThrough value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式穿越排列。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleWrapContourModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleWrapContourModeAttributeValue 多載。
    /// </summary>
    public OdfStyleWrapContourMode? GetStyleWrapContourModeAttributeValue(string localName, string namespaceUri) => GetStyleWrapContourModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style wrap contour mode attribute value.
    /// 取得具有 schema awareness 的樣式輪廓繞排模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式輪廓繞排模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleWrapContourMode? GetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleWrapContourMode>(value);
    }
    /// <summary>
    /// Short overload of SetStyleWrapContourModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleWrapContourModeAttributeValue 多載。
    /// </summary>
    public void SetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfStyleWrapContourMode value) => SetStyleWrapContourModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleWrapContourModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleWrapContourModeAttributeValue 多載。
    /// </summary>
    public void SetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfStyleWrapContourMode value, string? prefix) => SetStyleWrapContourModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style wrap contour mode attribute value.
    /// 設定具有 schema awareness 的樣式輪廓繞排模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式輪廓繞排模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfStyleWrapContourMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式輪廓繞排模式。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableDisplayMemberModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableDisplayMemberModeAttributeValue 多載。
    /// </summary>
    public OdfTableDisplayMemberMode? GetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri) => GetTableDisplayMemberModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table display member mode attribute value.
    /// 取得具有 schema awareness 的表格成員顯示方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格成員顯示方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableDisplayMemberMode? GetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableDisplayMemberMode>(value);
    }
    /// <summary>
    /// Short overload of SetTableDisplayMemberModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableDisplayMemberModeAttributeValue 多載。
    /// </summary>
    public void SetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfTableDisplayMemberMode value) => SetTableDisplayMemberModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableDisplayMemberModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableDisplayMemberModeAttributeValue 多載。
    /// </summary>
    public void SetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfTableDisplayMemberMode value, string? prefix) => SetTableDisplayMemberModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table display member mode attribute value.
    /// 設定具有 schema awareness 的表格成員顯示方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格成員顯示方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfTableDisplayMemberMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格成員顯示方向。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableLayoutModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableLayoutModeAttributeValue 多載。
    /// </summary>
    public OdfTableLayoutMode? GetTableLayoutModeAttributeValue(string localName, string namespaceUri) => GetTableLayoutModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table layout mode attribute value.
    /// 取得具有 schema awareness 的表格版面配置模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格版面配置模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableLayoutMode? GetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableLayoutMode>(value);
    }
    /// <summary>
    /// Short overload of SetTableLayoutModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableLayoutModeAttributeValue 多載。
    /// </summary>
    public void SetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfTableLayoutMode value) => SetTableLayoutModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableLayoutModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableLayoutModeAttributeValue 多載。
    /// </summary>
    public void SetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfTableLayoutMode value, string? prefix) => SetTableLayoutModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table layout mode attribute value.
    /// 設定具有 schema awareness 的表格版面配置模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格版面配置模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfTableLayoutMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格版面配置模式。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDatabaseRuleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDatabaseRuleAttributeValue 多載。
    /// </summary>
    public OdfDatabaseRule? GetDatabaseRuleAttributeValue(string localName, string namespaceUri) => GetDatabaseRuleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets database rule attribute value.
    /// 取得具有 schema awareness 的資料庫參照動作規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的資料庫參照動作規則；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDatabaseRule? GetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDatabaseRule>(value);
    }
    /// <summary>
    /// Short overload of SetDatabaseRuleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDatabaseRuleAttributeValue 多載。
    /// </summary>
    public void SetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfDatabaseRule value) => SetDatabaseRuleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDatabaseRuleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDatabaseRuleAttributeValue 多載。
    /// </summary>
    public void SetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfDatabaseRule value, string? prefix) => SetDatabaseRuleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets database rule attribute value.
    /// 設定具有 schema awareness 的資料庫參照動作規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的資料庫參照動作規則</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfDatabaseRule value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 資料庫參照動作規則。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetPresentationPresetClassAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPresentationPresetClassAttributeValue 多載。
    /// </summary>
    public OdfPresentationPresetClass? GetPresentationPresetClassAttributeValue(string localName, string namespaceUri) => GetPresentationPresetClassAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets presentation preset class attribute value.
    /// 取得具有 schema awareness 的簡報預設動畫類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的簡報預設動畫類別；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfPresentationPresetClass? GetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfPresentationPresetClass>(value);
    }
    /// <summary>
    /// Short overload of SetPresentationPresetClassAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPresentationPresetClassAttributeValue 多載。
    /// </summary>
    public void SetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfPresentationPresetClass value) => SetPresentationPresetClassAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPresentationPresetClassAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPresentationPresetClassAttributeValue 多載。
    /// </summary>
    public void SetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfPresentationPresetClass value, string? prefix) => SetPresentationPresetClassAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets presentation preset class attribute value.
    /// 設定具有 schema awareness 的簡報預設動畫類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的簡報預設動畫類別</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfPresentationPresetClass value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 簡報預設動畫類別。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetNumberTransliterationStyleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetNumberTransliterationStyleAttributeValue 多載。
    /// </summary>
    public OdfNumberTransliterationStyle? GetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri) => GetNumberTransliterationStyleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets number transliteration style attribute value.
    /// 取得具有 schema awareness 的數字音譯樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的數字音譯樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfNumberTransliterationStyle? GetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfNumberTransliterationStyle>(value);
    }
    /// <summary>
    /// Short overload of SetNumberTransliterationStyleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetNumberTransliterationStyleAttributeValue 多載。
    /// </summary>
    public void SetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfNumberTransliterationStyle value) => SetNumberTransliterationStyleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetNumberTransliterationStyleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetNumberTransliterationStyleAttributeValue 多載。
    /// </summary>
    public void SetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfNumberTransliterationStyle value, string? prefix) => SetNumberTransliterationStyleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets number transliteration style attribute value.
    /// 設定具有 schema awareness 的數字音譯樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的數字音譯樣式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfNumberTransliterationStyle value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 數字音譯樣式。"), prefix, version);
    }


    #endregion
}

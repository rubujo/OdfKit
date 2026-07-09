using System;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfElement API.
/// 提供 OdfElement API。
/// </summary>

public partial class OdfElement
{
    #region Attribute Values - Draw, FO & Stroke
    /// <summary>
    /// Short overload of GetDrawNoHrefAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDrawNoHrefAttributeValue 多載。
    /// </summary>
    public OdfDrawNoHref? GetDrawNoHrefAttributeValue(string localName, string namespaceUri) => GetDrawNoHrefAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets draw no href attribute value.
    /// 取得具有 schema awareness 的繪圖無連結屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的繪圖無連結；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDrawNoHref? GetDrawNoHrefAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDrawNoHref>(value);
    }
    /// <summary>
    /// Short overload of SetDrawNoHrefAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDrawNoHrefAttributeValue 多載。
    /// </summary>
    public void SetDrawNoHrefAttributeValue(string localName, string namespaceUri, OdfDrawNoHref value) => SetDrawNoHrefAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDrawNoHrefAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDrawNoHrefAttributeValue 多載。
    /// </summary>
    public void SetDrawNoHrefAttributeValue(string localName, string namespaceUri, OdfDrawNoHref value, string? prefix) => SetDrawNoHrefAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets draw no href attribute value.
    /// 設定具有 schema awareness 的繪圖無連結屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的繪圖無連結</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDrawNoHrefAttributeValue(string localName, string namespaceUri, OdfDrawNoHref value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖無連結。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableFunctionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableFunctionAttributeValue 多載。
    /// </summary>
    public OdfTableFunction? GetTableFunctionAttributeValue(string localName, string namespaceUri) => GetTableFunctionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table function attribute value.
    /// 取得具有 schema awareness 的表格彙總函式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格彙總函式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableFunction? GetTableFunctionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableFunction>(value);
    }
    /// <summary>
    /// Short overload of SetTableFunctionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableFunctionAttributeValue 多載。
    /// </summary>
    public void SetTableFunctionAttributeValue(string localName, string namespaceUri, OdfTableFunction value) => SetTableFunctionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableFunctionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableFunctionAttributeValue 多載。
    /// </summary>
    public void SetTableFunctionAttributeValue(string localName, string namespaceUri, OdfTableFunction value, string? prefix) => SetTableFunctionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table function attribute value.
    /// 設定具有 schema awareness 的表格彙總函式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格彙總函式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableFunctionAttributeValue(string localName, string namespaceUri, OdfTableFunction value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格彙總函式。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDrawStrokeLineJoinAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDrawStrokeLineJoinAttributeValue 多載。
    /// </summary>
    public OdfDrawStrokeLineJoin? GetDrawStrokeLineJoinAttributeValue(string localName, string namespaceUri) => GetDrawStrokeLineJoinAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets draw stroke line join attribute value.
    /// 取得具有 schema awareness 的繪圖線條接合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的繪圖線條接合；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDrawStrokeLineJoin? GetDrawStrokeLineJoinAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDrawStrokeLineJoin>(value);
    }
    /// <summary>
    /// Short overload of SetDrawStrokeLineJoinAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDrawStrokeLineJoinAttributeValue 多載。
    /// </summary>
    public void SetDrawStrokeLineJoinAttributeValue(string localName, string namespaceUri, OdfDrawStrokeLineJoin value) => SetDrawStrokeLineJoinAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDrawStrokeLineJoinAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDrawStrokeLineJoinAttributeValue 多載。
    /// </summary>
    public void SetDrawStrokeLineJoinAttributeValue(string localName, string namespaceUri, OdfDrawStrokeLineJoin value, string? prefix) => SetDrawStrokeLineJoinAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets draw stroke line join attribute value.
    /// 設定具有 schema awareness 的繪圖線條接合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的繪圖線條接合</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDrawStrokeLineJoinAttributeValue(string localName, string namespaceUri, OdfDrawStrokeLineJoin value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖線條接合。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetSvgStrokeLineCapAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetSvgStrokeLineCapAttributeValue 多載。
    /// </summary>
    public OdfSvgStrokeLineCap? GetSvgStrokeLineCapAttributeValue(string localName, string namespaceUri) => GetSvgStrokeLineCapAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets svg stroke line cap attribute value.
    /// 取得具有 schema awareness 的 SVG 線端樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 SVG 線端樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfSvgStrokeLineCap? GetSvgStrokeLineCapAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfSvgStrokeLineCap>(value);
    }
    /// <summary>
    /// Short overload of SetSvgStrokeLineCapAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetSvgStrokeLineCapAttributeValue 多載。
    /// </summary>
    public void SetSvgStrokeLineCapAttributeValue(string localName, string namespaceUri, OdfSvgStrokeLineCap value) => SetSvgStrokeLineCapAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetSvgStrokeLineCapAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetSvgStrokeLineCapAttributeValue 多載。
    /// </summary>
    public void SetSvgStrokeLineCapAttributeValue(string localName, string namespaceUri, OdfSvgStrokeLineCap value, string? prefix) => SetSvgStrokeLineCapAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets svg stroke line cap attribute value.
    /// 設定具有 schema awareness 的 SVG 線端樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 SVG 線端樣式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetSvgStrokeLineCapAttributeValue(string localName, string namespaceUri, OdfSvgStrokeLineCap value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF SVG 線端樣式。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFoKeepTogetherAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFoKeepTogetherAttributeValue 多載。
    /// </summary>
    public OdfFoKeepTogether? GetFoKeepTogetherAttributeValue(string localName, string namespaceUri) => GetFoKeepTogetherAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets fo keep together attribute value.
    /// 取得具有 schema awareness 的 FO 分頁保持屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 FO 分頁保持設定；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFoKeepTogether? GetFoKeepTogetherAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFoKeepTogether>(value, OdfElementSchemaRegistry.TryParseFoKeepTogether);
    }
    /// <summary>
    /// Short overload of SetFoKeepTogetherAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFoKeepTogetherAttributeValue 多載。
    /// </summary>
    public void SetFoKeepTogetherAttributeValue(string localName, string namespaceUri, OdfFoKeepTogether value) => SetFoKeepTogetherAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFoKeepTogetherAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFoKeepTogetherAttributeValue 多載。
    /// </summary>
    public void SetFoKeepTogetherAttributeValue(string localName, string namespaceUri, OdfFoKeepTogether value, string? prefix) => SetFoKeepTogetherAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets fo keep together attribute value.
    /// 設定具有 schema awareness 的 FO 分頁保持屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 FO 分頁保持設定</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFoKeepTogetherAttributeValue(string localName, string namespaceUri, OdfFoKeepTogether value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFoKeepTogether(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFoWrapOptionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFoWrapOptionAttributeValue 多載。
    /// </summary>
    public OdfFoWrapOption? GetFoWrapOptionAttributeValue(string localName, string namespaceUri) => GetFoWrapOptionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets fo wrap option attribute value.
    /// 取得具有 schema awareness 的 FO 換行選項屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 FO 換行選項；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFoWrapOption? GetFoWrapOptionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfFoWrapOption>(value, OdfElementSchemaRegistry.TryParseFoWrapOption);
    }
    /// <summary>
    /// Short overload of SetFoWrapOptionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFoWrapOptionAttributeValue 多載。
    /// </summary>
    public void SetFoWrapOptionAttributeValue(string localName, string namespaceUri, OdfFoWrapOption value) => SetFoWrapOptionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFoWrapOptionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFoWrapOptionAttributeValue 多載。
    /// </summary>
    public void SetFoWrapOptionAttributeValue(string localName, string namespaceUri, OdfFoWrapOption value, string? prefix) => SetFoWrapOptionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets fo wrap option attribute value.
    /// 設定具有 schema awareness 的 FO 換行選項屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 FO 換行選項</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFoWrapOptionAttributeValue(string localName, string namespaceUri, OdfFoWrapOption value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatFoWrapOption(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDr3dProjectionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDr3dProjectionAttributeValue 多載。
    /// </summary>
    public OdfDr3dProjection? GetDr3dProjectionAttributeValue(string localName, string namespaceUri) => GetDr3dProjectionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets dr 3 d projection attribute value.
    /// 取得具有 schema awareness 的 3D 投影屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 3D 投影；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDr3dProjection? GetDr3dProjectionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfDr3dProjection>(value, OdfElementSchemaRegistry.TryParseDr3dProjection);
    }
    /// <summary>
    /// Short overload of SetDr3dProjectionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDr3dProjectionAttributeValue 多載。
    /// </summary>
    public void SetDr3dProjectionAttributeValue(string localName, string namespaceUri, OdfDr3dProjection value) => SetDr3dProjectionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDr3dProjectionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDr3dProjectionAttributeValue 多載。
    /// </summary>
    public void SetDr3dProjectionAttributeValue(string localName, string namespaceUri, OdfDr3dProjection value, string? prefix) => SetDr3dProjectionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets dr 3 d projection attribute value.
    /// 設定具有 schema awareness 的 3D 投影屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 3D 投影</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDr3dProjectionAttributeValue(string localName, string namespaceUri, OdfDr3dProjection value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatDr3dProjection(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDr3dShadeModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDr3dShadeModeAttributeValue 多載。
    /// </summary>
    public OdfDr3dShadeMode? GetDr3dShadeModeAttributeValue(string localName, string namespaceUri) => GetDr3dShadeModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets dr 3 d shade mode attribute value.
    /// 取得具有 schema awareness 的 3D 著色模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 3D 著色模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDr3dShadeMode? GetDr3dShadeModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfDr3dShadeMode>(value, OdfElementSchemaRegistry.TryParseDr3dShadeMode);
    }
    /// <summary>
    /// Short overload of SetDr3dShadeModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDr3dShadeModeAttributeValue 多載。
    /// </summary>
    public void SetDr3dShadeModeAttributeValue(string localName, string namespaceUri, OdfDr3dShadeMode value) => SetDr3dShadeModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDr3dShadeModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDr3dShadeModeAttributeValue 多載。
    /// </summary>
    public void SetDr3dShadeModeAttributeValue(string localName, string namespaceUri, OdfDr3dShadeMode value, string? prefix) => SetDr3dShadeModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets dr 3 d shade mode attribute value.
    /// 設定具有 schema awareness 的 3D 著色模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 3D 著色模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDr3dShadeModeAttributeValue(string localName, string namespaceUri, OdfDr3dShadeMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatDr3dShadeMode(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetSvgFillRuleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetSvgFillRuleAttributeValue 多載。
    /// </summary>
    public OdfSvgFillRule? GetSvgFillRuleAttributeValue(string localName, string namespaceUri) => GetSvgFillRuleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets svg fill rule attribute value.
    /// 取得具有 schema awareness 的 SVG 填滿規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 SVG 填滿規則；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfSvgFillRule? GetSvgFillRuleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfSvgFillRule>(value, OdfElementSchemaRegistry.TryParseSvgFillRule);
    }
    /// <summary>
    /// Short overload of SetSvgFillRuleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetSvgFillRuleAttributeValue 多載。
    /// </summary>
    public void SetSvgFillRuleAttributeValue(string localName, string namespaceUri, OdfSvgFillRule value) => SetSvgFillRuleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetSvgFillRuleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetSvgFillRuleAttributeValue 多載。
    /// </summary>
    public void SetSvgFillRuleAttributeValue(string localName, string namespaceUri, OdfSvgFillRule value, string? prefix) => SetSvgFillRuleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets svg fill rule attribute value.
    /// 設定具有 schema awareness 的 SVG 填滿規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 SVG 填滿規則</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetSvgFillRuleAttributeValue(string localName, string namespaceUri, OdfSvgFillRule value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatSvgFillRule(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTableBorderModelAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTableBorderModelAttributeValue 多載。
    /// </summary>
    public OdfTableBorderModel? GetTableBorderModelAttributeValue(string localName, string namespaceUri) => GetTableBorderModelAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets table border model attribute value.
    /// 取得具有 schema awareness 的表格邊框模型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格邊框模型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableBorderModel? GetTableBorderModelAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableBorderModel>(value, OdfElementSchemaRegistry.TryParseTableBorderModel);
    }
    /// <summary>
    /// Short overload of SetTableBorderModelAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTableBorderModelAttributeValue 多載。
    /// </summary>
    public void SetTableBorderModelAttributeValue(string localName, string namespaceUri, OdfTableBorderModel value) => SetTableBorderModelAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTableBorderModelAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTableBorderModelAttributeValue 多載。
    /// </summary>
    public void SetTableBorderModelAttributeValue(string localName, string namespaceUri, OdfTableBorderModel value, string? prefix) => SetTableBorderModelAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets table border model attribute value.
    /// 設定具有 schema awareness 的表格邊框模型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格邊框模型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableBorderModelAttributeValue(string localName, string namespaceUri, OdfTableBorderModel value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableBorderModel(value), prefix, version);
    }


    #endregion
}

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
    #region Attribute Accessors - FO, Draw & Position
    /// <summary>
    /// Short overload of GetFoTextTransformAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFoTextTransformAttributeValue 多載。
    /// </summary>
    public OdfFoTextTransform? GetFoTextTransformAttributeValue(string localName, string namespaceUri) => GetFoTextTransformAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets fo text transform attribute value.
    /// 取得具有 schema awareness 的 FO 文字轉換屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 FO 文字轉換；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFoTextTransform? GetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfFoTextTransform>(value);
    }
    /// <summary>
    /// Short overload of SetFoTextTransformAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFoTextTransformAttributeValue 多載。
    /// </summary>
    public void SetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfFoTextTransform value) => SetFoTextTransformAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFoTextTransformAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFoTextTransformAttributeValue 多載。
    /// </summary>
    public void SetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfFoTextTransform value, string? prefix) => SetFoTextTransformAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets fo text transform attribute value.
    /// 設定具有 schema awareness 的 FO 文字轉換屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 FO 文字轉換</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfFoTextTransform value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF FO 文字轉換。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetFoTextAlignAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetFoTextAlignAttributeValue 多載。
    /// </summary>
    public OdfFoTextAlign? GetFoTextAlignAttributeValue(string localName, string namespaceUri) => GetFoTextAlignAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets fo text align attribute value.
    /// 取得具有 schema awareness 的 FO 文字對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 FO 文字對齊；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfFoTextAlign? GetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfFoTextAlign>(value);
    }
    /// <summary>
    /// Short overload of SetFoTextAlignAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetFoTextAlignAttributeValue 多載。
    /// </summary>
    public void SetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfFoTextAlign value) => SetFoTextAlignAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetFoTextAlignAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetFoTextAlignAttributeValue 多載。
    /// </summary>
    public void SetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfFoTextAlign value, string? prefix) => SetFoTextAlignAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets fo text align attribute value.
    /// 設定具有 schema awareness 的 FO 文字對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 FO 文字對齊</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfFoTextAlign value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF FO 文字對齊。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleTextRotationScaleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleTextRotationScaleAttributeValue 多載。
    /// </summary>
    public OdfStyleTextRotationScale? GetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri) => GetStyleTextRotationScaleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style text rotation scale attribute value.
    /// 取得具有 schema awareness 的樣式文字旋轉縮放屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式文字旋轉縮放；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleTextRotationScale? GetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleTextRotationScale>(value);
    }
    /// <summary>
    /// Short overload of SetStyleTextRotationScaleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleTextRotationScaleAttributeValue 多載。
    /// </summary>
    public void SetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfStyleTextRotationScale value) => SetStyleTextRotationScaleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleTextRotationScaleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleTextRotationScaleAttributeValue 多載。
    /// </summary>
    public void SetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfStyleTextRotationScale value, string? prefix) => SetStyleTextRotationScaleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style text rotation scale attribute value.
    /// 設定具有 schema awareness 的樣式文字旋轉縮放屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式文字旋轉縮放</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfStyleTextRotationScale value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文字旋轉縮放。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleTextCombineAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleTextCombineAttributeValue 多載。
    /// </summary>
    public OdfStyleTextCombine? GetStyleTextCombineAttributeValue(string localName, string namespaceUri) => GetStyleTextCombineAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style text combine attribute value.
    /// 取得具有 schema awareness 的樣式文字組合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式文字組合；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleTextCombine? GetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleTextCombine>(value);
    }
    /// <summary>
    /// Short overload of SetStyleTextCombineAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleTextCombineAttributeValue 多載。
    /// </summary>
    public void SetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfStyleTextCombine value) => SetStyleTextCombineAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleTextCombineAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleTextCombineAttributeValue 多載。
    /// </summary>
    public void SetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfStyleTextCombine value, string? prefix) => SetStyleTextCombineAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style text combine attribute value.
    /// 設定具有 schema awareness 的樣式文字組合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式文字組合</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfStyleTextCombine value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文字組合。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDrawFillAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDrawFillAttributeValue 多載。
    /// </summary>
    public OdfDrawFill? GetDrawFillAttributeValue(string localName, string namespaceUri) => GetDrawFillAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets draw fill attribute value.
    /// 取得具有 schema awareness 的繪圖填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的繪圖填滿；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDrawFill? GetDrawFillAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDrawFill>(value);
    }
    /// <summary>
    /// Short overload of SetDrawFillAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDrawFillAttributeValue 多載。
    /// </summary>
    public void SetDrawFillAttributeValue(string localName, string namespaceUri, OdfDrawFill value) => SetDrawFillAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDrawFillAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDrawFillAttributeValue 多載。
    /// </summary>
    public void SetDrawFillAttributeValue(string localName, string namespaceUri, OdfDrawFill value, string? prefix) => SetDrawFillAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets draw fill attribute value.
    /// 設定具有 schema awareness 的繪圖填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的繪圖填滿</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDrawFillAttributeValue(string localName, string namespaceUri, OdfDrawFill value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖填滿。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetSmilFillAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetSmilFillAttributeValue 多載。
    /// </summary>
    public OdfSmilFill? GetSmilFillAttributeValue(string localName, string namespaceUri) => GetSmilFillAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets smil fill attribute value.
    /// 取得具有 schema awareness 的 SMIL 動畫填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 SMIL 動畫填滿；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfSmilFill? GetSmilFillAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfSmilFill>(value);
    }
    /// <summary>
    /// Short overload of SetSmilFillAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetSmilFillAttributeValue 多載。
    /// </summary>
    public void SetSmilFillAttributeValue(string localName, string namespaceUri, OdfSmilFill value) => SetSmilFillAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetSmilFillAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetSmilFillAttributeValue 多載。
    /// </summary>
    public void SetSmilFillAttributeValue(string localName, string namespaceUri, OdfSmilFill value, string? prefix) => SetSmilFillAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets smil fill attribute value.
    /// 設定具有 schema awareness 的 SMIL 動畫填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 SMIL 動畫填滿</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetSmilFillAttributeValue(string localName, string namespaceUri, OdfSmilFill value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF SMIL 動畫填滿。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDrawFillImageRefPointAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDrawFillImageRefPointAttributeValue 多載。
    /// </summary>
    public OdfDrawFillImageRefPoint? GetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri) => GetDrawFillImageRefPointAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets draw fill image ref point attribute value.
    /// 取得具有 schema awareness 的繪圖填滿圖片參照點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的繪圖填滿圖片參照點；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDrawFillImageRefPoint? GetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDrawFillImageRefPoint>(value);
    }
    /// <summary>
    /// Short overload of SetDrawFillImageRefPointAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDrawFillImageRefPointAttributeValue 多載。
    /// </summary>
    public void SetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfDrawFillImageRefPoint value) => SetDrawFillImageRefPointAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDrawFillImageRefPointAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDrawFillImageRefPointAttributeValue 多載。
    /// </summary>
    public void SetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfDrawFillImageRefPoint value, string? prefix) => SetDrawFillImageRefPointAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets draw fill image ref point attribute value.
    /// 設定具有 schema awareness 的繪圖填滿圖片參照點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的繪圖填滿圖片參照點</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfDrawFillImageRefPoint value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖填滿圖片參照點。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetDrawColorModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDrawColorModeAttributeValue 多載。
    /// </summary>
    public OdfDrawColorMode? GetDrawColorModeAttributeValue(string localName, string namespaceUri) => GetDrawColorModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets draw color mode attribute value.
    /// 取得具有 schema awareness 的繪圖色彩模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的繪圖色彩模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDrawColorMode? GetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDrawColorMode>(value);
    }
    /// <summary>
    /// Short overload of SetDrawColorModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDrawColorModeAttributeValue 多載。
    /// </summary>
    public void SetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfDrawColorMode value) => SetDrawColorModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDrawColorModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDrawColorModeAttributeValue 多載。
    /// </summary>
    public void SetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfDrawColorMode value, string? prefix) => SetDrawColorModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets draw color mode attribute value.
    /// 設定具有 schema awareness 的繪圖色彩模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的繪圖色彩模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfDrawColorMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 繪圖色彩模式。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleVerticalAlignAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleVerticalAlignAttributeValue 多載。
    /// </summary>
    public OdfStyleVerticalAlign? GetStyleVerticalAlignAttributeValue(string localName, string namespaceUri) => GetStyleVerticalAlignAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style vertical align attribute value.
    /// 取得具有 schema awareness 的樣式垂直對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式垂直對齊；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleVerticalAlign? GetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleVerticalAlign>(value);
    }
    /// <summary>
    /// Short overload of SetStyleVerticalAlignAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleVerticalAlignAttributeValue 多載。
    /// </summary>
    public void SetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfStyleVerticalAlign value) => SetStyleVerticalAlignAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleVerticalAlignAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleVerticalAlignAttributeValue 多載。
    /// </summary>
    public void SetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfStyleVerticalAlign value, string? prefix) => SetStyleVerticalAlignAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style vertical align attribute value.
    /// 設定具有 schema awareness 的樣式垂直對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式垂直對齊</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfStyleVerticalAlign value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式垂直對齊。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleVerticalPosAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleVerticalPosAttributeValue 多載。
    /// </summary>
    public OdfStyleVerticalPos? GetStyleVerticalPosAttributeValue(string localName, string namespaceUri) => GetStyleVerticalPosAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style vertical pos attribute value.
    /// 取得具有 schema awareness 的樣式垂直位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式垂直位置；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleVerticalPos? GetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleVerticalPos>(value);
    }
    /// <summary>
    /// Short overload of SetStyleVerticalPosAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleVerticalPosAttributeValue 多載。
    /// </summary>
    public void SetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfStyleVerticalPos value) => SetStyleVerticalPosAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleVerticalPosAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleVerticalPosAttributeValue 多載。
    /// </summary>
    public void SetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfStyleVerticalPos value, string? prefix) => SetStyleVerticalPosAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style vertical pos attribute value.
    /// 設定具有 schema awareness 的樣式垂直位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式垂直位置</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfStyleVerticalPos value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式垂直位置。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleVerticalRelAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleVerticalRelAttributeValue 多載。
    /// </summary>
    public OdfStyleVerticalRel? GetStyleVerticalRelAttributeValue(string localName, string namespaceUri) => GetStyleVerticalRelAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style vertical rel attribute value.
    /// 取得具有 schema awareness 的樣式垂直相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式垂直相對基準；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleVerticalRel? GetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleVerticalRel>(value);
    }
    /// <summary>
    /// Short overload of SetStyleVerticalRelAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleVerticalRelAttributeValue 多載。
    /// </summary>
    public void SetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfStyleVerticalRel value) => SetStyleVerticalRelAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleVerticalRelAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleVerticalRelAttributeValue 多載。
    /// </summary>
    public void SetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfStyleVerticalRel value, string? prefix) => SetStyleVerticalRelAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style vertical rel attribute value.
    /// 設定具有 schema awareness 的樣式垂直相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式垂直相對基準</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfStyleVerticalRel value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式垂直相對基準。"), prefix, version);
    }

    /// <summary>
    /// Short overload of GetStyleHorizontalPosAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetStyleHorizontalPosAttributeValue 多載。
    /// </summary>
    public OdfStyleHorizontalPos? GetStyleHorizontalPosAttributeValue(string localName, string namespaceUri) => GetStyleHorizontalPosAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets style horizontal pos attribute value.
    /// 取得具有 schema awareness 的樣式水平位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式水平位置；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleHorizontalPos? GetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleHorizontalPos>(value);
    }
    /// <summary>
    /// Short overload of SetStyleHorizontalPosAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetStyleHorizontalPosAttributeValue 多載。
    /// </summary>
    public void SetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalPos value) => SetStyleHorizontalPosAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetStyleHorizontalPosAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetStyleHorizontalPosAttributeValue 多載。
    /// </summary>
    public void SetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalPos value, string? prefix) => SetStyleHorizontalPosAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets style horizontal pos attribute value.
    /// 設定具有 schema awareness 的樣式水平位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式水平位置</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalPos value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式水平位置。"), prefix, version);
    }


    #endregion
}

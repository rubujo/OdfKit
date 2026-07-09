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
    #region Attribute Values - Text List & Reference
    /// <summary>
    /// Short overload of GetTextLabelFollowedByAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextLabelFollowedByAttributeValue 多載。
    /// </summary>
    public OdfTextLabelFollowedBy? GetTextLabelFollowedByAttributeValue(string localName, string namespaceUri) => GetTextLabelFollowedByAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text label followed by attribute value.
    /// 取得具有 schema awareness 的文字清單標籤後接屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字清單標籤後接設定；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextLabelFollowedBy? GetTextLabelFollowedByAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextLabelFollowedBy>(value, OdfElementSchemaRegistry.TryParseTextLabelFollowedBy);
    }
    /// <summary>
    /// Short overload of SetTextLabelFollowedByAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextLabelFollowedByAttributeValue 多載。
    /// </summary>
    public void SetTextLabelFollowedByAttributeValue(string localName, string namespaceUri, OdfTextLabelFollowedBy value) => SetTextLabelFollowedByAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextLabelFollowedByAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextLabelFollowedByAttributeValue 多載。
    /// </summary>
    public void SetTextLabelFollowedByAttributeValue(string localName, string namespaceUri, OdfTextLabelFollowedBy value, string? prefix) => SetTextLabelFollowedByAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text label followed by attribute value.
    /// 設定具有 schema awareness 的文字清單標籤後接屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字清單標籤後接設定</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextLabelFollowedByAttributeValue(string localName, string namespaceUri, OdfTextLabelFollowedBy value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextLabelFollowedBy(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextListLevelPositionModeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextListLevelPositionModeAttributeValue 多載。
    /// </summary>
    public OdfTextListLevelPositionMode? GetTextListLevelPositionModeAttributeValue(string localName, string namespaceUri) => GetTextListLevelPositionModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text list level position mode attribute value.
    /// 取得具有 schema awareness 的文字清單層級定位模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字清單層級定位模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextListLevelPositionMode? GetTextListLevelPositionModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextListLevelPositionMode>(value, OdfElementSchemaRegistry.TryParseTextListLevelPositionMode);
    }
    /// <summary>
    /// Short overload of SetTextListLevelPositionModeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextListLevelPositionModeAttributeValue 多載。
    /// </summary>
    public void SetTextListLevelPositionModeAttributeValue(string localName, string namespaceUri, OdfTextListLevelPositionMode value) => SetTextListLevelPositionModeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextListLevelPositionModeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextListLevelPositionModeAttributeValue 多載。
    /// </summary>
    public void SetTextListLevelPositionModeAttributeValue(string localName, string namespaceUri, OdfTextListLevelPositionMode value, string? prefix) => SetTextListLevelPositionModeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text list level position mode attribute value.
    /// 設定具有 schema awareness 的文字清單層級定位模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字清單層級定位模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextListLevelPositionModeAttributeValue(string localName, string namespaceUri, OdfTextListLevelPositionMode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextListLevelPositionMode(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextIndexScopeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextIndexScopeAttributeValue 多載。
    /// </summary>
    public OdfTextIndexScope? GetTextIndexScopeAttributeValue(string localName, string namespaceUri) => GetTextIndexScopeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text index scope attribute value.
    /// 取得具有 schema awareness 的文字索引範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字索引範圍；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextIndexScope? GetTextIndexScopeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextIndexScope>(value, OdfElementSchemaRegistry.TryParseTextIndexScope);
    }
    /// <summary>
    /// Short overload of SetTextIndexScopeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextIndexScopeAttributeValue 多載。
    /// </summary>
    public void SetTextIndexScopeAttributeValue(string localName, string namespaceUri, OdfTextIndexScope value) => SetTextIndexScopeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextIndexScopeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextIndexScopeAttributeValue 多載。
    /// </summary>
    public void SetTextIndexScopeAttributeValue(string localName, string namespaceUri, OdfTextIndexScope value, string? prefix) => SetTextIndexScopeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text index scope attribute value.
    /// 設定具有 schema awareness 的文字索引範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字索引範圍</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextIndexScopeAttributeValue(string localName, string namespaceUri, OdfTextIndexScope value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextIndexScope(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextTableTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextTableTypeAttributeValue 多載。
    /// </summary>
    public OdfTextTableType? GetTextTableTypeAttributeValue(string localName, string namespaceUri) => GetTextTableTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text table type attribute value.
    /// 取得具有 schema awareness 的文字資料表來源類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字資料表來源類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextTableType? GetTextTableTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextTableType>(value, OdfElementSchemaRegistry.TryParseTextTableType);
    }
    /// <summary>
    /// Short overload of SetTextTableTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextTableTypeAttributeValue 多載。
    /// </summary>
    public void SetTextTableTypeAttributeValue(string localName, string namespaceUri, OdfTextTableType value) => SetTextTableTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextTableTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextTableTypeAttributeValue 多載。
    /// </summary>
    public void SetTextTableTypeAttributeValue(string localName, string namespaceUri, OdfTextTableType value, string? prefix) => SetTextTableTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text table type attribute value.
    /// 設定具有 schema awareness 的文字資料表來源類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字資料表來源類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextTableTypeAttributeValue(string localName, string namespaceUri, OdfTextTableType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextTableType(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextAnchorTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextAnchorTypeAttributeValue 多載。
    /// </summary>
    public OdfTextAnchorType? GetTextAnchorTypeAttributeValue(string localName, string namespaceUri) => GetTextAnchorTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text anchor type attribute value.
    /// 取得具有 schema awareness 的文字錨定類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字錨定類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextAnchorType? GetTextAnchorTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextAnchorType>(value, OdfElementSchemaRegistry.TryParseTextAnchorType);
    }
    /// <summary>
    /// Short overload of SetTextAnchorTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextAnchorTypeAttributeValue 多載。
    /// </summary>
    public void SetTextAnchorTypeAttributeValue(string localName, string namespaceUri, OdfTextAnchorType value) => SetTextAnchorTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextAnchorTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextAnchorTypeAttributeValue 多載。
    /// </summary>
    public void SetTextAnchorTypeAttributeValue(string localName, string namespaceUri, OdfTextAnchorType value, string? prefix) => SetTextAnchorTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text anchor type attribute value.
    /// 設定具有 schema awareness 的文字錨定類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字錨定類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextAnchorTypeAttributeValue(string localName, string namespaceUri, OdfTextAnchorType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextAnchorType(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextNoteClassAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextNoteClassAttributeValue 多載。
    /// </summary>
    public OdfTextNoteClass? GetTextNoteClassAttributeValue(string localName, string namespaceUri) => GetTextNoteClassAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text note class attribute value.
    /// 取得具有 schema awareness 的文字註解類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字註解類別；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextNoteClass? GetTextNoteClassAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextNoteClass>(value, OdfElementSchemaRegistry.TryParseTextNoteClass);
    }
    /// <summary>
    /// Short overload of SetTextNoteClassAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextNoteClassAttributeValue 多載。
    /// </summary>
    public void SetTextNoteClassAttributeValue(string localName, string namespaceUri, OdfTextNoteClass value) => SetTextNoteClassAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextNoteClassAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextNoteClassAttributeValue 多載。
    /// </summary>
    public void SetTextNoteClassAttributeValue(string localName, string namespaceUri, OdfTextNoteClass value, string? prefix) => SetTextNoteClassAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text note class attribute value.
    /// 設定具有 schema awareness 的文字註解類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字註解類別</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextNoteClassAttributeValue(string localName, string namespaceUri, OdfTextNoteClass value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextNoteClass(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextSelectPageAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextSelectPageAttributeValue 多載。
    /// </summary>
    public OdfTextSelectPage? GetTextSelectPageAttributeValue(string localName, string namespaceUri) => GetTextSelectPageAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text select page attribute value.
    /// 取得具有 schema awareness 的文字頁面選取屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字頁面選取；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextSelectPage? GetTextSelectPageAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextSelectPage>(value, OdfElementSchemaRegistry.TryParseTextSelectPage);
    }
    /// <summary>
    /// Short overload of SetTextSelectPageAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextSelectPageAttributeValue 多載。
    /// </summary>
    public void SetTextSelectPageAttributeValue(string localName, string namespaceUri, OdfTextSelectPage value) => SetTextSelectPageAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextSelectPageAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextSelectPageAttributeValue 多載。
    /// </summary>
    public void SetTextSelectPageAttributeValue(string localName, string namespaceUri, OdfTextSelectPage value, string? prefix) => SetTextSelectPageAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text select page attribute value.
    /// 設定具有 schema awareness 的文字頁面選取屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字頁面選取</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextSelectPageAttributeValue(string localName, string namespaceUri, OdfTextSelectPage value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextSelectPage(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextReferenceFormatAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextReferenceFormatAttributeValue 多載。
    /// </summary>
    public OdfTextReferenceFormat? GetTextReferenceFormatAttributeValue(string localName, string namespaceUri) => GetTextReferenceFormatAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text reference format attribute value.
    /// 取得具有 schema awareness 的文字參照格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字參照格式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextReferenceFormat? GetTextReferenceFormatAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextReferenceFormat>(value, OdfElementSchemaRegistry.TryParseTextReferenceFormat);
    }
    /// <summary>
    /// Short overload of SetTextReferenceFormatAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextReferenceFormatAttributeValue 多載。
    /// </summary>
    public void SetTextReferenceFormatAttributeValue(string localName, string namespaceUri, OdfTextReferenceFormat value) => SetTextReferenceFormatAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextReferenceFormatAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextReferenceFormatAttributeValue 多載。
    /// </summary>
    public void SetTextReferenceFormatAttributeValue(string localName, string namespaceUri, OdfTextReferenceFormat value, string? prefix) => SetTextReferenceFormatAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text reference format attribute value.
    /// 設定具有 schema awareness 的文字參照格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字參照格式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextReferenceFormatAttributeValue(string localName, string namespaceUri, OdfTextReferenceFormat value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextReferenceFormat(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextStartNumberingAtAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextStartNumberingAtAttributeValue 多載。
    /// </summary>
    public OdfTextStartNumberingAt? GetTextStartNumberingAtAttributeValue(string localName, string namespaceUri) => GetTextStartNumberingAtAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text start numbering at attribute value.
    /// 取得具有 schema awareness 的文字起始編號範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字起始編號範圍；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextStartNumberingAt? GetTextStartNumberingAtAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextStartNumberingAt>(value, OdfElementSchemaRegistry.TryParseTextStartNumberingAt);
    }
    /// <summary>
    /// Short overload of SetTextStartNumberingAtAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextStartNumberingAtAttributeValue 多載。
    /// </summary>
    public void SetTextStartNumberingAtAttributeValue(string localName, string namespaceUri, OdfTextStartNumberingAt value) => SetTextStartNumberingAtAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextStartNumberingAtAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextStartNumberingAtAttributeValue 多載。
    /// </summary>
    public void SetTextStartNumberingAtAttributeValue(string localName, string namespaceUri, OdfTextStartNumberingAt value, string? prefix) => SetTextStartNumberingAtAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text start numbering at attribute value.
    /// 設定具有 schema awareness 的文字起始編號範圍屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字起始編號範圍</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextStartNumberingAtAttributeValue(string localName, string namespaceUri, OdfTextStartNumberingAt value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextStartNumberingAt(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextFootnotesPositionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextFootnotesPositionAttributeValue 多載。
    /// </summary>
    public OdfTextFootnotesPosition? GetTextFootnotesPositionAttributeValue(string localName, string namespaceUri) => GetTextFootnotesPositionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text footnotes position attribute value.
    /// 取得具有 schema awareness 的文字註腳位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字註腳位置；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextFootnotesPosition? GetTextFootnotesPositionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextFootnotesPosition>(value, OdfElementSchemaRegistry.TryParseTextFootnotesPosition);
    }
    /// <summary>
    /// Short overload of SetTextFootnotesPositionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextFootnotesPositionAttributeValue 多載。
    /// </summary>
    public void SetTextFootnotesPositionAttributeValue(string localName, string namespaceUri, OdfTextFootnotesPosition value) => SetTextFootnotesPositionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextFootnotesPositionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextFootnotesPositionAttributeValue 多載。
    /// </summary>
    public void SetTextFootnotesPositionAttributeValue(string localName, string namespaceUri, OdfTextFootnotesPosition value, string? prefix) => SetTextFootnotesPositionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text footnotes position attribute value.
    /// 設定具有 schema awareness 的文字註腳位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字註腳位置</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextFootnotesPositionAttributeValue(string localName, string namespaceUri, OdfTextFootnotesPosition value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextFootnotesPosition(value), prefix, version);
    }



    #endregion
}

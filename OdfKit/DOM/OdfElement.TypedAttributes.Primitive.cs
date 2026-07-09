using OdfKit.Compliance;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfElement API.
/// 提供 OdfElement API。
/// </summary>

public partial class OdfElement
{
    #region Typed Attribute Accessors - Primitive
    /// <summary>
    /// Short overload of GetInt32AttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetInt32AttributeValue 多載。
    /// </summary>
    public int GetInt32AttributeValue(string localName, string namespaceUri) => GetInt32AttributeValue(localName, namespaceUri, 0, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of GetInt32AttributeValue that accepts localName, namespaceUri, and defaultValue; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 defaultValue；其餘可選參數使用預設值並轉呼叫最長 GetInt32AttributeValue 多載。
    /// </summary>
    public int GetInt32AttributeValue(string localName, string namespaceUri, int defaultValue) => GetInt32AttributeValue(localName, namespaceUri, defaultValue, OdfVersion.Odf14);


    /// <summary>
    /// Gets int 32 attribute value.
    /// 取得具有 schema awareness 的 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="defaultValue">屬性不存在或格式無效時的預設值</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的整數值</returns>
    public int GetInt32AttributeValue(string localName, string namespaceUri, int defaultValue, OdfVersion version) => OdfElementPrimitiveAttributeAccess.GetInt32(GetAttributeValue(localName, namespaceUri, version), defaultValue);

    /// <summary>
    /// Short overload of GetNullableInt32AttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetNullableInt32AttributeValue 多載。
    /// </summary>
    public int? GetNullableInt32AttributeValue(string localName, string namespaceUri) => GetNullableInt32AttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets nullable int 32 attribute value.
    /// 取得具有 schema awareness 的可空 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的整數值；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public int? GetNullableInt32AttributeValue(string localName, string namespaceUri, OdfVersion version) => OdfElementPrimitiveAttributeAccess.GetNullableInt32(GetAttributeValue(localName, namespaceUri, version));
    /// <summary>
    /// Short overload of SetInt32AttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetInt32AttributeValue 多載。
    /// </summary>
    public void SetInt32AttributeValue(string localName, string namespaceUri, int value) => SetInt32AttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetInt32AttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetInt32AttributeValue 多載。
    /// </summary>
    public void SetInt32AttributeValue(string localName, string namespaceUri, int value, string? prefix) => SetInt32AttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets int 32 attribute value.
    /// 設定具有 schema awareness 的 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的整數值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetInt32AttributeValue(string localName, string namespaceUri, int value, string? prefix, OdfVersion version) => SetAttributeValue(localName, namespaceUri, OdfElementPrimitiveAttributeAccess.FormatInt32(value), prefix, version);

    /// <summary>
    /// Short overload of GetBooleanAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetBooleanAttributeValue 多載。
    /// </summary>
    public bool? GetBooleanAttributeValue(string localName, string namespaceUri) => GetBooleanAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets boolean attribute value.
    /// 取得具有 schema awareness 的布林屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的布林值；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public bool? GetBooleanAttributeValue(string localName, string namespaceUri, OdfVersion version) => OdfElementPrimitiveAttributeAccess.GetBoolean(GetAttributeValue(localName, namespaceUri, version));
    /// <summary>
    /// Short overload of SetBooleanAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetBooleanAttributeValue 多載。
    /// </summary>
    public void SetBooleanAttributeValue(string localName, string namespaceUri, bool value) => SetBooleanAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetBooleanAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetBooleanAttributeValue 多載。
    /// </summary>
    public void SetBooleanAttributeValue(string localName, string namespaceUri, bool value, string? prefix) => SetBooleanAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets boolean attribute value.
    /// 設定具有 schema awareness 的布林屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的布林值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetBooleanAttributeValue(string localName, string namespaceUri, bool value, string? prefix, OdfVersion version) => SetAttributeValue(localName, namespaceUri, OdfElementPrimitiveAttributeAccess.FormatBoolean(value), prefix, version);

    /// <summary>
    /// Short overload of GetDecimalAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetDecimalAttributeValue 多載。
    /// </summary>
    public decimal? GetDecimalAttributeValue(string localName, string namespaceUri) => GetDecimalAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets decimal attribute value.
    /// 取得具有 schema awareness 的十進位數值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的十進位數值；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public decimal? GetDecimalAttributeValue(string localName, string namespaceUri, OdfVersion version) => OdfElementPrimitiveAttributeAccess.GetDecimal(GetAttributeValue(localName, namespaceUri, version));
    /// <summary>
    /// Short overload of SetDecimalAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetDecimalAttributeValue 多載。
    /// </summary>
    public void SetDecimalAttributeValue(string localName, string namespaceUri, decimal value) => SetDecimalAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetDecimalAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetDecimalAttributeValue 多載。
    /// </summary>
    public void SetDecimalAttributeValue(string localName, string namespaceUri, decimal value, string? prefix) => SetDecimalAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets decimal attribute value.
    /// 設定具有 schema awareness 的十進位數值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的十進位數值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDecimalAttributeValue(string localName, string namespaceUri, decimal value, string? prefix, OdfVersion version) => SetAttributeValue(localName, namespaceUri, OdfElementPrimitiveAttributeAccess.FormatDecimal(value), prefix, version);


    #endregion
}

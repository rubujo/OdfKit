using OdfKit.Compliance;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Typed Attribute Accessors - Primitive

    /// <summary>
    /// 取得具有 schema awareness 的 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="defaultValue">屬性不存在或格式無效時的預設值</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的整數值</returns>
    public int GetInt32AttributeValue(string localName, string namespaceUri, int defaultValue = 0, OdfVersion version = OdfVersion.Odf14)
        => OdfElementPrimitiveAttributeAccess.GetInt32(GetAttributeValue(localName, namespaceUri, version), defaultValue);

    /// <summary>
    /// 取得具有 schema awareness 的可空 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的整數值；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public int? GetNullableInt32AttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementPrimitiveAttributeAccess.GetNullableInt32(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的整數值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetInt32AttributeValue(string localName, string namespaceUri, int value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementPrimitiveAttributeAccess.FormatInt32(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的布林屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的布林值；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public bool? GetBooleanAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementPrimitiveAttributeAccess.GetBoolean(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的布林屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的布林值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetBooleanAttributeValue(string localName, string namespaceUri, bool value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementPrimitiveAttributeAccess.FormatBoolean(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的十進位數值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的十進位數值；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public decimal? GetDecimalAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementPrimitiveAttributeAccess.GetDecimal(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的十進位數值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的十進位數值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDecimalAttributeValue(string localName, string namespaceUri, decimal value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementPrimitiveAttributeAccess.FormatDecimal(value), prefix, version);

    #endregion
}

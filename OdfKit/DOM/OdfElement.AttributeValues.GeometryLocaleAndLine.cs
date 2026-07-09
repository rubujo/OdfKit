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
    #region Attribute Values - Geometry, Locale & Line
    /// <summary>
    /// Short overload of GetVector3DAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetVector3DAttributeValue 多載。
    /// </summary>
    public OdfVector3D? GetVector3DAttributeValue(string localName, string namespaceUri) => GetVector3DAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets vector 3 D attribute value.
    /// 取得具有 schema awareness 的三維向量屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的三維向量；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfVector3D? GetVector3DAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfVector3D>(value, OdfVector3D.TryParse);
    }
    /// <summary>
    /// Short overload of SetVector3DAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetVector3DAttributeValue 多載。
    /// </summary>
    public void SetVector3DAttributeValue(string localName, string namespaceUri, OdfVector3D value) => SetVector3DAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetVector3DAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetVector3DAttributeValue 多載。
    /// </summary>
    public void SetVector3DAttributeValue(string localName, string namespaceUri, OdfVector3D value, string? prefix) => SetVector3DAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets vector 3 D attribute value.
    /// 設定具有 schema awareness 的三維向量屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的三維向量</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetVector3DAttributeValue(string localName, string namespaceUri, OdfVector3D value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetPoint3DAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPoint3DAttributeValue 多載。
    /// </summary>
    public OdfPoint3D? GetPoint3DAttributeValue(string localName, string namespaceUri) => GetPoint3DAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets point 3 D attribute value.
    /// 取得具有 schema awareness 的三維點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的三維點；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfPoint3D? GetPoint3DAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfPoint3D>(value, OdfPoint3D.TryParse);
    }
    /// <summary>
    /// Short overload of SetPoint3DAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPoint3DAttributeValue 多載。
    /// </summary>
    public void SetPoint3DAttributeValue(string localName, string namespaceUri, OdfPoint3D value) => SetPoint3DAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPoint3DAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPoint3DAttributeValue 多載。
    /// </summary>
    public void SetPoint3DAttributeValue(string localName, string namespaceUri, OdfPoint3D value, string? prefix) => SetPoint3DAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets point 3 D attribute value.
    /// 設定具有 schema awareness 的三維點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的三維點</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPoint3DAttributeValue(string localName, string namespaceUri, OdfPoint3D value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetPointListAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPointListAttributeValue 多載。
    /// </summary>
    public OdfPointList? GetPointListAttributeValue(string localName, string namespaceUri) => GetPointListAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets point list attribute value.
    /// 取得具有 schema awareness 的二維座標清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的二維座標清單；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfPointList? GetPointListAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfPointList>(value, OdfPointList.TryParse);
    }
    /// <summary>
    /// Short overload of SetPointListAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPointListAttributeValue 多載。
    /// </summary>
    public void SetPointListAttributeValue(string localName, string namespaceUri, OdfPointList value) => SetPointListAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPointListAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPointListAttributeValue 多載。
    /// </summary>
    public void SetPointListAttributeValue(string localName, string namespaceUri, OdfPointList value, string? prefix) => SetPointListAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets point list attribute value.
    /// 設定具有 schema awareness 的二維座標清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的二維座標清單</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPointListAttributeValue(string localName, string namespaceUri, OdfPointList value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetXmlNameAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetXmlNameAttributeValue 多載。
    /// </summary>
    public OdfXmlName? GetXmlNameAttributeValue(string localName, string namespaceUri) => GetXmlNameAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets xml name attribute value.
    /// 取得具有 schema awareness 的 XML 名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 XML 名稱；若屬性不存在或不是有效 XML <c>NCName</c> 則為 <see langword="null"/></returns>
    public OdfXmlName? GetXmlNameAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfXmlName>(value, OdfXmlName.TryParse);
    }
    /// <summary>
    /// Short overload of SetXmlNameAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetXmlNameAttributeValue 多載。
    /// </summary>
    public void SetXmlNameAttributeValue(string localName, string namespaceUri, OdfXmlName value) => SetXmlNameAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetXmlNameAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetXmlNameAttributeValue 多載。
    /// </summary>
    public void SetXmlNameAttributeValue(string localName, string namespaceUri, OdfXmlName value, string? prefix) => SetXmlNameAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets xml name attribute value.
    /// 設定具有 schema awareness 的 XML 名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 XML 名稱</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetXmlNameAttributeValue(string localName, string namespaceUri, OdfXmlName value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetLanguageCodeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetLanguageCodeAttributeValue 多載。
    /// </summary>
    public OdfLanguageCode? GetLanguageCodeAttributeValue(string localName, string namespaceUri) => GetLanguageCodeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets language code attribute value.
    /// 取得具有 schema awareness 的語言代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的語言代碼；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfLanguageCode? GetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfLanguageCode>(value, OdfLanguageCode.TryParse);
    }
    /// <summary>
    /// Short overload of SetLanguageCodeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetLanguageCodeAttributeValue 多載。
    /// </summary>
    public void SetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfLanguageCode value) => SetLanguageCodeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetLanguageCodeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetLanguageCodeAttributeValue 多載。
    /// </summary>
    public void SetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfLanguageCode value, string? prefix) => SetLanguageCodeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets language code attribute value.
    /// 設定具有 schema awareness 的語言代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的語言代碼</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfLanguageCode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetCountryCodeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetCountryCodeAttributeValue 多載。
    /// </summary>
    public OdfCountryCode? GetCountryCodeAttributeValue(string localName, string namespaceUri) => GetCountryCodeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets country code attribute value.
    /// 取得具有 schema awareness 的國別代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的國別代碼；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfCountryCode? GetCountryCodeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfCountryCode>(value, OdfCountryCode.TryParse);
    }
    /// <summary>
    /// Short overload of SetCountryCodeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetCountryCodeAttributeValue 多載。
    /// </summary>
    public void SetCountryCodeAttributeValue(string localName, string namespaceUri, OdfCountryCode value) => SetCountryCodeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetCountryCodeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetCountryCodeAttributeValue 多載。
    /// </summary>
    public void SetCountryCodeAttributeValue(string localName, string namespaceUri, OdfCountryCode value, string? prefix) => SetCountryCodeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets country code attribute value.
    /// 設定具有 schema awareness 的國別代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的國別代碼</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetCountryCodeAttributeValue(string localName, string namespaceUri, OdfCountryCode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetScriptCodeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetScriptCodeAttributeValue 多載。
    /// </summary>
    public OdfScriptCode? GetScriptCodeAttributeValue(string localName, string namespaceUri) => GetScriptCodeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets script code attribute value.
    /// 取得具有 schema awareness 的文字系統代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字系統代碼；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfScriptCode? GetScriptCodeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfScriptCode>(value, OdfScriptCode.TryParse);
    }
    /// <summary>
    /// Short overload of SetScriptCodeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetScriptCodeAttributeValue 多載。
    /// </summary>
    public void SetScriptCodeAttributeValue(string localName, string namespaceUri, OdfScriptCode value) => SetScriptCodeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetScriptCodeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetScriptCodeAttributeValue 多載。
    /// </summary>
    public void SetScriptCodeAttributeValue(string localName, string namespaceUri, OdfScriptCode value, string? prefix) => SetScriptCodeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets script code attribute value.
    /// 設定具有 schema awareness 的文字系統代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字系統代碼</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetScriptCodeAttributeValue(string localName, string namespaceUri, OdfScriptCode value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetLanguageTagAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetLanguageTagAttributeValue 多載。
    /// </summary>
    public OdfLanguageTag? GetLanguageTagAttributeValue(string localName, string namespaceUri) => GetLanguageTagAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets language tag attribute value.
    /// 取得具有 schema awareness 的語言標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的語言標記；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfLanguageTag? GetLanguageTagAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfLanguageTag>(value, OdfLanguageTag.TryParse);
    }
    /// <summary>
    /// Short overload of SetLanguageTagAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetLanguageTagAttributeValue 多載。
    /// </summary>
    public void SetLanguageTagAttributeValue(string localName, string namespaceUri, OdfLanguageTag value) => SetLanguageTagAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetLanguageTagAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetLanguageTagAttributeValue 多載。
    /// </summary>
    public void SetLanguageTagAttributeValue(string localName, string namespaceUri, OdfLanguageTag value, string? prefix) => SetLanguageTagAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets language tag attribute value.
    /// 設定具有 schema awareness 的語言標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的語言標記</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLanguageTagAttributeValue(string localName, string namespaceUri, OdfLanguageTag value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetNamespacedTokenAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetNamespacedTokenAttributeValue 多載。
    /// </summary>
    public OdfNamespacedToken? GetNamespacedTokenAttributeValue(string localName, string namespaceUri) => GetNamespacedTokenAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets namespaced token attribute value.
    /// 取得具有 schema awareness 的命名空間 token 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的命名空間 token；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfNamespacedToken? GetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfNamespacedToken>(value, OdfNamespacedToken.TryParse);
    }
    /// <summary>
    /// Short overload of SetNamespacedTokenAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetNamespacedTokenAttributeValue 多載。
    /// </summary>
    public void SetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfNamespacedToken value) => SetNamespacedTokenAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetNamespacedTokenAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetNamespacedTokenAttributeValue 多載。
    /// </summary>
    public void SetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfNamespacedToken value, string? prefix) => SetNamespacedTokenAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets namespaced token attribute value.
    /// 設定具有 schema awareness 的命名空間 token 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的命名空間 token</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfNamespacedToken value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetCharacterAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetCharacterAttributeValue 多載。
    /// </summary>
    public OdfCharacter? GetCharacterAttributeValue(string localName, string namespaceUri) => GetCharacterAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets character attribute value.
    /// 取得具有 schema awareness 的單一字元屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的單一字元；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfCharacter? GetCharacterAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfCharacter>(value, OdfCharacter.TryParse);
    }
    /// <summary>
    /// Short overload of SetCharacterAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetCharacterAttributeValue 多載。
    /// </summary>
    public void SetCharacterAttributeValue(string localName, string namespaceUri, OdfCharacter value) => SetCharacterAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetCharacterAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetCharacterAttributeValue 多載。
    /// </summary>
    public void SetCharacterAttributeValue(string localName, string namespaceUri, OdfCharacter value, string? prefix) => SetCharacterAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets character attribute value.
    /// 設定具有 schema awareness 的單一字元屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的單一字元</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetCharacterAttributeValue(string localName, string namespaceUri, OdfCharacter value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextEncodingAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextEncodingAttributeValue 多載。
    /// </summary>
    public OdfTextEncoding? GetTextEncodingAttributeValue(string localName, string namespaceUri) => GetTextEncodingAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text encoding attribute value.
    /// 取得具有 schema awareness 的文字編碼名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字編碼名稱；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfTextEncoding? GetTextEncodingAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfTextEncoding>(value, OdfTextEncoding.TryParse);
    }
    /// <summary>
    /// Short overload of SetTextEncodingAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextEncodingAttributeValue 多載。
    /// </summary>
    public void SetTextEncodingAttributeValue(string localName, string namespaceUri, OdfTextEncoding value) => SetTextEncodingAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextEncodingAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextEncodingAttributeValue 多載。
    /// </summary>
    public void SetTextEncodingAttributeValue(string localName, string namespaceUri, OdfTextEncoding value, string? prefix) => SetTextEncodingAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text encoding attribute value.
    /// 設定具有 schema awareness 的文字編碼名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字編碼名稱</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextEncodingAttributeValue(string localName, string namespaceUri, OdfTextEncoding value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetTargetFrameNameAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTargetFrameNameAttributeValue 多載。
    /// </summary>
    public OdfTargetFrameName? GetTargetFrameNameAttributeValue(string localName, string namespaceUri) => GetTargetFrameNameAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets target frame name attribute value.
    /// 取得具有 schema awareness 的目標框架名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的目標框架名稱；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfTargetFrameName? GetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfTargetFrameName>(value, OdfTargetFrameName.TryParse);
    }
    /// <summary>
    /// Short overload of SetTargetFrameNameAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTargetFrameNameAttributeValue 多載。
    /// </summary>
    public void SetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfTargetFrameName value) => SetTargetFrameNameAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTargetFrameNameAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTargetFrameNameAttributeValue 多載。
    /// </summary>
    public void SetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfTargetFrameName value, string? prefix) => SetTargetFrameNameAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets target frame name attribute value.
    /// 設定具有 schema awareness 的目標框架名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的目標框架名稱</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfTargetFrameName value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetLineStyleAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetLineStyleAttributeValue 多載。
    /// </summary>
    public OdfLineStyle? GetLineStyleAttributeValue(string localName, string namespaceUri) => GetLineStyleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets line style attribute value.
    /// 取得具有 schema awareness 的線條樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的線條樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfLineStyle? GetLineStyleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfLineStyle>(value, OdfElementSchemaRegistry.TryParseLineStyle);
    }
    /// <summary>
    /// Short overload of SetLineStyleAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetLineStyleAttributeValue 多載。
    /// </summary>
    public void SetLineStyleAttributeValue(string localName, string namespaceUri, OdfLineStyle value) => SetLineStyleAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetLineStyleAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetLineStyleAttributeValue 多載。
    /// </summary>
    public void SetLineStyleAttributeValue(string localName, string namespaceUri, OdfLineStyle value, string? prefix) => SetLineStyleAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets line style attribute value.
    /// 設定具有 schema awareness 的線條樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的線條樣式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLineStyleAttributeValue(string localName, string namespaceUri, OdfLineStyle value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatLineStyle(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetLineTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetLineTypeAttributeValue 多載。
    /// </summary>
    public OdfLineType? GetLineTypeAttributeValue(string localName, string namespaceUri) => GetLineTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets line type attribute value.
    /// 取得具有 schema awareness 的線條類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的線條類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfLineType? GetLineTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfLineType>(value, OdfElementSchemaRegistry.TryParseLineType);
    }
    /// <summary>
    /// Short overload of SetLineTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetLineTypeAttributeValue 多載。
    /// </summary>
    public void SetLineTypeAttributeValue(string localName, string namespaceUri, OdfLineType value) => SetLineTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetLineTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetLineTypeAttributeValue 多載。
    /// </summary>
    public void SetLineTypeAttributeValue(string localName, string namespaceUri, OdfLineType value, string? prefix) => SetLineTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets line type attribute value.
    /// 設定具有 schema awareness 的線條類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的線條類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLineTypeAttributeValue(string localName, string namespaceUri, OdfLineType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatLineType(value), prefix, version);
    }


    #endregion
}

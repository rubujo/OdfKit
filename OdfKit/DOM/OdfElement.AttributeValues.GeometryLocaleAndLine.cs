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
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfVector3D? GetVector3DAttributeValue(string localName, string namespaceUri) => GetVector3DAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetVector3DAttributeValue operation.
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
    /// Executes the SetVector3DAttributeValue operation.
    /// 設定具有 schema awareness 的三維向量屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的三維向量</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetVector3DAttributeValue(string localName, string namespaceUri, OdfVector3D value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfPoint3D? GetPoint3DAttributeValue(string localName, string namespaceUri) => GetPoint3DAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetPoint3DAttributeValue operation.
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
    /// Executes the SetPoint3DAttributeValue operation.
    /// 設定具有 schema awareness 的三維點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的三維點</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPoint3DAttributeValue(string localName, string namespaceUri, OdfPoint3D value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfPointList? GetPointListAttributeValue(string localName, string namespaceUri) => GetPointListAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetPointListAttributeValue operation.
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
    /// Executes the SetPointListAttributeValue operation.
    /// 設定具有 schema awareness 的二維座標清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的二維座標清單</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPointListAttributeValue(string localName, string namespaceUri, OdfPointList value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfXmlName? GetXmlNameAttributeValue(string localName, string namespaceUri) => GetXmlNameAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetXmlNameAttributeValue operation.
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
    /// Executes the SetXmlNameAttributeValue operation.
    /// 設定具有 schema awareness 的 XML 名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 XML 名稱</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetXmlNameAttributeValue(string localName, string namespaceUri, OdfXmlName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfLanguageCode? GetLanguageCodeAttributeValue(string localName, string namespaceUri) => GetLanguageCodeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetLanguageCodeAttributeValue operation.
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
    /// Executes the SetLanguageCodeAttributeValue operation.
    /// 設定具有 schema awareness 的語言代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的語言代碼</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfLanguageCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfCountryCode? GetCountryCodeAttributeValue(string localName, string namespaceUri) => GetCountryCodeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetCountryCodeAttributeValue operation.
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
    /// Executes the SetCountryCodeAttributeValue operation.
    /// 設定具有 schema awareness 的國別代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的國別代碼</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetCountryCodeAttributeValue(string localName, string namespaceUri, OdfCountryCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfScriptCode? GetScriptCodeAttributeValue(string localName, string namespaceUri) => GetScriptCodeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetScriptCodeAttributeValue operation.
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
    /// Executes the SetScriptCodeAttributeValue operation.
    /// 設定具有 schema awareness 的文字系統代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字系統代碼</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetScriptCodeAttributeValue(string localName, string namespaceUri, OdfScriptCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfLanguageTag? GetLanguageTagAttributeValue(string localName, string namespaceUri) => GetLanguageTagAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetLanguageTagAttributeValue operation.
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
    /// Executes the SetLanguageTagAttributeValue operation.
    /// 設定具有 schema awareness 的語言標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的語言標記</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLanguageTagAttributeValue(string localName, string namespaceUri, OdfLanguageTag value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfNamespacedToken? GetNamespacedTokenAttributeValue(string localName, string namespaceUri) => GetNamespacedTokenAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetNamespacedTokenAttributeValue operation.
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
    /// Executes the SetNamespacedTokenAttributeValue operation.
    /// 設定具有 schema awareness 的命名空間 token 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的命名空間 token</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfNamespacedToken value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfCharacter? GetCharacterAttributeValue(string localName, string namespaceUri) => GetCharacterAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetCharacterAttributeValue operation.
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
    /// Executes the SetCharacterAttributeValue operation.
    /// 設定具有 schema awareness 的單一字元屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的單一字元</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetCharacterAttributeValue(string localName, string namespaceUri, OdfCharacter value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfTextEncoding? GetTextEncodingAttributeValue(string localName, string namespaceUri) => GetTextEncodingAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetTextEncodingAttributeValue operation.
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
    /// Executes the SetTextEncodingAttributeValue operation.
    /// 設定具有 schema awareness 的文字編碼名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字編碼名稱</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextEncodingAttributeValue(string localName, string namespaceUri, OdfTextEncoding value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfTargetFrameName? GetTargetFrameNameAttributeValue(string localName, string namespaceUri) => GetTargetFrameNameAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetTargetFrameNameAttributeValue operation.
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
    /// Executes the SetTargetFrameNameAttributeValue operation.
    /// 設定具有 schema awareness 的目標框架名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的目標框架名稱</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfTargetFrameName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfLineStyle? GetLineStyleAttributeValue(string localName, string namespaceUri) => GetLineStyleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetLineStyleAttributeValue operation.
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
    /// Executes the SetLineStyleAttributeValue operation.
    /// 設定具有 schema awareness 的線條樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的線條樣式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLineStyleAttributeValue(string localName, string namespaceUri, OdfLineStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatLineStyle(value), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfLineType? GetLineTypeAttributeValue(string localName, string namespaceUri) => GetLineTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetLineTypeAttributeValue operation.
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
    /// Executes the SetLineTypeAttributeValue operation.
    /// 設定具有 schema awareness 的線條類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的線條類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLineTypeAttributeValue(string localName, string namespaceUri, OdfLineType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatLineType(value), prefix, version);
    }

    #endregion
}

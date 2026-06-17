using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Accessors - XLink, Presentation & Table

    /// <summary>
    /// 取得具有 schema awareness 的 IRI 參照屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 IRI 參照；若屬性不存在或包含控制字元則為 <see langword="null"/>。</returns>
    public OdfIriReference? GetIriReferenceAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfIriReference.TryParse(value, out OdfIriReference iriReference) ? iriReference : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 IRI 參照屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 IRI 參照。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetIriReferenceAttributeValue(string localName, string namespaceUri, OdfIriReference value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 XLink 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 XLink 類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfXLinkType? GetXLinkTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfXLinkType>(value, OdfElementSchemaRegistry.TryParseXLinkType);
    }

    /// <summary>
    /// 設定具有 schema awareness 的 XLink 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 XLink 類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetXLinkTypeAttributeValue(string localName, string namespaceUri, OdfXLinkType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatXLinkType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 XLink 顯示行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 XLink 顯示行為；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfXLinkShow? GetXLinkShowAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfXLinkShow>(value, OdfElementSchemaRegistry.TryParseXLinkShow);
    }

    /// <summary>
    /// 設定具有 schema awareness 的 XLink 顯示行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 XLink 顯示行為。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetXLinkShowAttributeValue(string localName, string namespaceUri, OdfXLinkShow value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatXLinkShow(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 XLink 觸發行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 XLink 觸發行為；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfXLinkActuate? GetXLinkActuateAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfXLinkActuate>(value, OdfElementSchemaRegistry.TryParseXLinkActuate);
    }

    /// <summary>
    /// 設定具有 schema awareness 的 XLink 觸發行為屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 XLink 觸發行為。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetXLinkActuateAttributeValue(string localName, string namespaceUri, OdfXLinkActuate value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatXLinkActuate(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的數字樣式長短屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的數字樣式長短；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfNumberStyle? GetNumberStyleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfNumberStyle>(value, OdfElementSchemaRegistry.TryParseNumberStyle);
    }

    /// <summary>
    /// 設定具有 schema awareness 的數字樣式長短屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的數字樣式長短。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetNumberStyleAttributeValue(string localName, string namespaceUri, OdfNumberStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatNumberStyle(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格排序方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格排序方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableOrder? GetTableOrderAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableOrder>(value, OdfElementSchemaRegistry.TryParseTableOrder);
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格排序方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格排序方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableOrderAttributeValue(string localName, string namespaceUri, OdfTableOrder value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableOrder(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableType? GetTableTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTableType>(value, OdfElementSchemaRegistry.TryParseTableType);
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableTypeAttributeValue(string localName, string namespaceUri, OdfTableType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTableType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的簡報效果屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的簡報效果；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfPresentationEffect? GetPresentationEffectAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationEffect>(value, OdfElementSchemaRegistry.TryParsePresentationEffect);
    }

    /// <summary>
    /// 設定具有 schema awareness 的簡報效果屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的簡報效果。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPresentationEffectAttributeValue(string localName, string namespaceUri, OdfPresentationEffect value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationEffect(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的簡報速度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的簡報速度；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfPresentationSpeed? GetPresentationSpeedAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationSpeed>(value, OdfElementSchemaRegistry.TryParsePresentationSpeed);
    }

    /// <summary>
    /// 設定具有 schema awareness 的簡報速度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的簡報速度。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPresentationSpeedAttributeValue(string localName, string namespaceUri, OdfPresentationSpeed value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationSpeed(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的簡報動作屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的簡報動作；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfPresentationAction? GetPresentationActionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationAction>(value, OdfElementSchemaRegistry.TryParsePresentationAction);
    }

    /// <summary>
    /// 設定具有 schema awareness 的簡報動作屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的簡報動作。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPresentationActionAttributeValue(string localName, string namespaceUri, OdfPresentationAction value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationAction(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的簡報轉場類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的簡報轉場類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfPresentationTransitionType? GetPresentationTransitionTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationTransitionType>(value, OdfElementSchemaRegistry.TryParsePresentationTransitionType);
    }

    /// <summary>
    /// 設定具有 schema awareness 的簡報轉場類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的簡報轉場類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPresentationTransitionTypeAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationTransitionType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的簡報轉場樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的簡報轉場樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfPresentationTransitionStyle? GetPresentationTransitionStyleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfPresentationTransitionStyle>(value, OdfElementSchemaRegistry.TryParsePresentationTransitionStyle);
    }

    /// <summary>
    /// 設定具有 schema awareness 的簡報轉場樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的簡報轉場樣式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPresentationTransitionStyleAttributeValue(string localName, string namespaceUri, OdfPresentationTransitionStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatPresentationTransitionStyle(value), prefix, version);
    }

    #endregion
}

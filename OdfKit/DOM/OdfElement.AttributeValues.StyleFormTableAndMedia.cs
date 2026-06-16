using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values - Style, Form, Table & Media

    /// <summary>
    /// 取得具有 schema awareness 的斷行規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的斷行規則；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleLineBreak? GetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleLineBreak(value, out OdfStyleLineBreak lineBreak) ? lineBreak : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的斷行規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的斷行規則。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleLineBreakAttributeValue(string localName, string namespaceUri, OdfStyleLineBreak value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleLineBreak(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的背景重複屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的背景重複；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleRepeat? GetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleRepeat(value, out OdfStyleRepeat repeat) ? repeat : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的背景重複屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的背景重複。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleRepeatAttributeValue(string localName, string namespaceUri, OdfStyleRepeat value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleRepeat(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleDirection? GetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleDirection(value, out OdfStyleDirection direction) ? direction : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleDirectionAttributeValue(string localName, string namespaceUri, OdfStyleDirection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleDirection(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表單方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表單方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFormOrientation? GetFormOrientationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFormOrientation(value, out OdfFormOrientation orientation) ? orientation : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表單方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表單方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFormOrientationAttributeValue(string localName, string namespaceUri, OdfFormOrientation value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFormOrientation(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableDirection? GetTableDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTableDirection(value, out OdfTableDirection direction) ? direction : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableDirectionAttributeValue(string localName, string namespaceUri, OdfTableDirection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTableDirection(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格方位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格方位；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableOrientation? GetTableOrientationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTableOrientation(value, out OdfTableOrientation orientation) ? orientation : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格方位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格方位。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableOrientationAttributeValue(string localName, string namespaceUri, OdfTableOrientation value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTableOrientation(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式家族；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleFamily? GetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseStyleFamily(value, out OdfStyleFamily family) ? family : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式家族。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleFamilyAttributeValue(string localName, string namespaceUri, OdfStyleFamily value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatStyleFamily(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 ODF 版本屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 ODF 版本；若屬性不存在或不是已知版本則為 <see langword="null"/>。</returns>
    public OdfVersion? GetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfVersionInfo.TryParseVersionString(value, out OdfVersion parsed) ? parsed : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 ODF 版本屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 ODF 版本。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetOdfVersionAttributeValue(string localName, string namespaceUri, OdfVersion value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfVersionInfo.ToVersionString(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 MIME 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 MIME 類型；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfMediaType? GetMediaTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfMediaType.TryParse(value, out OdfMediaType mediaType) ? mediaType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 MIME 類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 MIME 類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetMediaTypeAttributeValue(string localName, string namespaceUri, OdfMediaType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }



    #endregion
}

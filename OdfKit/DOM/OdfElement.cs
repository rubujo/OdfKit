using System;
using System.Globalization;
using System.IO;
using OdfKit.Core;
using OdfKit.Compliance;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// 所有專門類型 ODF 元素包裝器的基底類別。
/// </summary>
/// <param name="localName">元素局部名稱</param>
/// <param name="namespaceUri">元素命名空間 URI</param>
/// <param name="prefix">選用的命名空間前綴</param>
public class OdfElement(string localName, string namespaceUri, string? prefix = null) 
    : OdfNode(OdfNodeType.Element, localName, namespaceUri, prefix)
{
    /// <summary>
    /// 取得具有版本內容且結構定義說明的屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>屬性值；如果找不到，則為 <see langword="null"/></returns>
    public string? GetAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        var attrDef = OdfSchemaRegistry.GetSchema(version).FindAttribute(namespaceUri, localName);
        if (attrDef is null)
        {
            OdfKitDiagnostics.Warn($"Attribute '{localName}' in namespace '{namespaceUri}' is not defined in ODF {version} schema.");
        }
        return GetAttribute(localName, namespaceUri);
    }

    /// <summary>
    /// 設定具有版本內容且結構定義說明的屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">屬性值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetAttributeValue(string localName, string namespaceUri, string value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        var attrDef = OdfSchemaRegistry.GetSchema(version).FindAttribute(namespaceUri, localName);
        if (attrDef is null)
        {
            OdfKitDiagnostics.Warn($"Attribute '{localName}' in namespace '{namespaceUri}' is not defined in ODF {version} schema.");
        }
        SetAttribute(localName, namespaceUri, value, prefix);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="defaultValue">屬性不存在或格式無效時的預設值。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的整數值。</returns>
    public int GetInt32AttributeValue(string localName, string namespaceUri, int defaultValue = 0, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    /// <summary>
    /// 取得具有 schema awareness 的可空 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的整數值；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public int? GetNullableInt32AttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 32 位元整數屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的整數值。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetInt32AttributeValue(string localName, string namespaceUri, int value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.ToString(CultureInfo.InvariantCulture), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的布林屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的布林值；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public bool? GetBooleanAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "0", StringComparison.Ordinal))
        {
            return false;
        }

        return null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的布林屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的布林值。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetBooleanAttributeValue(string localName, string namespaceUri, bool value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value ? "true" : "false", prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的十進位數值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的十進位數值；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public decimal? GetDecimalAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的十進位數值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的十進位數值。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDecimalAttributeValue(string localName, string namespaceUri, decimal value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.ToString(CultureInfo.InvariantCulture), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的日期時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的日期時間；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public DateTime? GetDateTimeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string text = value!;
        string format = text.EndsWith("Z", StringComparison.Ordinal) ? "yyyy-MM-ddTHH:mm:ssZ" : "yyyy-MM-ddTHH:mm:ss";
        DateTimeStyles styles = text.EndsWith("Z", StringComparison.Ordinal)
            ? DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
            : DateTimeStyles.None;
        return DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, styles, out DateTime parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的日期時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的日期時間。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDateTimeAttributeValue(string localName, string namespaceUri, DateTime value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        string formatted = value.Kind == DateTimeKind.Utc
            ? value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        SetAttributeValue(localName, namespaceUri, formatted, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的時間；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfTime? GetTimeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfTime.TryParse(value, out OdfTime parsed) ? parsed : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的時間。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTimeAttributeValue(string localName, string namespaceUri, OdfTime value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.ToString(), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的長度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的長度；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfLength? GetLengthAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfLength.TryParse(value, out OdfLength parsed) ? (OdfLength?)parsed : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的長度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的長度。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLengthAttributeValue(string localName, string namespaceUri, OdfLength value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.ToString(), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的三段邊框線寬屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的三段邊框線寬；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfBorderWidths? GetBorderWidthsAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfBorderWidths.TryParse(value, out OdfBorderWidths borderWidths) ? borderWidths : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的三段邊框線寬屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的三段邊框線寬。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetBorderWidthsAttributeValue(string localName, string namespaceUri, OdfBorderWidths value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 duration 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 duration；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfDuration? GetDurationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfDuration.TryParse(value, out OdfDuration parsed) ? (OdfDuration?)parsed : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 duration 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 duration。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDurationAttributeValue(string localName, string namespaceUri, OdfDuration value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的角度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的角度；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfAngle? GetAngleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfAngle.TryParse(value, out OdfAngle parsed) ? (OdfAngle?)parsed : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的角度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的角度。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetAngleAttributeValue(string localName, string namespaceUri, OdfAngle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式名稱；若屬性不存在或不是有效 XML <c>NCName</c> 則為 <see langword="null"/>。</returns>
    public OdfStyleName? GetStyleNameAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfStyleName.TryParse(value, out OdfStyleName styleName) ? styleName : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式名稱。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleNameAttributeValue(string localName, string namespaceUri, OdfStyleName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式名稱參照清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式名稱參照清單；若屬性不存在或不是有效清單則為 <see langword="null"/>。</returns>
    public OdfStyleNameList? GetStyleNameListAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfStyleNameList.TryParse(value, out OdfStyleNameList styleNameList) ? styleNameList : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式名稱參照清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式名稱參照清單。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleNameListAttributeValue(string localName, string namespaceUri, OdfStyleNameList value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的色彩屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的色彩；若屬性不存在或不是 <c>#RRGGBB</c> 格式則為 <see langword="null"/>。</returns>
    public OdfColor? GetColorAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfColor.TryParse(value, out OdfColor color) ? color : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的色彩屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的色彩。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetColorAttributeValue(string localName, string namespaceUri, OdfColor value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

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
        return TryParseXLinkType(value, out OdfXLinkType type) ? type : null;
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
        SetAttributeValue(localName, namespaceUri, FormatXLinkType(value), prefix, version);
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
        return TryParseXLinkShow(value, out OdfXLinkShow show) ? show : null;
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
        SetAttributeValue(localName, namespaceUri, FormatXLinkShow(value), prefix, version);
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
        return TryParseXLinkActuate(value, out OdfXLinkActuate actuate) ? actuate : null;
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
        SetAttributeValue(localName, namespaceUri, FormatXLinkActuate(value), prefix, version);
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
        return TryParseNumberStyle(value, out OdfNumberStyle style) ? style : null;
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
        SetAttributeValue(localName, namespaceUri, FormatNumberStyle(value), prefix, version);
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
        return TryParseTableOrder(value, out OdfTableOrder order) ? order : null;
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
        SetAttributeValue(localName, namespaceUri, FormatTableOrder(value), prefix, version);
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
        return TryParseTableType(value, out OdfTableType type) ? type : null;
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
        SetAttributeValue(localName, namespaceUri, FormatTableType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 0 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的百分比；若屬性不存在或超出 0 到 100 範圍則為 <see langword="null"/>。</returns>
    public OdfPercent? GetPercentAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPercent.TryParse(value, out OdfPercent percent) ? percent : null;
    }

    /// <summary>
    /// 取得具有 schema awareness 的 -100 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的百分比；若屬性不存在或超出 -100 到 100 範圍則為 <see langword="null"/>。</returns>
    public OdfPercent? GetSignedPercentAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPercent.TryParse(value, allowNegative: true, out OdfPercent percent) ? percent : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 0 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的百分比。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <exception cref="ArgumentOutOfRangeException">當百分比值為負數時擲回。</exception>
    public void SetPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        if (value.Percent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value.Percent, "百分比值不可為負數。");
        }

        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 設定具有 schema awareness 的 -100 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的百分比。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetSignedPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的儲存格位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的儲存格位址；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCellAddressReference? GetCellAddressAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCellAddressReference.TryParse(value, out OdfCellAddressReference cellAddress) ? cellAddress : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的儲存格位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的儲存格位址。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCellAddressAttributeValue(string localName, string namespaceUri, OdfCellAddressReference value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的儲存格範圍位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的儲存格範圍位址；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCellRangeAddress? GetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCellRangeAddress.TryParse(value, out OdfCellRangeAddress cellRangeAddress) ? cellRangeAddress : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的儲存格範圍位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的儲存格範圍位址。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfCellRangeAddress value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的儲存格範圍位址清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的儲存格範圍位址清單；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCellRangeAddressList? GetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCellRangeAddressList.TryParse(value, out OdfCellRangeAddressList cellRangeAddressList) ? cellRangeAddressList : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的儲存格範圍位址清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的儲存格範圍位址清單。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfCellRangeAddressList value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的三維向量屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的三維向量；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfVector3D? GetVector3DAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfVector3D.TryParse(value, out OdfVector3D vector) ? vector : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的三維向量屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的三維向量。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetVector3DAttributeValue(string localName, string namespaceUri, OdfVector3D value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的三維點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的三維點；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfPoint3D? GetPoint3DAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPoint3D.TryParse(value, out OdfPoint3D point) ? point : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的三維點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的三維點。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPoint3DAttributeValue(string localName, string namespaceUri, OdfPoint3D value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的二維座標清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的二維座標清單；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfPointList? GetPointListAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfPointList.TryParse(value, out OdfPointList pointList) ? pointList : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的二維座標清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的二維座標清單。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPointListAttributeValue(string localName, string namespaceUri, OdfPointList value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 XML 名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 XML 名稱；若屬性不存在或不是有效 XML <c>NCName</c> 則為 <see langword="null"/>。</returns>
    public OdfXmlName? GetXmlNameAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfXmlName.TryParse(value, out OdfXmlName xmlName) ? xmlName : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 XML 名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 XML 名稱。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetXmlNameAttributeValue(string localName, string namespaceUri, OdfXmlName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的語言代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的語言代碼；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfLanguageCode? GetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfLanguageCode.TryParse(value, out OdfLanguageCode languageCode) ? languageCode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的語言代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的語言代碼。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLanguageCodeAttributeValue(string localName, string namespaceUri, OdfLanguageCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的國別代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的國別代碼；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCountryCode? GetCountryCodeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCountryCode.TryParse(value, out OdfCountryCode countryCode) ? countryCode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的國別代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的國別代碼。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCountryCodeAttributeValue(string localName, string namespaceUri, OdfCountryCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字系統代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字系統代碼；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfScriptCode? GetScriptCodeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfScriptCode.TryParse(value, out OdfScriptCode scriptCode) ? scriptCode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字系統代碼屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字系統代碼。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetScriptCodeAttributeValue(string localName, string namespaceUri, OdfScriptCode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的語言標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的語言標記；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfLanguageTag? GetLanguageTagAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfLanguageTag.TryParse(value, out OdfLanguageTag languageTag) ? languageTag : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的語言標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的語言標記。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLanguageTagAttributeValue(string localName, string namespaceUri, OdfLanguageTag value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的命名空間 token 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的命名空間 token；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfNamespacedToken? GetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfNamespacedToken.TryParse(value, out OdfNamespacedToken namespacedToken) ? namespacedToken : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的命名空間 token 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的命名空間 token。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetNamespacedTokenAttributeValue(string localName, string namespaceUri, OdfNamespacedToken value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的單一字元屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的單一字元；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfCharacter? GetCharacterAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfCharacter.TryParse(value, out OdfCharacter character) ? character : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的單一字元屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的單一字元。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetCharacterAttributeValue(string localName, string namespaceUri, OdfCharacter value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字編碼名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字編碼名稱；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfTextEncoding? GetTextEncodingAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfTextEncoding.TryParse(value, out OdfTextEncoding textEncoding) ? textEncoding : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字編碼名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字編碼名稱。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextEncodingAttributeValue(string localName, string namespaceUri, OdfTextEncoding value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的目標框架名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的目標框架名稱；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfTargetFrameName? GetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfTargetFrameName.TryParse(value, out OdfTargetFrameName targetFrameName) ? targetFrameName : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的目標框架名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的目標框架名稱。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTargetFrameNameAttributeValue(string localName, string namespaceUri, OdfTargetFrameName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfLineStyle? GetLineStyleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseLineStyle(value, out OdfLineStyle lineStyle) ? lineStyle : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條樣式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineStyleAttributeValue(string localName, string namespaceUri, OdfLineStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatLineStyle(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfLineType? GetLineTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseLineType(value, out OdfLineType lineType) ? lineType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineTypeAttributeValue(string localName, string namespaceUri, OdfLineType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatLineType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條寬度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條寬度；若屬性不存在或格式無效則為 <see langword="null"/>。</returns>
    public OdfLineWidth? GetLineWidthAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfLineWidth.TryParse(value, out OdfLineWidth lineWidth) ? lineWidth : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條寬度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條寬度。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineWidthAttributeValue(string localName, string namespaceUri, OdfLineWidth value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的線條模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的線條模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfLineMode? GetLineModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseLineMode(value, out OdfLineMode lineMode) ? lineMode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的線條模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的線條模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetLineModeAttributeValue(string localName, string namespaceUri, OdfLineMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatLineMode(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontStyle? GetFontStyleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontStyle(value, out OdfFontStyle fontStyle) ? fontStyle : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型樣式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontStyleAttributeValue(string localName, string namespaceUri, OdfFontStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontStyle(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型變體屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型變體；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontVariant? GetFontVariantAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontVariant(value, out OdfFontVariant fontVariant) ? fontVariant : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型變體屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型變體。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontVariantAttributeValue(string localName, string namespaceUri, OdfFontVariant value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontVariant(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型粗細屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型粗細；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontWeight? GetFontWeightAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontWeight(value, out OdfFontWeight fontWeight) ? fontWeight : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型粗細屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型粗細。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontWeightAttributeValue(string localName, string namespaceUri, OdfFontWeight value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontWeight(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的通用字型家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的通用字型家族；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontFamilyGeneric? GetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontFamilyGeneric(value, out OdfFontFamilyGeneric family) ? family : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的通用字型家族屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的通用字型家族。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontFamilyGenericAttributeValue(string localName, string namespaceUri, OdfFontFamilyGeneric value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontFamilyGeneric(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型間距屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型間距；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontPitch? GetFontPitchAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontPitch(value, out OdfFontPitch pitch) ? pitch : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型間距屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型間距。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontPitchAttributeValue(string localName, string namespaceUri, OdfFontPitch value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontPitch(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型浮雕屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型浮雕；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontRelief? GetFontReliefAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontRelief(value, out OdfFontRelief relief) ? relief : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型浮雕屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型浮雕。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontReliefAttributeValue(string localName, string namespaceUri, OdfFontRelief value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontRelief(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的字型伸縮屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的字型伸縮；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFontStretch? GetFontStretchAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseFontStretch(value, out OdfFontStretch stretch) ? stretch : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的字型伸縮屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的字型伸縮。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFontStretchAttributeValue(string localName, string namespaceUri, OdfFontStretch value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatFontStretch(value), prefix, version);
    }

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

    private static bool TryParseLineStyle(string? value, out OdfLineStyle lineStyle)
    {
        switch (value)
        {
            case "none":
                lineStyle = OdfLineStyle.None;
                return true;
            case "solid":
                lineStyle = OdfLineStyle.Solid;
                return true;
            case "dotted":
                lineStyle = OdfLineStyle.Dotted;
                return true;
            case "dash":
                lineStyle = OdfLineStyle.Dash;
                return true;
            case "long-dash":
                lineStyle = OdfLineStyle.LongDash;
                return true;
            case "dot-dash":
                lineStyle = OdfLineStyle.DotDash;
                return true;
            case "dot-dot-dash":
                lineStyle = OdfLineStyle.DotDotDash;
                return true;
            case "wave":
                lineStyle = OdfLineStyle.Wave;
                return true;
            default:
                lineStyle = default;
                return false;
        }
    }

    private static string FormatLineStyle(OdfLineStyle lineStyle)
    {
        return lineStyle switch
        {
            OdfLineStyle.None => "none",
            OdfLineStyle.Solid => "solid",
            OdfLineStyle.Dotted => "dotted",
            OdfLineStyle.Dash => "dash",
            OdfLineStyle.LongDash => "long-dash",
            OdfLineStyle.DotDash => "dot-dash",
            OdfLineStyle.DotDotDash => "dot-dot-dash",
            OdfLineStyle.Wave => "wave",
            _ => throw new ArgumentOutOfRangeException(nameof(lineStyle), lineStyle, "未知的 ODF 線條樣式。")
        };
    }

    private static bool TryParseLineType(string? value, out OdfLineType lineType)
    {
        switch (value)
        {
            case "none":
                lineType = OdfLineType.None;
                return true;
            case "single":
                lineType = OdfLineType.Single;
                return true;
            case "double":
                lineType = OdfLineType.Double;
                return true;
            default:
                lineType = default;
                return false;
        }
    }

    private static string FormatLineType(OdfLineType lineType)
    {
        return lineType switch
        {
            OdfLineType.None => "none",
            OdfLineType.Single => "single",
            OdfLineType.Double => "double",
            _ => throw new ArgumentOutOfRangeException(nameof(lineType), lineType, "未知的 ODF 線條類型。")
        };
    }

    private static bool TryParseLineMode(string? value, out OdfLineMode lineMode)
    {
        switch (value)
        {
            case "continuous":
                lineMode = OdfLineMode.Continuous;
                return true;
            case "skip-white-space":
                lineMode = OdfLineMode.SkipWhiteSpace;
                return true;
            default:
                lineMode = default;
                return false;
        }
    }

    private static string FormatLineMode(OdfLineMode lineMode)
    {
        return lineMode switch
        {
            OdfLineMode.Continuous => "continuous",
            OdfLineMode.SkipWhiteSpace => "skip-white-space",
            _ => throw new ArgumentOutOfRangeException(nameof(lineMode), lineMode, "未知的 ODF 線條模式。")
        };
    }

    private static bool TryParseFontStyle(string? value, out OdfFontStyle fontStyle)
    {
        switch (value)
        {
            case "normal":
                fontStyle = OdfFontStyle.Normal;
                return true;
            case "italic":
                fontStyle = OdfFontStyle.Italic;
                return true;
            case "oblique":
                fontStyle = OdfFontStyle.Oblique;
                return true;
            default:
                fontStyle = default;
                return false;
        }
    }

    private static string FormatFontStyle(OdfFontStyle fontStyle)
    {
        return fontStyle switch
        {
            OdfFontStyle.Normal => "normal",
            OdfFontStyle.Italic => "italic",
            OdfFontStyle.Oblique => "oblique",
            _ => throw new ArgumentOutOfRangeException(nameof(fontStyle), fontStyle, "未知的 ODF 字型樣式。")
        };
    }

    private static bool TryParseFontVariant(string? value, out OdfFontVariant fontVariant)
    {
        switch (value)
        {
            case "normal":
                fontVariant = OdfFontVariant.Normal;
                return true;
            case "small-caps":
                fontVariant = OdfFontVariant.SmallCaps;
                return true;
            default:
                fontVariant = default;
                return false;
        }
    }

    private static string FormatFontVariant(OdfFontVariant fontVariant)
    {
        return fontVariant switch
        {
            OdfFontVariant.Normal => "normal",
            OdfFontVariant.SmallCaps => "small-caps",
            _ => throw new ArgumentOutOfRangeException(nameof(fontVariant), fontVariant, "未知的 ODF 字型變體。")
        };
    }

    private static bool TryParseFontWeight(string? value, out OdfFontWeight fontWeight)
    {
        switch (value)
        {
            case "normal":
                fontWeight = OdfFontWeight.Normal;
                return true;
            case "bold":
                fontWeight = OdfFontWeight.Bold;
                return true;
            case "100":
                fontWeight = OdfFontWeight.Weight100;
                return true;
            case "200":
                fontWeight = OdfFontWeight.Weight200;
                return true;
            case "300":
                fontWeight = OdfFontWeight.Weight300;
                return true;
            case "400":
                fontWeight = OdfFontWeight.Weight400;
                return true;
            case "500":
                fontWeight = OdfFontWeight.Weight500;
                return true;
            case "600":
                fontWeight = OdfFontWeight.Weight600;
                return true;
            case "700":
                fontWeight = OdfFontWeight.Weight700;
                return true;
            case "800":
                fontWeight = OdfFontWeight.Weight800;
                return true;
            case "900":
                fontWeight = OdfFontWeight.Weight900;
                return true;
            default:
                fontWeight = default;
                return false;
        }
    }

    private static string FormatFontWeight(OdfFontWeight fontWeight)
    {
        return fontWeight switch
        {
            OdfFontWeight.Normal => "normal",
            OdfFontWeight.Bold => "bold",
            OdfFontWeight.Weight100 => "100",
            OdfFontWeight.Weight200 => "200",
            OdfFontWeight.Weight300 => "300",
            OdfFontWeight.Weight400 => "400",
            OdfFontWeight.Weight500 => "500",
            OdfFontWeight.Weight600 => "600",
            OdfFontWeight.Weight700 => "700",
            OdfFontWeight.Weight800 => "800",
            OdfFontWeight.Weight900 => "900",
            _ => throw new ArgumentOutOfRangeException(nameof(fontWeight), fontWeight, "未知的 ODF 字型粗細。")
        };
    }

    private static bool TryParseFontFamilyGeneric(string? value, out OdfFontFamilyGeneric family)
    {
        switch (value)
        {
            case "roman":
                family = OdfFontFamilyGeneric.Roman;
                return true;
            case "swiss":
                family = OdfFontFamilyGeneric.Swiss;
                return true;
            case "modern":
                family = OdfFontFamilyGeneric.Modern;
                return true;
            case "decorative":
                family = OdfFontFamilyGeneric.Decorative;
                return true;
            case "script":
                family = OdfFontFamilyGeneric.Script;
                return true;
            case "system":
                family = OdfFontFamilyGeneric.System;
                return true;
            default:
                family = default;
                return false;
        }
    }

    private static string FormatFontFamilyGeneric(OdfFontFamilyGeneric family)
    {
        return family switch
        {
            OdfFontFamilyGeneric.Roman => "roman",
            OdfFontFamilyGeneric.Swiss => "swiss",
            OdfFontFamilyGeneric.Modern => "modern",
            OdfFontFamilyGeneric.Decorative => "decorative",
            OdfFontFamilyGeneric.Script => "script",
            OdfFontFamilyGeneric.System => "system",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "未知的 ODF 通用字型家族。")
        };
    }

    private static bool TryParseFontPitch(string? value, out OdfFontPitch pitch)
    {
        switch (value)
        {
            case "fixed":
                pitch = OdfFontPitch.Fixed;
                return true;
            case "variable":
                pitch = OdfFontPitch.Variable;
                return true;
            default:
                pitch = default;
                return false;
        }
    }

    private static string FormatFontPitch(OdfFontPitch pitch)
    {
        return pitch switch
        {
            OdfFontPitch.Fixed => "fixed",
            OdfFontPitch.Variable => "variable",
            _ => throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "未知的 ODF 字型間距。")
        };
    }

    private static bool TryParseFontRelief(string? value, out OdfFontRelief relief)
    {
        switch (value)
        {
            case "none":
                relief = OdfFontRelief.None;
                return true;
            case "embossed":
                relief = OdfFontRelief.Embossed;
                return true;
            case "engraved":
                relief = OdfFontRelief.Engraved;
                return true;
            default:
                relief = default;
                return false;
        }
    }

    private static string FormatFontRelief(OdfFontRelief relief)
    {
        return relief switch
        {
            OdfFontRelief.None => "none",
            OdfFontRelief.Embossed => "embossed",
            OdfFontRelief.Engraved => "engraved",
            _ => throw new ArgumentOutOfRangeException(nameof(relief), relief, "未知的 ODF 字型浮雕。")
        };
    }

    private static bool TryParseFontStretch(string? value, out OdfFontStretch stretch)
    {
        switch (value)
        {
            case "normal":
                stretch = OdfFontStretch.Normal;
                return true;
            case "ultra-condensed":
                stretch = OdfFontStretch.UltraCondensed;
                return true;
            case "extra-condensed":
                stretch = OdfFontStretch.ExtraCondensed;
                return true;
            case "condensed":
                stretch = OdfFontStretch.Condensed;
                return true;
            case "semi-condensed":
                stretch = OdfFontStretch.SemiCondensed;
                return true;
            case "semi-expanded":
                stretch = OdfFontStretch.SemiExpanded;
                return true;
            case "expanded":
                stretch = OdfFontStretch.Expanded;
                return true;
            case "extra-expanded":
                stretch = OdfFontStretch.ExtraExpanded;
                return true;
            case "ultra-expanded":
                stretch = OdfFontStretch.UltraExpanded;
                return true;
            default:
                stretch = default;
                return false;
        }
    }

    private static string FormatFontStretch(OdfFontStretch stretch)
    {
        return stretch switch
        {
            OdfFontStretch.Normal => "normal",
            OdfFontStretch.UltraCondensed => "ultra-condensed",
            OdfFontStretch.ExtraCondensed => "extra-condensed",
            OdfFontStretch.Condensed => "condensed",
            OdfFontStretch.SemiCondensed => "semi-condensed",
            OdfFontStretch.SemiExpanded => "semi-expanded",
            OdfFontStretch.Expanded => "expanded",
            OdfFontStretch.ExtraExpanded => "extra-expanded",
            OdfFontStretch.UltraExpanded => "ultra-expanded",
            _ => throw new ArgumentOutOfRangeException(nameof(stretch), stretch, "未知的 ODF 字型伸縮。")
        };
    }

    private static bool TryParseStyleLineBreak(string? value, out OdfStyleLineBreak lineBreak)
    {
        switch (value)
        {
            case "normal":
                lineBreak = OdfStyleLineBreak.Normal;
                return true;
            case "strict":
                lineBreak = OdfStyleLineBreak.Strict;
                return true;
            default:
                lineBreak = default;
                return false;
        }
    }

    private static string FormatStyleLineBreak(OdfStyleLineBreak lineBreak)
    {
        return lineBreak switch
        {
            OdfStyleLineBreak.Normal => "normal",
            OdfStyleLineBreak.Strict => "strict",
            _ => throw new ArgumentOutOfRangeException(nameof(lineBreak), lineBreak, "未知的 ODF 斷行規則。")
        };
    }

    private static bool TryParseStyleRepeat(string? value, out OdfStyleRepeat repeat)
    {
        switch (value)
        {
            case "no-repeat":
                repeat = OdfStyleRepeat.NoRepeat;
                return true;
            case "repeat":
                repeat = OdfStyleRepeat.Repeat;
                return true;
            case "stretch":
                repeat = OdfStyleRepeat.Stretch;
                return true;
            default:
                repeat = default;
                return false;
        }
    }

    private static string FormatStyleRepeat(OdfStyleRepeat repeat)
    {
        return repeat switch
        {
            OdfStyleRepeat.NoRepeat => "no-repeat",
            OdfStyleRepeat.Repeat => "repeat",
            OdfStyleRepeat.Stretch => "stretch",
            _ => throw new ArgumentOutOfRangeException(nameof(repeat), repeat, "未知的 ODF 背景重複。")
        };
    }

    private static bool TryParseXLinkType(string? value, out OdfXLinkType type)
    {
        switch (value)
        {
            case "simple":
                type = OdfXLinkType.Simple;
                return true;
            default:
                type = default;
                return false;
        }
    }

    private static string FormatXLinkType(OdfXLinkType type)
    {
        return type switch
        {
            OdfXLinkType.Simple => "simple",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "未知的 ODF XLink 類型。")
        };
    }

    private static bool TryParseXLinkShow(string? value, out OdfXLinkShow show)
    {
        switch (value)
        {
            case "embed":
                show = OdfXLinkShow.Embed;
                return true;
            case "new":
                show = OdfXLinkShow.New;
                return true;
            case "none":
                show = OdfXLinkShow.None;
                return true;
            case "replace":
                show = OdfXLinkShow.Replace;
                return true;
            default:
                show = default;
                return false;
        }
    }

    private static string FormatXLinkShow(OdfXLinkShow show)
    {
        return show switch
        {
            OdfXLinkShow.Embed => "embed",
            OdfXLinkShow.New => "new",
            OdfXLinkShow.None => "none",
            OdfXLinkShow.Replace => "replace",
            _ => throw new ArgumentOutOfRangeException(nameof(show), show, "未知的 ODF XLink 顯示行為。")
        };
    }

    private static bool TryParseXLinkActuate(string? value, out OdfXLinkActuate actuate)
    {
        switch (value)
        {
            case "onLoad":
                actuate = OdfXLinkActuate.OnLoad;
                return true;
            case "onRequest":
                actuate = OdfXLinkActuate.OnRequest;
                return true;
            default:
                actuate = default;
                return false;
        }
    }

    private static string FormatXLinkActuate(OdfXLinkActuate actuate)
    {
        return actuate switch
        {
            OdfXLinkActuate.OnLoad => "onLoad",
            OdfXLinkActuate.OnRequest => "onRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(actuate), actuate, "未知的 ODF XLink 觸發行為。")
        };
    }

    private static bool TryParseNumberStyle(string? value, out OdfNumberStyle style)
    {
        switch (value)
        {
            case "short":
                style = OdfNumberStyle.Short;
                return true;
            case "long":
                style = OdfNumberStyle.Long;
                return true;
            default:
                style = default;
                return false;
        }
    }

    private static string FormatNumberStyle(OdfNumberStyle style)
    {
        return style switch
        {
            OdfNumberStyle.Short => "short",
            OdfNumberStyle.Long => "long",
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "未知的 ODF 數字樣式長短。")
        };
    }

    private static bool TryParseTableOrder(string? value, out OdfTableOrder order)
    {
        switch (value)
        {
            case "ascending":
                order = OdfTableOrder.Ascending;
                return true;
            case "descending":
                order = OdfTableOrder.Descending;
                return true;
            default:
                order = default;
                return false;
        }
    }

    private static string FormatTableOrder(OdfTableOrder order)
    {
        return order switch
        {
            OdfTableOrder.Ascending => "ascending",
            OdfTableOrder.Descending => "descending",
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, "未知的 ODF 表格排序方向。")
        };
    }

    private static bool TryParseTableType(string? value, out OdfTableType type)
    {
        switch (value)
        {
            case "column":
                type = OdfTableType.Column;
                return true;
            case "row":
                type = OdfTableType.Row;
                return true;
            case "table":
                type = OdfTableType.Table;
                return true;
            case "column-percentage":
                type = OdfTableType.ColumnPercentage;
                return true;
            case "index":
                type = OdfTableType.Index;
                return true;
            case "member-difference":
                type = OdfTableType.MemberDifference;
                return true;
            case "member-percentage":
                type = OdfTableType.MemberPercentage;
                return true;
            case "member-percentage-difference":
                type = OdfTableType.MemberPercentageDifference;
                return true;
            case "none":
                type = OdfTableType.None;
                return true;
            case "row-percentage":
                type = OdfTableType.RowPercentage;
                return true;
            case "running-total":
                type = OdfTableType.RunningTotal;
                return true;
            case "total-percentage":
                type = OdfTableType.TotalPercentage;
                return true;
            default:
                type = default;
                return false;
        }
    }

    private static string FormatTableType(OdfTableType type)
    {
        return type switch
        {
            OdfTableType.Column => "column",
            OdfTableType.Row => "row",
            OdfTableType.Table => "table",
            OdfTableType.ColumnPercentage => "column-percentage",
            OdfTableType.Index => "index",
            OdfTableType.MemberDifference => "member-difference",
            OdfTableType.MemberPercentage => "member-percentage",
            OdfTableType.MemberPercentageDifference => "member-percentage-difference",
            OdfTableType.None => "none",
            OdfTableType.RowPercentage => "row-percentage",
            OdfTableType.RunningTotal => "running-total",
            OdfTableType.TotalPercentage => "total-percentage",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "未知的 ODF 表格類型。")
        };
    }

    private static bool TryParseStyleDirection(string? value, out OdfStyleDirection direction)
    {
        switch (value)
        {
            case "ltr":
                direction = OdfStyleDirection.LeftToRight;
                return true;
            case "ttb":
                direction = OdfStyleDirection.TopToBottom;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static string FormatStyleDirection(OdfStyleDirection direction)
    {
        return direction switch
        {
            OdfStyleDirection.LeftToRight => "ltr",
            OdfStyleDirection.TopToBottom => "ttb",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "未知的 ODF 樣式方向。")
        };
    }

    private static bool TryParseFormOrientation(string? value, out OdfFormOrientation orientation)
    {
        switch (value)
        {
            case "horizontal":
                orientation = OdfFormOrientation.Horizontal;
                return true;
            case "vertical":
                orientation = OdfFormOrientation.Vertical;
                return true;
            default:
                orientation = default;
                return false;
        }
    }

    private static string FormatFormOrientation(OdfFormOrientation orientation)
    {
        return orientation switch
        {
            OdfFormOrientation.Horizontal => "horizontal",
            OdfFormOrientation.Vertical => "vertical",
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "未知的 ODF 表單方向。")
        };
    }

    private static bool TryParseTableDirection(string? value, out OdfTableDirection direction)
    {
        switch (value)
        {
            case "from-another-table":
                direction = OdfTableDirection.FromAnotherTable;
                return true;
            case "to-another-table":
                direction = OdfTableDirection.ToAnotherTable;
                return true;
            case "from-same-table":
                direction = OdfTableDirection.FromSameTable;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static string FormatTableDirection(OdfTableDirection direction)
    {
        return direction switch
        {
            OdfTableDirection.FromAnotherTable => "from-another-table",
            OdfTableDirection.ToAnotherTable => "to-another-table",
            OdfTableDirection.FromSameTable => "from-same-table",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "未知的 ODF 表格方向。")
        };
    }

    private static bool TryParseTableOrientation(string? value, out OdfTableOrientation orientation)
    {
        switch (value)
        {
            case "row":
                orientation = OdfTableOrientation.Row;
                return true;
            case "column":
                orientation = OdfTableOrientation.Column;
                return true;
            case "data":
                orientation = OdfTableOrientation.Data;
                return true;
            case "hidden":
                orientation = OdfTableOrientation.Hidden;
                return true;
            case "page":
                orientation = OdfTableOrientation.Page;
                return true;
            default:
                orientation = default;
                return false;
        }
    }

    private static string FormatTableOrientation(OdfTableOrientation orientation)
    {
        return orientation switch
        {
            OdfTableOrientation.Row => "row",
            OdfTableOrientation.Column => "column",
            OdfTableOrientation.Data => "data",
            OdfTableOrientation.Hidden => "hidden",
            OdfTableOrientation.Page => "page",
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "未知的 ODF 表格方位。")
        };
    }

    private static bool TryParseStyleFamily(string? value, out OdfStyleFamily family)
    {
        switch (value)
        {
            case "text":
                family = OdfStyleFamily.Text;
                return true;
            case "paragraph":
                family = OdfStyleFamily.Paragraph;
                return true;
            case "section":
                family = OdfStyleFamily.Section;
                return true;
            case "ruby":
                family = OdfStyleFamily.Ruby;
                return true;
            case "table":
                family = OdfStyleFamily.Table;
                return true;
            case "table-column":
                family = OdfStyleFamily.TableColumn;
                return true;
            case "table-row":
                family = OdfStyleFamily.TableRow;
                return true;
            case "table-cell":
                family = OdfStyleFamily.TableCell;
                return true;
            case "graphic":
                family = OdfStyleFamily.Graphic;
                return true;
            case "presentation":
                family = OdfStyleFamily.Presentation;
                return true;
            case "drawing-page":
                family = OdfStyleFamily.DrawingPage;
                return true;
            case "chart":
                family = OdfStyleFamily.Chart;
                return true;
            default:
                family = default;
                return false;
        }
    }

    private static string FormatStyleFamily(OdfStyleFamily family)
    {
        return family switch
        {
            OdfStyleFamily.Text => "text",
            OdfStyleFamily.Paragraph => "paragraph",
            OdfStyleFamily.Section => "section",
            OdfStyleFamily.Ruby => "ruby",
            OdfStyleFamily.Table => "table",
            OdfStyleFamily.TableColumn => "table-column",
            OdfStyleFamily.TableRow => "table-row",
            OdfStyleFamily.TableCell => "table-cell",
            OdfStyleFamily.Graphic => "graphic",
            OdfStyleFamily.Presentation => "presentation",
            OdfStyleFamily.DrawingPage => "drawing-page",
            OdfStyleFamily.Chart => "chart",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "未知的 ODF 樣式家族。")
        };
    }

    /// <summary>
    /// 複製目前元素，傳回新的類型元素執行個體。
    /// </summary>
    /// <param name="deep">是否進行深層複製 (遞迴複製子節點)</param>
    /// <returns>複製的新元素</returns>
    public override OdfNode CloneNode(bool deep)
    {
        var clone = OdfNodeFactory.CreateElement(LocalName, NamespaceUri, Prefix);
        foreach (var attr in Attributes)
        {
            clone.Attributes[attr.Key] = attr.Value;
        }
        if (deep)
        {
            foreach (var child in Children)
            {
                clone.AppendChild(child.CloneNode(true));
            }
        }
        return clone;
    }
}

#region Text Wrappers

/// <summary>
/// 表示 ODF 中的 text:p 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextPElement(string? prefix = null) : OdfElement("p", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此段落的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:h 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextHElement(string? prefix = null) : OdfElement("h", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此標題的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此標題的大綱層級。
    /// </summary>
    public int OutlineLevel
    {
        get => GetInt32AttributeValue("outline-level", OdfNamespaces.Text, 1, GetDocumentVersion());
        set => SetInt32AttributeValue("outline-level", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
    }
}

/// <summary>
/// 表示 ODF 中的 text:span 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextSpanElement(string? prefix = null) : OdfElement("span", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此文字區段的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:list 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextListElement(string? prefix = null) : OdfElement("list", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此清單的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:list-item 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextListItemElement(string? prefix = null) : OdfElement("list-item", OdfNamespaces.Text, prefix);

/// <summary>
/// 表示 ODF 中的 text:section 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextSectionElement(string? prefix = null) : OdfElement("section", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此區段的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Text);
            else
                SetAttributeValue("name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此區段的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:bookmark 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextBookmarkElement(string? prefix = null) : OdfElement("bookmark", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此書籤的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Text);
            else
                SetAttributeValue("name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:note 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextNoteElement(string? prefix = null) : OdfElement("note", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此註腳或章節附註的類別。
    /// </summary>
    public string? NoteClass
    {
        get => GetAttributeValue("note-class", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("note-class", OdfNamespaces.Text);
            else
                SetAttributeValue("note-class", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 office:annotation 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeAnnotationElement(string? prefix = null) : OdfElement("annotation", OdfNamespaces.Office, prefix)
{
    /// <summary>
    /// 取得或設定此註解的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Office, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Office);
            else
                SetAttributeValue("name", OdfNamespaces.Office, value, OdfNamespaces.GetPrefix(OdfNamespaces.Office), GetDocumentVersion());
        }
    }
}

#endregion

#region Table Wrappers

/// <summary>
/// 表示 ODF 中的 table:table 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableTableElement(string? prefix = null) : OdfElement("table", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此表格的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Table);
            else
                SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此表格的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Table);
            else
                SetAttributeValue("style-name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 table:table-row 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableTableRowElement(string? prefix = null) : OdfElement("table-row", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此表格列的重複次數。
    /// </summary>
    public int NumberRowsRepeated
    {
        get => GetInt32AttributeValue("number-rows-repeated", OdfNamespaces.Table, 1, GetDocumentVersion());
        set => SetInt32AttributeValue("number-rows-repeated", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
    }
}

/// <summary>
/// 表示 ODF 中的 table:table-cell 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableTableCellElement(string? prefix = null) : OdfElement("table-cell", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此表格儲存格的重複次數。
    /// </summary>
    public int NumberColumnsRepeated
    {
        get => GetInt32AttributeValue("number-columns-repeated", OdfNamespaces.Table, 1, GetDocumentVersion());
        set => SetInt32AttributeValue("number-columns-repeated", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
    }

    /// <summary>
    /// 取得或設定此表格儲存格的值類型。
    /// </summary>
    public string? ValueType
    {
        get => GetAttributeValue("value-type", OdfNamespaces.Office, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("value-type", OdfNamespaces.Office);
            else
                SetAttributeValue("value-type", OdfNamespaces.Office, value, OdfNamespaces.GetPrefix(OdfNamespaces.Office), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 table:covered-table-cell 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableCoveredTableCellElement(string? prefix = null) : OdfElement("covered-table-cell", OdfNamespaces.Table, prefix);

/// <summary>
/// 表示 ODF 中的 table:named-range 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableNamedRangeElement(string? prefix = null) : OdfElement("named-range", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此具名範圍的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Table);
            else
                SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 table:database-range 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableDatabaseRangeElement(string? prefix = null) : OdfElement("database-range", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此資料庫範圍的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Table);
            else
                SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }
}

#endregion

#region Draw Wrappers

/// <summary>
/// 表示 ODF 中的 draw:frame 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawFrameElement(string? prefix = null) : OdfElement("frame", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定此框架的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Draw);
            else
                SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:image 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawImageElement(string? prefix = null) : OdfElement("image", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定影像的超連結 URL。
    /// </summary>
    public string? Href
    {
        get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("href", OdfNamespaces.XLink);
            else
                SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:object 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawObjectElement(string? prefix = null) : OdfElement("object", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定內嵌物件的超連結 URL。
    /// </summary>
    public string? Href
    {
        get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("href", OdfNamespaces.XLink);
            else
                SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的繪圖形狀元素。
/// </summary>
/// <param name="shapeKind">形狀種類</param>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawShapeElement(string shapeKind, string? prefix = null) : OdfElement(shapeKind, OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定此形狀的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Draw);
            else
                SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:g 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawGroupElement(string? prefix = null) : OdfElement("g", OdfNamespaces.Draw, prefix);

/// <summary>
/// 表示 ODF 中的 draw:connector 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawConnectorElement(string? prefix = null) : OdfElement("connector", OdfNamespaces.Draw, prefix);

#endregion

#region Style Wrappers

/// <summary>
/// 表示 ODF 中的 style:style 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleStyleElement(string? prefix = null) : OdfElement("style", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此樣式的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Style);
            else
                SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此樣式的家族類型。
    /// </summary>
    public string? Family
    {
        get => GetAttributeValue("family", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("family", OdfNamespaces.Style);
            else
                SetAttributeValue("family", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:default-style 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleDefaultStyleElement(string? prefix = null) : OdfElement("default-style", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此預設樣式的家族類型。
    /// </summary>
    public string? Family
    {
        get => GetAttributeValue("family", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("family", OdfNamespaces.Style);
            else
                SetAttributeValue("family", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:master-page 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleMasterPageElement(string? prefix = null) : OdfElement("master-page", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此母片頁面的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Style);
            else
                SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:page-layout 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StylePageLayoutElement(string? prefix = null) : OdfElement("page-layout", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此頁面版面配置的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Style);
            else
                SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:text-properties 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleTextPropertiesElement(string? prefix = null) : OdfElement("text-properties", OdfNamespaces.Style, prefix);

/// <summary>
/// 表示 ODF 中的 style:paragraph-properties 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleParagraphPropertiesElement(string? prefix = null) : OdfElement("paragraph-properties", OdfNamespaces.Style, prefix);

#endregion

#region Office Wrappers

/// <summary>
/// 表示 ODF 中的 office:document 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeDocumentElement(string? prefix = null) : OdfElement("document", OdfNamespaces.Office, prefix);

/// <summary>
/// 表示 ODF 中的 office:document-content 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeDocumentContentElement(string? prefix = null) : OdfElement("document-content", OdfNamespaces.Office, prefix);

/// <summary>
/// 表示 ODF 中的 office:body 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeBodyElement(string? prefix = null) : OdfElement("body", OdfNamespaces.Office, prefix);

/// <summary>
/// 表示 ODF 中的 office:text 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeTextElement(string? prefix = null) : OdfElement("text", OdfNamespaces.Office, prefix);

/// <summary>
/// 表示 ODF 中的 office:spreadsheet 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeSpreadsheetElement(string? prefix = null) : OdfElement("spreadsheet", OdfNamespaces.Office, prefix);

/// <summary>
/// 表示 ODF 中的 office:presentation 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficePresentationElement(string? prefix = null) : OdfElement("presentation", OdfNamespaces.Office, prefix);

/// <summary>
/// 表示 ODF 中的 office:drawing 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeDrawingElement(string? prefix = null) : OdfElement("drawing", OdfNamespaces.Office, prefix);

#endregion

#region Manifest Wrappers

/// <summary>
/// 表示 ODF 資訊清單中的 manifest:manifest 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class ManifestManifestElement(string? prefix = null) : OdfElement("manifest", OdfNamespaces.Manifest, prefix);

/// <summary>
/// 表示 ODF 資訊清單中的 manifest:file-entry 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class ManifestFileEntryElement(string? prefix = null) : OdfElement("file-entry", OdfNamespaces.Manifest, prefix)
{
    /// <summary>
    /// 取得或設定檔案項目在套件中的完整路徑。
    /// </summary>
    public string? FullPath
    {
        get => GetAttributeValue("full-path", OdfNamespaces.Manifest, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("full-path", OdfNamespaces.Manifest);
            else
                SetAttributeValue("full-path", OdfNamespaces.Manifest, value, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定檔案項目的媒體類型。
    /// </summary>
    public string? MediaType
    {
        get => GetAttributeValue("media-type", OdfNamespaces.Manifest, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("media-type", OdfNamespaces.Manifest);
            else
                SetAttributeValue("media-type", OdfNamespaces.Manifest, value, OdfNamespaces.GetPrefix(OdfNamespaces.Manifest), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 資訊清單中的 manifest:encryption-data 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class ManifestEncryptionDataElement(string? prefix = null) : OdfElement("encryption-data", OdfNamespaces.Manifest, prefix);

#endregion

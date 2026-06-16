using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// 所有專門類型 ODF 元素包裝器的基底類別。
/// </summary>
/// <param name="localName">元素局部名稱</param>
/// <param name="namespaceUri">元素命名空間 URI</param>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OdfElement(string localName, string namespaceUri, string? prefix = null)
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
    /// 列舉此元素的直接子元素，並只傳回指定的 typed DOM 元素型別。
    /// </summary>
    /// <typeparam name="TElement">要篩選的 typed DOM 元素型別。</typeparam>
    /// <returns>符合型別的直接子元素列舉。</returns>
    public IEnumerable<TElement> ChildElements<TElement>()
        where TElement : OdfElement
    {
        foreach (OdfNode child in Children)
        {
            if (child is TElement typedChild)
            {
                yield return typedChild;
            }
        }
    }

    /// <summary>
    /// 列舉此元素的所有後代元素，並只傳回指定的 typed DOM 元素型別。
    /// </summary>
    /// <typeparam name="TElement">要篩選的 typed DOM 元素型別。</typeparam>
    /// <returns>符合型別的後代元素列舉。</returns>
    public IEnumerable<TElement> DescendantElements<TElement>()
        where TElement : OdfElement
    {
        foreach (OdfNode descendant in Descendants())
        {
            if (descendant is TElement typedDescendant)
            {
                yield return typedDescendant;
            }
        }
    }

    /// <summary>
    /// 將 typed DOM 元素加入此元素的子節點清單末尾，並傳回同一個元素以便串接設定。
    /// </summary>
    /// <typeparam name="TElement">要加入的 typed DOM 元素型別。</typeparam>
    /// <param name="child">要加入的 typed DOM 子元素。</param>
    /// <returns>已加入的 typed DOM 子元素。</returns>
    public TElement AppendElement<TElement>(TElement child)
        where TElement : OdfElement
    {
        AppendChild(child);
        return child;
    }

    /// <summary>
    /// 在參考子節點之前插入 typed DOM 元素，並傳回同一個元素以便串接設定。
    /// </summary>
    /// <typeparam name="TElement">要插入的 typed DOM 元素型別。</typeparam>
    /// <param name="newChild">要插入的 typed DOM 子元素。</param>
    /// <param name="refChild">參考子節點，新元素將插入在此節點之前。</param>
    /// <returns>已插入的 typed DOM 子元素。</returns>
    public TElement InsertElementBefore<TElement>(TElement newChild, OdfNode refChild)
        where TElement : OdfElement
    {
        InsertBefore(newChild, refChild);
        return newChild;
    }

    /// <summary>
    /// 在參考子節點之後插入 typed DOM 元素，並傳回同一個元素以便串接設定。
    /// </summary>
    /// <typeparam name="TElement">要插入的 typed DOM 元素型別。</typeparam>
    /// <param name="newChild">要插入的 typed DOM 子元素。</param>
    /// <param name="refChild">參考子節點，新元素將插入在此節點之後。</param>
    /// <returns>已插入的 typed DOM 子元素。</returns>
    public TElement InsertElementAfter<TElement>(TElement newChild, OdfNode refChild)
        where TElement : OdfElement
    {
        InsertAfter(newChild, refChild);
        return newChild;
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
    /// 取得具有 schema awareness 的簡報效果屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的簡報效果；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfPresentationEffect? GetPresentationEffectAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParsePresentationEffect(value, out OdfPresentationEffect effect) ? effect : null;
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
        SetAttributeValue(localName, namespaceUri, FormatPresentationEffect(value), prefix, version);
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
        return TryParsePresentationSpeed(value, out OdfPresentationSpeed speed) ? speed : null;
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
        SetAttributeValue(localName, namespaceUri, FormatPresentationSpeed(value), prefix, version);
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
        return TryParsePresentationAction(value, out OdfPresentationAction action) ? action : null;
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
        SetAttributeValue(localName, namespaceUri, FormatPresentationAction(value), prefix, version);
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
        return TryParsePresentationTransitionType(value, out OdfPresentationTransitionType transitionType) ? transitionType : null;
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
        SetAttributeValue(localName, namespaceUri, FormatPresentationTransitionType(value), prefix, version);
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
        return TryParsePresentationTransitionStyle(value, out OdfPresentationTransitionStyle transitionStyle) ? transitionStyle : null;
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
        SetAttributeValue(localName, namespaceUri, FormatPresentationTransitionStyle(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 FO 文字轉換屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 FO 文字轉換；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFoTextTransform? GetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfFoTextTransform textTransform) ? textTransform : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 FO 文字轉換屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 FO 文字轉換。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFoTextTransformAttributeValue(string localName, string namespaceUri, OdfFoTextTransform value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF FO 文字轉換。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 FO 文字對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 FO 文字對齊；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfFoTextAlign? GetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfFoTextAlign textAlign) ? textAlign : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 FO 文字對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 FO 文字對齊。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetFoTextAlignAttributeValue(string localName, string namespaceUri, OdfFoTextAlign value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF FO 文字對齊。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式文字旋轉縮放屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式文字旋轉縮放；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleTextRotationScale? GetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleTextRotationScale scale) ? scale : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式文字旋轉縮放屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式文字旋轉縮放。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleTextRotationScaleAttributeValue(string localName, string namespaceUri, OdfStyleTextRotationScale value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式文字旋轉縮放。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式文字組合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式文字組合；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleTextCombine? GetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleTextCombine textCombine) ? textCombine : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式文字組合屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式文字組合。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleTextCombineAttributeValue(string localName, string namespaceUri, OdfStyleTextCombine value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式文字組合。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的繪圖填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖填滿；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawFill? GetDrawFillAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDrawFill fill) ? fill : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖填滿。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawFillAttributeValue(string localName, string namespaceUri, OdfDrawFill value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 繪圖填滿。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的 SMIL 動畫填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的 SMIL 動畫填滿；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfSmilFill? GetSmilFillAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfSmilFill fill) ? fill : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的 SMIL 動畫填滿屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的 SMIL 動畫填滿。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetSmilFillAttributeValue(string localName, string namespaceUri, OdfSmilFill value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF SMIL 動畫填滿。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的繪圖填滿圖片參照點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖填滿圖片參照點；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawFillImageRefPoint? GetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDrawFillImageRefPoint refPoint) ? refPoint : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖填滿圖片參照點屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖填滿圖片參照點。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawFillImageRefPointAttributeValue(string localName, string namespaceUri, OdfDrawFillImageRefPoint value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 繪圖填滿圖片參照點。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的繪圖色彩模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的繪圖色彩模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDrawColorMode? GetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDrawColorMode colorMode) ? colorMode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的繪圖色彩模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的繪圖色彩模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDrawColorModeAttributeValue(string localName, string namespaceUri, OdfDrawColorMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 繪圖色彩模式。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式垂直對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式垂直對齊；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleVerticalAlign? GetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleVerticalAlign verticalAlign) ? verticalAlign : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式垂直對齊屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式垂直對齊。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleVerticalAlignAttributeValue(string localName, string namespaceUri, OdfStyleVerticalAlign value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式垂直對齊。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式垂直位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式垂直位置；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleVerticalPos? GetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleVerticalPos pos) ? pos : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式垂直位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式垂直位置。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleVerticalPosAttributeValue(string localName, string namespaceUri, OdfStyleVerticalPos value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式垂直位置。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式垂直相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式垂直相對基準；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleVerticalRel? GetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleVerticalRel rel) ? rel : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式垂直相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式垂直相對基準。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleVerticalRelAttributeValue(string localName, string namespaceUri, OdfStyleVerticalRel value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式垂直相對基準。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式水平位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式水平位置；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleHorizontalPos? GetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleHorizontalPos pos) ? pos : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式水平位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式水平位置。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleHorizontalPosAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalPos value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式水平位置。"), prefix, version);
    }
}

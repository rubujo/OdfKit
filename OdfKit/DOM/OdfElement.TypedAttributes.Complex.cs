using System;
using OdfKit.Compliance;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Typed Attribute Accessors - Complex

    /// <summary>
    /// 取得具有 schema awareness 的日期時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的日期時間；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public DateTime? GetDateTimeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetDateTime(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的日期時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的日期時間</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDateTimeAttributeValue(string localName, string namespaceUri, DateTime value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatDateTime(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的時間；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfTime? GetTimeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetTime(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的時間屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的時間</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTimeAttributeValue(string localName, string namespaceUri, OdfTime value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatTime(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的長度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的長度；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfLength? GetLengthAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetLength(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的長度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的長度</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetLengthAttributeValue(string localName, string namespaceUri, OdfLength value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatLength(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的三段邊框線寬屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的三段邊框線寬；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfBorderWidths? GetBorderWidthsAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetBorderWidths(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的三段邊框線寬屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的三段邊框線寬</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetBorderWidthsAttributeValue(string localName, string namespaceUri, OdfBorderWidths value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatBorderWidths(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的 duration 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的 duration；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfDuration? GetDurationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetDuration(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的 duration 屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的 duration</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDurationAttributeValue(string localName, string namespaceUri, OdfDuration value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatDuration(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的角度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的角度；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfAngle? GetAngleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetAngle(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的角度屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的角度</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetAngleAttributeValue(string localName, string namespaceUri, OdfAngle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatAngle(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的樣式名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式名稱；若屬性不存在或不是有效 XML <c>NCName</c> 則為 <see langword="null"/></returns>
    public OdfStyleName? GetStyleNameAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetStyleName(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的樣式名稱屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式名稱</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleNameAttributeValue(string localName, string namespaceUri, OdfStyleName value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatStyleName(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的樣式名稱參照清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式名稱參照清單；若屬性不存在或不是有效清單則為 <see langword="null"/></returns>
    public OdfStyleNameList? GetStyleNameListAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetStyleNameList(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的樣式名稱參照清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式名稱參照清單</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleNameListAttributeValue(string localName, string namespaceUri, OdfStyleNameList value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatStyleNameList(value), prefix, version);

    /// <summary>
    /// 取得具有 schema awareness 的色彩屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的色彩；若屬性不存在或不是 <c>#RRGGBB</c> 格式則為 <see langword="null"/></returns>
    public OdfColor? GetColorAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
        => OdfElementComplexAttributeAccess.GetColor(GetAttributeValue(localName, namespaceUri, version));

    /// <summary>
    /// 設定具有 schema awareness 的色彩屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的色彩</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetColorAttributeValue(string localName, string namespaceUri, OdfColor value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
        => SetAttributeValue(localName, namespaceUri, OdfElementComplexAttributeAccess.FormatColor(value), prefix, version);

    #endregion
}

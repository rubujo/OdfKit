using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values - Text Caption & Address

    /// <summary>
    /// 取得具有 schema awareness 的文字標號序列格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字標號序列格式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextCaptionSequenceFormat? GetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextCaptionSequenceFormat(value, out OdfTextCaptionSequenceFormat format) ? format : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字標號序列格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字標號序列格式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfTextCaptionSequenceFormat value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextCaptionSequenceFormat(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字編號位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字編號位置；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextNumberPosition? GetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextNumberPosition(value, out OdfTextNumberPosition position) ? position : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字編號位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字編號位置。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfTextNumberPosition value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextNumberPosition(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字預留位置類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字預留位置類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextPlaceholderType? GetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextPlaceholderType(value, out OdfTextPlaceholderType placeholderType) ? placeholderType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字預留位置類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字預留位置類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfTextPlaceholderType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextPlaceholderType(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字動畫屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字動畫；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextAnimation? GetTextAnimationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextAnimation(value, out OdfTextAnimation animation) ? animation : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字動畫屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字動畫。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextAnimationAttributeValue(string localName, string namespaceUri, OdfTextAnimation value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextAnimation(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字動畫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字動畫方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextAnimationDirection? GetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextAnimationDirection(value, out OdfTextAnimationDirection direction) ? direction : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字動畫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字動畫方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfTextAnimationDirection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextAnimationDirection(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的文字索引項目種類屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的文字索引項目種類；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTextKind? GetTextKindAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseTextKind(value, out OdfTextKind kind) ? kind : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的文字索引項目種類屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的文字索引項目種類。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTextKindAttributeValue(string localName, string namespaceUri, OdfTextKind value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatTextKind(value), prefix, version);
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


    #endregion
}

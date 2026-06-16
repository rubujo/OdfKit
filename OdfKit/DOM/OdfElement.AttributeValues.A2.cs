using System;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values (A2)

    /// <summary>
    /// 取得具有 schema awareness 的樣式文字系統類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式文字系統類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleScriptType? GetStyleScriptTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleScriptType scriptType) ? scriptType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式文字系統類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式文字系統類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleScriptTypeAttributeValue(string localName, string namespaceUri, OdfStyleScriptType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式文字系統類型。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式文字強調標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式文字強調標記；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleTextEmphasize? GetStyleTextEmphasizeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleTextEmphasize textEmphasize) ? textEmphasize : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式文字強調標記屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式文字強調標記。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleTextEmphasizeAttributeValue(string localName, string namespaceUri, OdfStyleTextEmphasize value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式文字強調標記。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的數字曆法屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的數字曆法；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfNumberCalendar? GetNumberCalendarAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseNumberCalendar(value, out OdfNumberCalendar calendar) ? calendar : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的數字曆法屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的數字曆法。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetNumberCalendarAttributeValue(string localName, string namespaceUri, OdfNumberCalendar value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatNumberCalendar(value), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格成員類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格成員類型；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableMemberType? GetTableMemberTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfTableMemberType memberType) ? memberType : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格成員類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格成員類型。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableMemberTypeAttributeValue(string localName, string namespaceUri, OdfTableMemberType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 表格成員類型。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格分組單位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格分組單位；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableGroupedBy? GetTableGroupedByAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfTableGroupedBy groupedBy) ? groupedBy : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格分組單位屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格分組單位。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableGroupedByAttributeValue(string localName, string namespaceUri, OdfTableGroupedBy value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 表格分組單位。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格排序模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格排序模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableSortMode? GetTableSortModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfTableSortMode sortMode) ? sortMode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格排序模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格排序模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableSortModeAttributeValue(string localName, string namespaceUri, OdfTableSortMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 表格排序模式。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格條件來源屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格條件來源；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableConditionSource? GetTableConditionSourceAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfTableConditionSource conditionSource) ? conditionSource : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格條件來源屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格條件來源。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableConditionSourceAttributeValue(string localName, string namespaceUri, OdfTableConditionSource value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 表格條件來源。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的動畫色彩插值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的動畫色彩插值；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfAnimationColorInterpolation? GetAnimationColorInterpolationAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfAnimationColorInterpolation interpolation) ? interpolation : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的動畫色彩插值屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的動畫色彩插值。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetAnimationColorInterpolationAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolation value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 動畫色彩插值。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的動畫色彩插值方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的動畫色彩插值方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfAnimationColorInterpolationDirection? GetAnimationColorInterpolationDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfAnimationColorInterpolationDirection direction) ? direction : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的動畫色彩插值方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的動畫色彩插值方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetAnimationColorInterpolationDirectionAttributeValue(string localName, string namespaceUri, OdfAnimationColorInterpolationDirection value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 動畫色彩插值方向。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的資料庫可空性屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的資料庫可空性；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDatabaseIsNullable? GetDatabaseIsNullableAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDatabaseIsNullable isNullable) ? isNullable : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的資料庫可空性屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的資料庫可空性。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDatabaseIsNullableAttributeValue(string localName, string namespaceUri, OdfDatabaseIsNullable value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 資料庫可空性。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的資料庫資料來源設定型別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的資料庫資料來源設定型別；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDatabaseDataSourceSettingType? GetDatabaseDataSourceSettingTypeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDatabaseDataSourceSettingType type) ? type : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的資料庫資料來源設定型別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的資料庫資料來源設定型別。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDatabaseDataSourceSettingTypeAttributeValue(string localName, string namespaceUri, OdfDatabaseDataSourceSettingType value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 資料庫資料來源設定型別。"), prefix, version);
    }


    #endregion
}

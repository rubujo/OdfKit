using System;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values (A)


    /// <summary>
    /// 取得具有 schema awareness 的樣式水平相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式水平相對基準；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleHorizontalRel? GetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleHorizontalRel rel) ? rel : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式水平相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式水平相對基準。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalRel value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式水平相對基準。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式書寫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式書寫方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleWritingMode? GetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleWritingMode writingMode) ? writingMode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式書寫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式書寫方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfStyleWritingMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式書寫方向。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式文繞圖屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式文繞圖；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleWrap? GetStyleWrapAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleWrap wrap) ? wrap : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式文繞圖屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式文繞圖。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleWrapAttributeValue(string localName, string namespaceUri, OdfStyleWrap value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式文繞圖。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式穿越排列屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式穿越排列；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleRunThrough? GetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleRunThrough runThrough) ? runThrough : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式穿越排列屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式穿越排列。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfStyleRunThrough value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式穿越排列。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的樣式輪廓繞排模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的樣式輪廓繞排模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfStyleWrapContourMode? GetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfStyleWrapContourMode mode) ? mode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的樣式輪廓繞排模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的樣式輪廓繞排模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfStyleWrapContourMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 樣式輪廓繞排模式。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格成員顯示方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格成員顯示方向；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableDisplayMemberMode? GetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfTableDisplayMemberMode mode) ? mode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格成員顯示方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格成員顯示方向。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfTableDisplayMemberMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 表格成員顯示方向。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的表格版面配置模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的表格版面配置模式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfTableLayoutMode? GetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfTableLayoutMode mode) ? mode : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的表格版面配置模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的表格版面配置模式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfTableLayoutMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 表格版面配置模式。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的資料庫參照動作規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的資料庫參照動作規則；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfDatabaseRule? GetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfDatabaseRule rule) ? rule : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的資料庫參照動作規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的資料庫參照動作規則。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfDatabaseRule value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 資料庫參照動作規則。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的簡報預設動畫類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的簡報預設動畫類別；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfPresentationPresetClass? GetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfPresentationPresetClass presetClass) ? presetClass : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的簡報預設動畫類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的簡報預設動畫類別。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfPresentationPresetClass value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 簡報預設動畫類別。"), prefix, version);
    }

    /// <summary>
    /// 取得具有 schema awareness 的數字音譯樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="version">ODF 版本內容。</param>
    /// <returns>解析後的數字音譯樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/>。</returns>
    public OdfNumberTransliterationStyle? GetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return TryParseEnumToken(value, out OdfNumberTransliterationStyle transliterationStyle) ? transliterationStyle : null;
    }

    /// <summary>
    /// 設定具有 schema awareness 的數字音譯樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱。</param>
    /// <param name="namespaceUri">屬性命名空間 URI。</param>
    /// <param name="value">要寫入的數字音譯樣式。</param>
    /// <param name="prefix">選用的命名空間前綴。</param>
    /// <param name="version">ODF 版本內容。</param>
    public void SetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfNumberTransliterationStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, FormatEnumToken(value, "未知的 ODF 數字音譯樣式。"), prefix, version);
    }

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

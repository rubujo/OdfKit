using System;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Attribute Values - Style & Presentation

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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleHorizontalRel rel) ? rel : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式水平相對基準。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleWritingMode writingMode) ? writingMode : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式書寫方向。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleWrap wrap) ? wrap : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文繞圖。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleRunThrough runThrough) ? runThrough : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式穿越排列。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfStyleWrapContourMode mode) ? mode : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式輪廓繞排模式。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfTableDisplayMemberMode mode) ? mode : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格成員顯示方向。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfTableLayoutMode mode) ? mode : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格版面配置模式。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfDatabaseRule rule) ? rule : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 資料庫參照動作規則。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfPresentationPresetClass presetClass) ? presetClass : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 簡報預設動畫類別。"), prefix, version);
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
        return OdfElementSchemaRegistry.TryParseEnumToken(value, out OdfNumberTransliterationStyle transliterationStyle) ? transliterationStyle : null;
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
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 數字音譯樣式。"), prefix, version);
    }

    #endregion
}

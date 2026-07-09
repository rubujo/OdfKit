using System;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfElement API.
/// 提供 OdfElement API。
/// </summary>

public partial class OdfElement
{
    #region Attribute Values - Style & Presentation
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfStyleHorizontalRel? GetStyleHorizontalRelAttributeValue(string localName, string namespaceUri) => GetStyleHorizontalRelAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetStyleHorizontalRelAttributeValue operation.
    /// 取得具有 schema awareness 的樣式水平相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式水平相對基準；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleHorizontalRel? GetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleHorizontalRel>(value);
    }


    /// <summary>
    /// Executes the SetStyleHorizontalRelAttributeValue operation.
    /// 設定具有 schema awareness 的樣式水平相對基準屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式水平相對基準</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleHorizontalRelAttributeValue(string localName, string namespaceUri, OdfStyleHorizontalRel value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式水平相對基準。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfStyleWritingMode? GetStyleWritingModeAttributeValue(string localName, string namespaceUri) => GetStyleWritingModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetStyleWritingModeAttributeValue operation.
    /// 取得具有 schema awareness 的樣式書寫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式書寫方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleWritingMode? GetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleWritingMode>(value);
    }


    /// <summary>
    /// Executes the SetStyleWritingModeAttributeValue operation.
    /// 設定具有 schema awareness 的樣式書寫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式書寫方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleWritingModeAttributeValue(string localName, string namespaceUri, OdfStyleWritingMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式書寫方向。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfStyleWrap? GetStyleWrapAttributeValue(string localName, string namespaceUri) => GetStyleWrapAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetStyleWrapAttributeValue operation.
    /// 取得具有 schema awareness 的樣式文繞圖屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式文繞圖；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleWrap? GetStyleWrapAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleWrap>(value);
    }


    /// <summary>
    /// Executes the SetStyleWrapAttributeValue operation.
    /// 設定具有 schema awareness 的樣式文繞圖屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式文繞圖</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleWrapAttributeValue(string localName, string namespaceUri, OdfStyleWrap value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式文繞圖。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfStyleRunThrough? GetStyleRunThroughAttributeValue(string localName, string namespaceUri) => GetStyleRunThroughAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetStyleRunThroughAttributeValue operation.
    /// 取得具有 schema awareness 的樣式穿越排列屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式穿越排列；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleRunThrough? GetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleRunThrough>(value);
    }


    /// <summary>
    /// Executes the SetStyleRunThroughAttributeValue operation.
    /// 設定具有 schema awareness 的樣式穿越排列屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式穿越排列</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleRunThroughAttributeValue(string localName, string namespaceUri, OdfStyleRunThrough value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式穿越排列。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfStyleWrapContourMode? GetStyleWrapContourModeAttributeValue(string localName, string namespaceUri) => GetStyleWrapContourModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetStyleWrapContourModeAttributeValue operation.
    /// 取得具有 schema awareness 的樣式輪廓繞排模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的樣式輪廓繞排模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfStyleWrapContourMode? GetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfStyleWrapContourMode>(value);
    }


    /// <summary>
    /// Executes the SetStyleWrapContourModeAttributeValue operation.
    /// 設定具有 schema awareness 的樣式輪廓繞排模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的樣式輪廓繞排模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetStyleWrapContourModeAttributeValue(string localName, string namespaceUri, OdfStyleWrapContourMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 樣式輪廓繞排模式。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfTableDisplayMemberMode? GetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri) => GetTableDisplayMemberModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetTableDisplayMemberModeAttributeValue operation.
    /// 取得具有 schema awareness 的表格成員顯示方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格成員顯示方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableDisplayMemberMode? GetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableDisplayMemberMode>(value);
    }


    /// <summary>
    /// Executes the SetTableDisplayMemberModeAttributeValue operation.
    /// 設定具有 schema awareness 的表格成員顯示方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格成員顯示方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableDisplayMemberModeAttributeValue(string localName, string namespaceUri, OdfTableDisplayMemberMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格成員顯示方向。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfTableLayoutMode? GetTableLayoutModeAttributeValue(string localName, string namespaceUri) => GetTableLayoutModeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetTableLayoutModeAttributeValue operation.
    /// 取得具有 schema awareness 的表格版面配置模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的表格版面配置模式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTableLayoutMode? GetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfTableLayoutMode>(value);
    }


    /// <summary>
    /// Executes the SetTableLayoutModeAttributeValue operation.
    /// 設定具有 schema awareness 的表格版面配置模式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的表格版面配置模式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTableLayoutModeAttributeValue(string localName, string namespaceUri, OdfTableLayoutMode value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 表格版面配置模式。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfDatabaseRule? GetDatabaseRuleAttributeValue(string localName, string namespaceUri) => GetDatabaseRuleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetDatabaseRuleAttributeValue operation.
    /// 取得具有 schema awareness 的資料庫參照動作規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的資料庫參照動作規則；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfDatabaseRule? GetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfDatabaseRule>(value);
    }


    /// <summary>
    /// Executes the SetDatabaseRuleAttributeValue operation.
    /// 設定具有 schema awareness 的資料庫參照動作規則屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的資料庫參照動作規則</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetDatabaseRuleAttributeValue(string localName, string namespaceUri, OdfDatabaseRule value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 資料庫參照動作規則。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfPresentationPresetClass? GetPresentationPresetClassAttributeValue(string localName, string namespaceUri) => GetPresentationPresetClassAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetPresentationPresetClassAttributeValue operation.
    /// 取得具有 schema awareness 的簡報預設動畫類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的簡報預設動畫類別；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfPresentationPresetClass? GetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfPresentationPresetClass>(value);
    }


    /// <summary>
    /// Executes the SetPresentationPresetClassAttributeValue operation.
    /// 設定具有 schema awareness 的簡報預設動畫類別屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的簡報預設動畫類別</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetPresentationPresetClassAttributeValue(string localName, string namespaceUri, OdfPresentationPresetClass value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 簡報預設動畫類別。"), prefix, version);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public OdfNumberTransliterationStyle? GetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri) => GetNumberTransliterationStyleAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Executes the GetNumberTransliterationStyleAttributeValue operation.
    /// 取得具有 schema awareness 的數字音譯樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的數字音譯樣式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfNumberTransliterationStyle? GetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetEnumToken<OdfNumberTransliterationStyle>(value);
    }


    /// <summary>
    /// Executes the SetNumberTransliterationStyleAttributeValue operation.
    /// 設定具有 schema awareness 的數字音譯樣式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的數字音譯樣式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetNumberTransliterationStyleAttributeValue(string localName, string namespaceUri, OdfNumberTransliterationStyle value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatEnumToken(value, "未知的 ODF 數字音譯樣式。"), prefix, version);
    }

    #endregion
}

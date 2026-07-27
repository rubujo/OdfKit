using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Core;
/// <summary>
/// Adds metadata accessors for ODF document properties.
/// 提供 ODF 文件屬性的中繼資料存取器。
/// </summary>

public abstract partial class OdfDocument
{
    #region Metadata API (meta.xml)

    /// <summary>
    /// Gets or sets the document title.
    /// 取得或設定文件標題。
    /// </summary>
    public string? Title
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:title");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:title", value);
    }

    /// <summary>
    /// Gets or sets the document creator.
    /// 取得或設定文件建立者。
    /// </summary>
    public string? Creator
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:creator");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:creator", value);
    }

    /// <summary>
    /// Gets or sets the document description.
    /// 取得或設定文件描述。
    /// </summary>
    public string? Description
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:description");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:description", value);
    }

    /// <summary>
    /// Gets or sets the document subject.
    /// 取得或設定文件主旨。
    /// </summary>
    public string? Subject
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:subject");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:subject", value);
    }

    /// <summary>
    /// Gets or sets the document language tag.
    /// 取得或設定文件語言。
    /// </summary>
    public string? Language
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:language");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:language", value);
    }

    /// <summary>
    /// Gets or sets the document creation timestamp.
    /// 取得或設定文件建立日期。
    /// </summary>
    public DateTime? CreationDate
    {
        get => OdfDocumentMetadataEngine.ParseMetaDate(OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "meta:creation-date"));
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "meta:creation-date", OdfDocumentMetadataEngine.FormatMetaDate(value));
    }

    /// <summary>
    /// Gets or sets the document modification timestamp.
    /// 取得或設定文件修改日期。
    /// </summary>
    public DateTime? ModificationDate
    {
        get => OdfDocumentMetadataEngine.ParseMetaDate(OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:date"));
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:date", OdfDocumentMetadataEngine.FormatMetaDate(value));
    }

    /// <summary>
    /// Gets or sets metadata describing the source template.
    /// 取得或設定文件來源範本中繼資料。
    /// </summary>
    public OdfTemplateMetadata? TemplateMetadata
    {
        get => OdfDocumentMetadataEngine.GetTemplateMetadata(MetaDom);
        set => OdfDocumentMetadataEngine.SetTemplateMetadata(MetaDom, value);
    }

    /// <summary>
    /// 設定自訂中繼資料屬性。
    /// </summary>
    /// <param name="name">屬性名稱</param>
    /// <param name="value">屬性值</param>
    /// <param name="type">ODF 中繼資料值類型，例如 string、float、boolean 或 date</param>
    internal void SetCustomProperty(string name, object value, string type)
        => OdfDocumentMetadataEngine.SetCustomProperty(MetaDom, name, value, type);

    /// <summary>
    /// Sets a string custom metadata property.
    /// 設定字串類型的自訂中繼資料屬性。
    /// </summary>
    public void SetCustomProperty(string name, string value) => SetCustomProperty(name, (object)value, "string");

    /// <summary>
    /// Sets an integer custom metadata property.
    /// 設定整數類型的自訂中繼資料屬性。
    /// </summary>
    public void SetCustomProperty(string name, int value) => SetCustomProperty(name, (object)value, "float");

    /// <summary>
    /// Sets a floating-point custom metadata property.
    /// 設定浮點數類型的自訂中繼資料屬性。
    /// </summary>
    public void SetCustomProperty(string name, double value) => SetCustomProperty(name, (object)value, "float");

    /// <summary>
    /// Sets a Boolean custom metadata property.
    /// 設定布林類型的自訂中繼資料屬性。
    /// </summary>
    public void SetCustomProperty(string name, bool value) => SetCustomProperty(name, (object)value, "boolean");

    /// <summary>
    /// Sets a date custom metadata property.
    /// 設定日期類型的自訂中繼資料屬性。
    /// </summary>
    public void SetCustomProperty(string name, DateTime value) => SetCustomProperty(name, (object)value, "date");

    /// <summary>
    /// Finds the custom metadata property.
    /// 尋找自訂中繼資料屬性。
    /// </summary>
    /// <param name="name">The custom property name. / 自訂屬性名稱。</param>
    /// <returns>The property value, or <see langword="null"/> when it does not exist. / 屬性值；若不存在則為 <see langword="null"/>。</returns>
    public object? FindCustomProperty(string name)
        => OdfDocumentMetadataEngine.FindCustomProperty(MetaDom, name);

    /// <summary>
    /// Finds and converts the custom metadata property to the specified type.
    /// 尋找自訂中繼資料屬性，並轉換成指定型別。
    /// </summary>
    /// <typeparam name="T">The target value type. / 目標值型別。</typeparam>
    /// <param name="name">The custom property name. / 自訂屬性名稱。</param>
    /// <returns>The converted property value, or the default value when missing or conversion fails. / 轉換後的屬性值；若不存在或轉換失敗則為預設值。</returns>
    public T? FindCustomProperty<T>(string name)
    {
        object? val = FindCustomProperty(name);
        if (val is null)
            return default;
        try
        { return (T)Convert.ChangeType(val, typeof(T), System.Globalization.CultureInfo.InvariantCulture); }
        catch { return default; }
    }

    /// <summary>
    /// Gets all custom metadata properties.
    /// 取得所有自訂中繼資料屬性的字典。
    /// </summary>
    /// <returns>A read-only dictionary keyed by property name. / 以屬性名稱為鍵的唯讀字典。</returns>
    public IReadOnlyDictionary<string, object?> GetAllCustomProperties()
        => OdfDocumentMetadataEngine.GetAllCustomProperties(MetaDom);

    #endregion

    #region Statistics & Document Structure Diagnostics

    /// <summary>
    /// Updates metadata statistics for the current document.
    /// 更新文件統計中繼資料。
    /// </summary>
    protected virtual void UpdateDocumentStatistics()
        => OdfDocumentMetadataEngine.UpdateDocumentStatistics(MetaDom, ContentDom);

    #endregion
}

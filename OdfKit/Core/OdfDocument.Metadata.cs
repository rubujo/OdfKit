using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Metadata API (meta.xml)

    /// <summary>
    /// 取得或設定文件標題。
    /// </summary>
    public string? Title
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:title");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:title", value);
    }

    /// <summary>
    /// 取得或設定文件建立者。
    /// </summary>
    public string? Creator
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:creator");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:creator", value);
    }

    /// <summary>
    /// 取得或設定文件描述。
    /// </summary>
    public string? Description
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:description");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:description", value);
    }

    /// <summary>
    /// 取得或設定文件主旨。
    /// </summary>
    public string? Subject
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:subject");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:subject", value);
    }

    /// <summary>
    /// 取得或設定文件語言。
    /// </summary>
    public string? Language
    {
        get => OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:language");
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:language", value);
    }

    /// <summary>
    /// 取得或設定文件建立日期。
    /// </summary>
    public DateTime? CreationDate
    {
        get => OdfDocumentMetadataEngine.ParseMetaDate(OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "meta:creation-date"));
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "meta:creation-date", OdfDocumentMetadataEngine.FormatMetaDate(value));
    }

    /// <summary>
    /// 取得或設定文件修改日期。
    /// </summary>
    public DateTime? ModificationDate
    {
        get => OdfDocumentMetadataEngine.ParseMetaDate(OdfDocumentMetadataEngine.GetMetaElementText(MetaDom, "dc:date"));
        set => OdfDocumentMetadataEngine.SetMetaElementText(MetaDom, "dc:date", OdfDocumentMetadataEngine.FormatMetaDate(value));
    }

    /// <summary>
    /// 設定自訂中繼資料屬性。
    /// </summary>
    /// <param name="name">屬性名稱。</param>
    /// <param name="value">屬性值。</param>
    /// <param name="type">ODF 中繼資料值類型，例如 string、float、boolean 或 date。</param>
    internal void SetCustomProperty(string name, object value, string type)
        => OdfDocumentMetadataEngine.SetCustomProperty(MetaDom, name, value, type);

    /// <summary>設定字串類型的自訂屬性。</summary>
    public void SetCustomProperty(string name, string value) => SetCustomProperty(name, (object)value, "string");

    /// <summary>設定整數類型的自訂屬性。</summary>
    public void SetCustomProperty(string name, int value) => SetCustomProperty(name, (object)value, "float");

    /// <summary>設定浮點數類型的自訂屬性。</summary>
    public void SetCustomProperty(string name, double value) => SetCustomProperty(name, (object)value, "float");

    /// <summary>設定布林類型的自訂屬性。</summary>
    public void SetCustomProperty(string name, bool value) => SetCustomProperty(name, (object)value, "boolean");

    /// <summary>設定日期類型的自訂屬性。</summary>
    public void SetCustomProperty(string name, DateTime value) => SetCustomProperty(name, (object)value, "date");

    /// <summary>
    /// 取得自訂中繼資料屬性。
    /// </summary>
    /// <param name="name">屬性名稱。</param>
    /// <returns>屬性值；若不存在則為 <see langword="null"/>。</returns>
    public object? GetCustomProperty(string name)
        => OdfDocumentMetadataEngine.GetCustomProperty(MetaDom, name);

    /// <summary>
    /// 以強型別讀取自訂中繼資料屬性，並轉換成指定型別。
    /// </summary>
    /// <typeparam name="T">目標型別（string、int、double、bool、DateTime）。</typeparam>
    /// <param name="name">屬性名稱。</param>
    /// <returns>轉換後的屬性值；若不存在或轉換失敗則為預設值。</returns>
    public T? GetCustomProperty<T>(string name)
    {
        object? val = GetCustomProperty(name);
        if (val is null)
            return default;
        try
        { return (T)Convert.ChangeType(val, typeof(T)); }
        catch { return default; }
    }

    /// <summary>
    /// 取得所有自訂中繼資料屬性的字典。
    /// </summary>
    /// <returns>以屬性名稱為 Key 的唯讀字典。</returns>
    public IReadOnlyDictionary<string, object?> GetAllCustomProperties()
        => OdfDocumentMetadataEngine.GetAllCustomProperties(MetaDom);

    #endregion

    #region Statistics & Document Structure Diagnostics

    /// <summary>
    /// 更新文件統計中繼資料。
    /// </summary>
    protected virtual void UpdateDocumentStatistics()
        => OdfDocumentMetadataEngine.UpdateDocumentStatistics(MetaDom, ContentDom);

    #endregion
}

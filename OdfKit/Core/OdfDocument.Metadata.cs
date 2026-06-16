using System;
using System.Collections.Generic;
using OdfKit.Core;
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
        get => GetMetaElementText("dc:title");
        set => SetMetaElementText("dc:title", value);
    }

    /// <summary>
    /// 取得或設定文件建立者。
    /// </summary>
    public string? Creator
    {
        get => GetMetaElementText("dc:creator");
        set => SetMetaElementText("dc:creator", value);
    }

    /// <summary>
    /// 取得或設定文件描述。
    /// </summary>
    public string? Description
    {
        get => GetMetaElementText("dc:description");
        set => SetMetaElementText("dc:description", value);
    }

    /// <summary>
    /// 取得或設定文件主旨。
    /// </summary>
    public string? Subject
    {
        get => GetMetaElementText("dc:subject");
        set => SetMetaElementText("dc:subject", value);
    }

    /// <summary>
    /// 取得或設定文件語言。
    /// </summary>
    public string? Language
    {
        get => GetMetaElementText("dc:language");
        set => SetMetaElementText("dc:language", value);
    }

    /// <summary>
    /// 取得或設定文件建立日期。
    /// </summary>
    public DateTime? CreationDate
    {
        get => ParseMetaDate(GetMetaElementText("meta:creation-date"));
        set => SetMetaElementText("meta:creation-date", FormatMetaDate(value));
    }

    /// <summary>
    /// 取得或設定文件修改日期。
    /// </summary>
    public DateTime? ModificationDate
    {
        get => ParseMetaDate(GetMetaElementText("dc:date"));
        set => SetMetaElementText("dc:date", FormatMetaDate(value));
    }

    /// <summary>
    /// 設定自訂中繼資料屬性。
    /// </summary>
    /// <param name="name">屬性名稱。</param>
    /// <param name="value">屬性值。</param>
    /// <param name="type">ODF 中繼資料值類型，例如 string、float、boolean 或 date。</param>
    internal void SetCustomProperty(string name, object value, string type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name cannot be empty.", nameof(name));

        if (name.Contains(":"))
        {
            string oldName = name;
            name = name.Replace(":", "_");
            OdfKitDiagnostics.Warn($"Custom property name '{oldName}' contains invalid character ':'. Renamed to '{name}' for Excel compatibility.");
        }

        var metaRoot = FindOrCreateMetaRoot();

        OdfNode? existing = FindCustomPropertyNode(metaRoot, name);
        if (existing != null)
            metaRoot.RemoveChild(existing);

        var propNode = new OdfNode(OdfNodeType.Element, "user-defined", OdfNamespaces.Meta, "meta");
        propNode.SetAttribute("name", OdfNamespaces.Meta, name, "meta");
        propNode.SetAttribute("value-type", OdfNamespaces.Meta, type, "meta");
        propNode.TextContent = FormatValue(value, type);

        metaRoot.AppendChild(propNode);
    }

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
    {
        var metaRoot = FindOrCreateMetaRoot();
        var propNode = FindCustomPropertyNode(metaRoot, name);
        if (propNode == null)
            return null;

        string? type = propNode.GetAttribute("value-type", OdfNamespaces.Meta);
        string valStr = propNode.TextContent;
        return ParseValue(valStr, type);
    }

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
    {
        var metaRoot = FindOrCreateMetaRoot();
        var result = new Dictionary<string, object?>();
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "user-defined" && child.NamespaceUri == OdfNamespaces.Meta)
            {
                string? n = child.GetAttribute("name", OdfNamespaces.Meta);
                if (!string.IsNullOrEmpty(n))
                {
                    string? type = child.GetAttribute("value-type", OdfNamespaces.Meta);
                    result[n!] = ParseValue(child.TextContent, type);
                }
            }
        }
        return result;
    }


    #endregion

    #region Statistics & Document Structure Diagnostics


    /// <summary>
    /// 更新文件統計中繼資料。
    /// </summary>
    protected virtual void UpdateDocumentStatistics()
    {
        int wordCount = 0;
        int charCount = 0;
        int paragraphCount = 0;
        int tableCount = 0;
        int imageCount = 0;

        TraverseForStats(ContentDom, ref wordCount, ref charCount, ref paragraphCount, ref tableCount, ref imageCount);

        var metaRoot = FindOrCreateMetaRoot();
        OdfNode? statNode = null;
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "document-statistic" && child.NamespaceUri == OdfNamespaces.Meta)
            {
                statNode = child;
                break;
            }
        }

        if (statNode == null)
        {
            statNode = new OdfNode(OdfNodeType.Element, "document-statistic", OdfNamespaces.Meta, "meta");
            metaRoot.AppendChild(statNode);
        }

        statNode.SetAttribute("word-count", OdfNamespaces.Meta, wordCount.ToString(), "meta");
        statNode.SetAttribute("character-count", OdfNamespaces.Meta, charCount.ToString(), "meta");
        statNode.SetAttribute("paragraph-count", OdfNamespaces.Meta, paragraphCount.ToString(), "meta");
        statNode.SetAttribute("table-count", OdfNamespaces.Meta, tableCount.ToString(), "meta");
        statNode.SetAttribute("image-count", OdfNamespaces.Meta, imageCount.ToString(), "meta");
        statNode.SetAttribute("page-count", OdfNamespaces.Meta, "1", "meta"); // Layout engine placeholder
    }

    private void TraverseForStats(OdfNode node, ref int words, ref int chars, ref int paragraphs, ref int tables, ref int images)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            chars += text.Length;

            string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            words += parts.Length;
            return;
        }

        if (node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text)
            paragraphs++;
        else if (node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table)
            tables++;
        else if (node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw)
            images++;

        foreach (var child in node.Children)
        {
            TraverseForStats(child, ref words, ref chars, ref paragraphs, ref tables, ref images);
        }
    }


    #endregion

}

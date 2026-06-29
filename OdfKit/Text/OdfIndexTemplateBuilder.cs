using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Builds index entry templates.
/// 用於建構索引專案範本的建立器。
/// </summary>
/// <param name="template">The target template OdfNode. / 目標範本 OdfNode 節點。</param>
public class OdfIndexTemplateBuilder(OdfNode template)
{
    private readonly OdfNode _template = template;

    /// <summary>
    /// Adds a text field entry to the template.
    /// 在範本中新增文字欄位專案。
    /// </summary>
    /// <returns>The current builder instance, to support chained calls. / 目前的建立器執行個體，以支援鏈結呼叫。</returns>
    public OdfIndexTemplateBuilder AddText()
    {
        _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-text", OdfNamespaces.Text, "text"));
        return this;
    }

    /// <summary>
    /// Adds a tab stop entry to the template.
    /// 在範本中新增定位點專案。
    /// </summary>
    /// <param name="type">The tab type. / 定位類型。</param>
    /// <param name="leaderChar">The leader character. / 前置字元。</param>
    /// <returns>The current builder instance, to support chained calls. / 目前的建立器執行個體，以支援鏈結呼叫。</returns>
    public OdfIndexTemplateBuilder AddTabStop(string type = "right", char leaderChar = '.')
    {
        var tab = OdfNodeFactory.CreateElement("index-entry-tab-stop", OdfNamespaces.Text, "text");
        tab.SetAttribute("type", OdfNamespaces.Style, type, "style");
        tab.SetAttribute("leader-char", OdfNamespaces.Style, leaderChar.ToString(), "style");
        _template.AppendChild(tab);
        return this;
    }

    /// <summary>
    /// Adds a page number entry to the template.
    /// 在範本中新增頁碼專案。
    /// </summary>
    /// <returns>The current builder instance, to support chained calls. / 目前的建立器執行個體，以支援鏈結呼叫。</returns>
    public OdfIndexTemplateBuilder AddPageNumber()
    {
        _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-page-number", OdfNamespaces.Text, "text"));
        return this;
    }

    /// <summary>
    /// Adds a custom text string entry to the template.
    /// 在範本中新增自訂文字字串專案。
    /// </summary>
    /// <param name="text">The custom text content. / 自訂的文字內容。</param>
    /// <returns>The current builder instance, to support chained calls. / 目前的建立器執行個體，以支援鏈結呼叫。</returns>
    public OdfIndexTemplateBuilder AddSpan(string text)
    {
        var span = OdfNodeFactory.CreateElement("index-entry-span", OdfNamespaces.Text, "text");
        span.TextContent = text;
        _template.AppendChild(span);
        return this;
    }
}


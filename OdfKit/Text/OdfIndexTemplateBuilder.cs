using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides odf index template builder.
/// 用於建構索引專案範本的建立器。
/// </summary>
/// <param name="template">The value to use. / 目標範本 OdfNode 節點</param>
public class OdfIndexTemplateBuilder(OdfNode template)
{
    private readonly OdfNode _template = template;

    /// <summary>
    /// Provides add text.
    /// 在範本中新增文字欄位專案。
    /// </summary>
    /// <returns>The result. / 目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddText()
    {
        _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-text", OdfNamespaces.Text, "text"));
        return this;
    }

    /// <summary>
    /// Provides add tab stop.
    /// 在範本中新增定位點專案。
    /// </summary>
    /// <param name="type">The value to use. / 定位類型</param>
    /// <param name="leaderChar">The value to use. / 前置字元</param>
    /// <returns>The result. / 目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddTabStop(string type = "right", char leaderChar = '.')
    {
        var tab = OdfNodeFactory.CreateElement("index-entry-tab-stop", OdfNamespaces.Text, "text");
        tab.SetAttribute("type", OdfNamespaces.Style, type, "style");
        tab.SetAttribute("leader-char", OdfNamespaces.Style, leaderChar.ToString(), "style");
        _template.AppendChild(tab);
        return this;
    }

    /// <summary>
    /// Provides add page number.
    /// 在範本中新增頁碼專案。
    /// </summary>
    /// <returns>The result. / 目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddPageNumber()
    {
        _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-page-number", OdfNamespaces.Text, "text"));
        return this;
    }

    /// <summary>
    /// Provides add span.
    /// 在範本中新增自訂文字字串專案。
    /// </summary>
    /// <param name="text">The text or value. / 自訂的文字內容</param>
    /// <returns>The result. / 目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddSpan(string text)
    {
        var span = OdfNodeFactory.CreateElement("index-entry-span", OdfNamespaces.Text, "text");
        span.TextContent = text;
        _template.AppendChild(span);
        return this;
    }
}


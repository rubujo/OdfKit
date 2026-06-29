using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Builds bibliography entry templates.
/// 用於建構文獻目錄專案範本的建立器。
/// </summary>
/// <param name="template">The target template OdfNode. / 目標範本 OdfNode 節點。</param>
public class OdfBibliographyTemplateBuilder(OdfNode template)
{
    private readonly OdfNode _template = template;

    /// <summary>
    /// Adds a custom text string entry to the bibliography template.
    /// 在文獻範本中新增自訂文字字串專案。
    /// </summary>
    /// <param name="text">The custom text content. / 自訂的文字內容。</param>
    /// <returns>The current builder instance, to support chained calls. / 目前的建立器執行個體，以支援鏈結呼叫。</returns>
    public OdfBibliographyTemplateBuilder AddSpan(string text)
    {
        var span = OdfNodeFactory.CreateElement("index-entry-span", OdfNamespaces.Text, "text");
        span.TextContent = text;
        _template.AppendChild(span);
        return this;
    }

    /// <summary>
    /// Adds a bibliography field entry to the bibliography template.
    /// 在文獻範本中新增文獻欄位專案。
    /// </summary>
    /// <param name="dataField">The bibliography data field name. / 文獻資料欄位名稱。</param>
    /// <returns>The current builder instance, to support chained calls. / 目前的建立器執行個體，以支援鏈結呼叫。</returns>
    public OdfBibliographyTemplateBuilder AddBibliographyField(string dataField)
    {
        var field = OdfNodeFactory.CreateElement("index-entry-bibliography", OdfNamespaces.Text, "text");
        field.SetAttribute("bibliography-data-field", OdfNamespaces.Text, dataField, "text");
        _template.AppendChild(field);
        return this;
    }
}


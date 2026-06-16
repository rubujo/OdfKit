using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 用於建構文獻目錄項目範本的建立器。
/// </summary>
/// <param name="template">目標範本 OdfNode 節點</param>
public class OdfBibliographyTemplateBuilder(OdfNode template)
{
    private readonly OdfNode _template = template;

    /// <summary>
    /// 在文獻範本中新增自訂文字字串項目。
    /// </summary>
    /// <param name="text">自訂的文字內容</param>
    /// <returns>目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfBibliographyTemplateBuilder AddSpan(string text)
    {
        var span = OdfNodeFactory.CreateElement("index-entry-span", OdfNamespaces.Text, "text");
        span.TextContent = text;
        _template.AppendChild(span);
        return this;
    }

    /// <summary>
    /// 在文獻範本中新增文獻欄位項目。
    /// </summary>
    /// <param name="dataField">文獻資料欄位名稱</param>
    /// <returns>目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfBibliographyTemplateBuilder AddBibliographyField(string dataField)
    {
        var field = OdfNodeFactory.CreateElement("index-entry-bibliography", OdfNamespaces.Text, "text");
        field.SetAttribute("bibliography-data-field", OdfNamespaces.Text, dataField, "text");
        _template.AppendChild(field);
        return this;
    }
}


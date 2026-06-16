using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Multi-Column Sections Layouts


    /// <summary>
    /// 新增多欄版面配置區段至文件本文結尾。
    /// </summary>
    /// <param name="name">區段名稱</param>
    /// <param name="columnCount">欄位數量</param>
    /// <param name="gap">欄間距寬度</param>
    /// <returns>新建立的區段物件</returns>
    public OdfSection AddSection(string name, int columnCount, OdfLength gap)
    {
        var section = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        section.SetAttribute("name", OdfNamespaces.Text, name, "text");

        string styleName = StyleEngine.GetOrCreateLocalStyle(section, "section").GetAttribute("name", OdfNamespaces.Style) ?? "S1";
        StyleEngine.SetLocalStyleProperty(section, "section", "section-properties", "column-count", OdfNamespaces.Fo, columnCount.ToString(), "fo");
        StyleEngine.SetLocalStyleProperty(section, "section", "section-properties", "column-gap", OdfNamespaces.Fo, gap.ToString(), "fo");

        BodyTextRoot.AppendChild(section);
        return new OdfSection(section, this);
    }

    /// <summary>
    /// 在文件本文結尾新增一個指向外部子文件參照的區段（用於主文件）。
    /// </summary>
    /// <param name="name">區段名稱。</param>
    /// <param name="subDocumentUri">外部子文件的相對或絕對 URI/路徑（將寫入 xlink:href）。</param>
    /// <returns>新建立的區段物件。</returns>
    public OdfSection AddSubDocumentReference(string name, string subDocumentUri)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("區段名稱不可為空。", nameof(name));
        if (string.IsNullOrEmpty(subDocumentUri))
            throw new ArgumentException("子文件 URI 不可為空。", nameof(subDocumentUri));

        var sectionNode = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        sectionNode.SetAttribute("name", OdfNamespaces.Text, name, "text");

        var sourceNode = new OdfNode(OdfNodeType.Element, "section-source", OdfNamespaces.Text, "text");
        sourceNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        sourceNode.SetAttribute("href", OdfNamespaces.XLink, subDocumentUri, "xlink");
        sourceNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        sourceNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        sectionNode.AppendChild(sourceNode);
        BodyTextRoot.AppendChild(sectionNode);

        return new OdfSection(sectionNode, this);
    }


    #endregion
}

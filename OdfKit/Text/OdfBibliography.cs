using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents a bibliography in an ODF document.
/// 表示 ODF 文件中的文獻目錄。
/// </summary>
public class OdfBibliography : OdfIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfBibliography"/> class.
    /// 初始化 <see cref="OdfBibliography"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The OdfNode of the bibliography. / 文獻目錄的 OdfNode 節點。</param>
    /// <param name="doc">The owning text document. / 所屬的文字文件。</param>
    public OdfBibliography(OdfNode node, TextDocument doc) : base(node, doc)
    {
        FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
    }

    /// <summary>
    /// Gets the XML local name of the bibliography source node.
    /// 取得文獻目錄來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>The XML local name. / XML 本地名稱。</returns>
    protected override string GetSourceLocalName() => "bibliography-source";

    /// <summary>
    /// Adds an entry template for the bibliography.
    /// 新增文獻目錄專案範本。
    /// </summary>
    /// <param name="bibType">The bibliography type. / 文獻類型。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    /// <returns>An <see cref="OdfBibliographyTemplateBuilder"/> instance for constructing the bibliography template. / 用於建構文獻範本的 <see cref="OdfBibliographyTemplateBuilder"/> 執行個體。</returns>
    public OdfBibliographyTemplateBuilder AddEntryTemplate(string bibType, string styleName)
    {
        var src = FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        var template = OdfNodeFactory.CreateElement("bibliography-entry-template", OdfNamespaces.Text, "text");
        template.SetAttribute("bibliography-type", OdfNamespaces.Text, bibType, "text");
        template.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        src.AppendChild(template);
        return new OdfBibliographyTemplateBuilder(template);
    }

    /// <summary>
    /// Updates the content of the bibliography.
    /// 更新文獻目錄的內容。
    /// </summary>
    public override void Update()
    {
        var body = FindChild(Node, "index-body", OdfNamespaces.Text);
        if (body is null)
            return;

        var title = FindChild(body, "index-title", OdfNamespaces.Text) ?? body.Children.FirstOrDefault(c => c.LocalName == "p" && c.NamespaceUri == OdfNamespaces.Text);
        body.Children.Clear();
        if (title is not null)
            body.AppendChild(title);

        var rawMarks = new List<OdfBibliographyMarkInfo>();
        ScanBibliographyMarks(Doc.BodyTextRoot, rawMarks);

        var uniqueMarks = new List<OdfBibliographyMarkInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in rawMarks)
        {
            if (seen.Add(m.Identifier))
            {
                uniqueMarks.Add(m);
            }
        }

        // 預設排序：依識別碼按字母順序不區分大小寫排序
        uniqueMarks = uniqueMarks.OrderBy(m => m.Identifier, StringComparer.OrdinalIgnoreCase).ToList();

        var templates = new Dictionary<string, OdfNode>();
        var source = SourceNode;
        if (source is not null)
        {
            foreach (var child in source.Children)
            {
                if (child.LocalName == "bibliography-entry-template" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    string? bt = child.GetAttribute("bibliography-type", OdfNamespaces.Text);
                    if (bt is not null)
                    {
                        templates[bt] = child;
                    }
                }
            }
        }

        foreach (var bib in uniqueMarks)
        {
            templates.TryGetValue(bib.Type, out var template);
            var entryPara = BuildBibliographyEntryParagraph(bib, template);
            body.AppendChild(entryPara);
        }
    }

    private void ScanBibliographyMarks(OdfNode node, List<OdfBibliographyMarkInfo> rawMarks)
    {
        if (node.NodeType == OdfNodeType.Element &&
            node.LocalName == "bibliography-mark" &&
            node.NamespaceUri == OdfNamespaces.Text)
        {
            string id = node.GetAttribute("identifier", OdfNamespaces.Text) ?? "Ref";
            string type = node.GetAttribute("bibliography-type", OdfNamespaces.Text) ?? "book";

            var meta = new Dictionary<string, string>();
            foreach (var attr in node.Attributes)
            {
                if (attr.Key.NamespaceUri == OdfNamespaces.Text)
                {
                    meta[attr.Key.LocalName] = attr.Value;
                }
            }

            rawMarks.Add(new OdfBibliographyMarkInfo(id, type, meta));
        }

        foreach (var child in node.Children)
        {
            if (child.LocalName == "index-body" && child.NamespaceUri == OdfNamespaces.Text)
                continue;
            ScanBibliographyMarks(child, rawMarks);
        }
    }

    private OdfNode BuildBibliographyEntryParagraph(OdfBibliographyMarkInfo bib, OdfNode? template)
    {
        var p = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        string styleName = template?.GetAttribute("style-name", OdfNamespaces.Text) ?? "Bibliography_20_1";
        p.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");

        if (template is not null && template.Children.Count > 0)
        {
            foreach (var child in template.Children)
            {
                if (child.LocalName == "index-entry-span")
                {
                    var span = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
                    span.TextContent = child.TextContent;
                    p.AppendChild(span);
                }
                else if (child.LocalName == "index-entry-bibliography")
                {
                    string? field = child.GetAttribute("bibliography-data-field", OdfNamespaces.Text);
                    if (field is not null && bib.Metadata.TryGetValue(field, out var val))
                    {
                        var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = val };
                        p.AppendChild(textNode);
                    }
                }
            }
        }
        else
        {
            // 預設遞補格式：[Identifier] Author, Title (Year)
            string text = $"[{bib.Identifier}] ";
            if (bib.Metadata.TryGetValue("author", out var author))
                text += $"{author}, ";
            if (bib.Metadata.TryGetValue("title", out var title))
                text += $"\"{title}\"";
            if (bib.Metadata.TryGetValue("year", out var year))
                text += $" ({year})";

            p.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
        }

        return p;
    }
}


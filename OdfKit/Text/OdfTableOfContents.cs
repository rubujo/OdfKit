using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents a table of contents in an ODF document.
/// 表示 ODF 文件中的目錄。
/// </summary>
public class OdfTableOfContents : OdfIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfTableOfContents"/> class.
    /// 初始化 <see cref="OdfTableOfContents"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The OdfNode of the table of contents. / 目錄的 OdfNode 節點。</param>
    /// <param name="doc">The owning text document. / 所屬的文字文件。</param>
    public OdfTableOfContents(OdfNode node, TextDocument doc) : base(node, doc)
    {
        FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
    }

    /// <summary>
    /// Gets the XML local name of the table of contents source node.
    /// 取得目錄來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>The XML local name. / XML 本地名稱。</returns>
    protected override string GetSourceLocalName() => "table-of-content-source";

    /// <summary>
    /// Gets or sets the outline level of the table of contents.
    /// 取得或設定目錄的大綱階層。
    /// </summary>
    public int OutlineLevel
    {
        get => int.TryParse(SourceNode?.GetAttribute("outline-level", OdfNamespaces.Text), out var lvl) ? lvl : 10;
        set => SourceNode?.SetAttribute("outline-level", OdfNamespaces.Text, value.ToString(CultureInfo.InvariantCulture), "text");
    }

    /// <summary>
    /// Gets or sets a value indicating whether the outline level is used to generate the table of contents.
    /// 取得或設定一個值，指出是否使用大綱階層來產生目錄。
    /// </summary>
    public bool UseOutlineLevel
    {
        get => SourceNode?.GetAttribute("use-outline-level", OdfNamespaces.Text) != "false";
        set => SourceNode?.SetAttribute("use-outline-level", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// Gets or sets a value indicating whether index marks are used to generate the table of contents.
    /// 取得或設定一個值，指出是否使用索引標記來產生目錄。
    /// </summary>
    public bool UseIndexMarks
    {
        get => SourceNode?.GetAttribute("use-index-marks", OdfNamespaces.Text) == "true";
        set => SourceNode?.SetAttribute("use-index-marks", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// Adds an entry template for the table of contents.
    /// 新增目錄專案範本。
    /// </summary>
    /// <param name="outlineLevel">The outline level. / 大綱階層。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    /// <returns>An <see cref="OdfIndexTemplateBuilder"/> instance for constructing the template. / 用於建構範本的 <see cref="OdfIndexTemplateBuilder"/> 執行個體。</returns>
    public OdfIndexTemplateBuilder AddEntryTemplate(int outlineLevel, string styleName)
    {
        var src = FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        var template = OdfNodeFactory.CreateElement("table-of-content-entry-template", OdfNamespaces.Text, "text");
        template.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(CultureInfo.InvariantCulture), "text");
        template.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        src.AppendChild(template);
        return new OdfIndexTemplateBuilder(template);
    }

    /// <summary>
    /// Updates the content of the table of contents.
    /// 更新目錄的內容。
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

        var headings = new List<OdfHeadingInfo>();
        ScanHeadings(Doc.BodyTextRoot, headings);

        int maxLevel = OutlineLevel;
        var templates = new Dictionary<int, OdfNode>();
        var source = SourceNode;
        if (source is not null)
        {
            foreach (var child in source.Children)
            {
                if (child.LocalName == "table-of-content-entry-template" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    if (int.TryParse(child.GetAttribute("outline-level", OdfNamespaces.Text), out var ol))
                    {
                        templates[ol] = child;
                    }
                }
            }
        }

        foreach (var heading in headings)
        {
            if (heading.Level > maxLevel)
                continue;

            templates.TryGetValue(heading.Level, out var template);
            var entryPara = BuildTocEntryParagraph(heading, template);
            body.AppendChild(entryPara);
        }
    }

    private class OdfHeadingInfo(string text, int level, string? anchor)
    {
        public string Text { get; } = text;
        public int Level { get; } = level;
        public string? Anchor { get; } = anchor;
    }

    private void ScanHeadings(OdfNode node, List<OdfHeadingInfo> headings)
    {
        if (node.NodeType == OdfNodeType.Element &&
            node.LocalName == "h" &&
            node.NamespaceUri == OdfNamespaces.Text)
        {
            int level = int.TryParse(node.GetAttribute("outline-level", OdfNamespaces.Text), out var lvl) ? lvl : 1;

            string? anchor = null;
            var refMark = FindChild(node, "reference-mark", OdfNamespaces.Text) ??
                          FindChild(node, "reference-mark-start", OdfNamespaces.Text);
            if (refMark is not null)
            {
                anchor = refMark.GetAttribute("name", OdfNamespaces.Text);
            }

            if (string.IsNullOrEmpty(anchor))
            {
                anchor = "_Toc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                refMark = OdfNodeFactory.CreateElement("reference-mark", OdfNamespaces.Text, "text");
                refMark.SetAttribute("name", OdfNamespaces.Text, anchor, "text");
                node.AppendChild(refMark);
            }

            headings.Add(new OdfHeadingInfo(node.TextContent, level, anchor));
        }

        foreach (var child in node.Children)
        {
            if (child.LocalName == "index-body" && child.NamespaceUri == OdfNamespaces.Text)
                continue;
            ScanHeadings(child, headings);
        }
    }

    private OdfNode BuildTocEntryParagraph(OdfHeadingInfo heading, OdfNode? template)
    {
        var p = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        string styleName = template?.GetAttribute("style-name", OdfNamespaces.Text) ?? $"Contents_{heading.Level}";
        p.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");

        OdfNode textContainer = p;
        if (!string.IsNullOrEmpty(heading.Anchor))
        {
            var link = OdfNodeFactory.CreateElement("a", OdfNamespaces.Text, "text");
            link.SetAttribute("href", OdfNamespaces.XLink, $"#{heading.Anchor}", "xlink");
            link.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            p.AppendChild(link);
            textContainer = link;
        }

        if (template is not null && template.Children.Count > 0)
        {
            foreach (var child in template.Children)
            {
                if (child.LocalName == "index-entry-text")
                {
                    var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = heading.Text };
                    textContainer.AppendChild(textNode);
                }
                else if (child.LocalName == "index-entry-tab-stop")
                {
                    var tab = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
                    textContainer.AppendChild(tab);
                }
                else if (child.LocalName == "index-entry-page-number")
                {
                    var pageNumText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "1" };
                    textContainer.AppendChild(pageNumText);
                }
                else if (child.LocalName == "index-entry-span")
                {
                    var span = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
                    span.TextContent = child.TextContent;
                    textContainer.AppendChild(span);
                }
            }
        }
        else
        {
            var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = heading.Text };
            textContainer.AppendChild(textNode);

            var tab = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
            textContainer.AppendChild(tab);

            var pageNumText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "1" };
            textContainer.AppendChild(pageNumText);
        }

        return p;
    }
}


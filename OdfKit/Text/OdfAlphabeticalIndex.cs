using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文件中的字母索引。
/// </summary>
public class OdfAlphabeticalIndex : OdfIndex
{
    /// <summary>
    /// 初始化 <see cref="OdfAlphabeticalIndex"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">字母索引的 OdfNode 節點</param>
    /// <param name="doc">所屬的文字文件</param>
    public OdfAlphabeticalIndex(OdfNode node, TextDocument doc) : base(node, doc)
    {
        FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
    }

    /// <summary>
    /// 取得字母索引來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>XML 本地名稱</returns>
    protected override string GetSourceLocalName() => "alphabetical-index-source";

    /// <summary>
    /// 取得或設定一個值，指出是否使用字母分隔符號。
    /// </summary>
    public bool AlphabeticalSeparators
    {
        get => SourceNode?.GetAttribute("alphabetical-separators", OdfNamespaces.Text) == "true";
        set => SourceNode?.SetAttribute("alphabetical-separators", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// 取得或設定一個值，指出是否合併相同的索引專案。
    /// </summary>
    public bool CombineEntries
    {
        get => SourceNode?.GetAttribute("combine-entries", OdfNamespaces.Text) == "true";
        set => SourceNode?.SetAttribute("combine-entries", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// 取得或設定一個值，指出索引排序時是否忽略大小寫。
    /// </summary>
    public bool IgnoreCase
    {
        get => SourceNode?.GetAttribute("ignore-case", OdfNamespaces.Text) == "true";
        set => SourceNode?.SetAttribute("ignore-case", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// 設定字母索引來源的屬性。
    /// </summary>
    /// <param name="commaSeparated">是否使用逗號分隔</param>
    /// <param name="ignoreCase">是否忽略大小寫</param>
    public void ConfigureSource(bool commaSeparated = false, bool ignoreCase = false)
    {
        var src = FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        src.SetAttribute("comma-separated", OdfNamespaces.Text, commaSeparated ? "true" : "false", "text");
        src.SetAttribute("ignore-case", OdfNamespaces.Text, ignoreCase ? "true" : "false", "text");
    }

    /// <summary>
    /// 新增字母索引專案範本。
    /// </summary>
    /// <param name="outlineLevel">大綱階層</param>
    /// <param name="styleName">樣式名稱</param>
    /// <returns>用於建構範本的 <see cref="OdfIndexTemplateBuilder"/> 執行個體</returns>
    public OdfIndexTemplateBuilder AddEntryTemplate(string outlineLevel, string styleName)
    {
        var src = FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        var template = OdfNodeFactory.CreateElement("alphabetical-index-entry-template", OdfNamespaces.Text, "text");
        template.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel, "text");
        template.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        src.AppendChild(template);
        return new OdfIndexTemplateBuilder(template);
    }

    /// <summary>
    /// 更新字母索引的內容。
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

        var marks = new List<OdfIndexMarkInfo>();
        ScanIndexMarks(Doc.BodyTextRoot, marks);

        var comparer = IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var sorted = marks
            .OrderBy(m => m.Key1 ?? m.Term, comparer)
            .ThenBy(m => m.Key2 ?? string.Empty, comparer)
            .ThenBy(m => m.Term, comparer)
            .ToList();

        // 讀取範本
        var templates = new Dictionary<string, OdfNode>();
        var source = SourceNode;
        if (source is not null)
        {
            foreach (var child in source.Children)
            {
                if (child.LocalName == "alphabetical-index-entry-template" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    string? ol = child.GetAttribute("outline-level", OdfNamespaces.Text);
                    if (ol is not null)
                    {
                        templates[ol] = child;
                    }
                }
            }
        }

        string? currentLetter = null;
        foreach (var m in sorted)
        {
            if (AlphabeticalSeparators)
            {
                string letter = string.IsNullOrEmpty(m.Key1 ?? m.Term) ? "" : (m.Key1 ?? m.Term).Substring(0, 1).ToUpperInvariant();
                if (letter != currentLetter)
                {
                    currentLetter = letter;
                    var sepPara = BuildSeparatorParagraph(currentLetter, templates.TryGetValue("separator", out var st) ? st : null);
                    body.AppendChild(sepPara);
                }
            }

            int level = 1;
            string text = m.Term;
            if (!string.IsNullOrEmpty(m.Key1))
            {
                level = 2;
                if (!string.IsNullOrEmpty(m.Key2))
                {
                    level = 3;
                }
            }

            templates.TryGetValue(level.ToString(), out var temp);
            var entryPara = BuildAlphabeticalEntryParagraph(text, level, temp);
            body.AppendChild(entryPara);
        }
    }

    private void ScanIndexMarks(OdfNode node, List<OdfIndexMarkInfo> marks)
    {
        if (node.NodeType == OdfNodeType.Element && node.NamespaceUri == OdfNamespaces.Text)
        {
            if (node.LocalName == "alphabetical-index-mark")
            {
                string stringValue = node.GetAttribute("string-value", OdfNamespaces.Text) ?? node.TextContent;
                string? key1 = node.GetAttribute("key1", OdfNamespaces.Text);
                string? key2 = node.GetAttribute("key2", OdfNamespaces.Text);
                marks.Add(new OdfIndexMarkInfo(stringValue, key1, key2));
            }
            else if (node.LocalName == "alphabetical-index-mark-start")
            {
                string stringValue = node.GetAttribute("string-value", OdfNamespaces.Text) ?? node.TextContent;
                string? key1 = node.GetAttribute("key1", OdfNamespaces.Text);
                string? key2 = node.GetAttribute("key2", OdfNamespaces.Text);
                if (string.IsNullOrEmpty(stringValue))
                {
                    stringValue = node.GetAttribute("id", OdfNamespaces.Text) ?? "Range";
                }
                marks.Add(new OdfIndexMarkInfo(stringValue, key1, key2));
            }
        }

        foreach (var child in node.Children)
        {
            if (child.LocalName == "index-body" && child.NamespaceUri == OdfNamespaces.Text)
                continue;
            ScanIndexMarks(child, marks);
        }
    }

    private OdfNode BuildSeparatorParagraph(string letter, OdfNode? template)
    {
        var p = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        string styleName = template?.GetAttribute("style-name", OdfNamespaces.Text) ?? "Index_20_Separator";
        p.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        p.TextContent = letter;
        return p;
    }

    private OdfNode BuildAlphabeticalEntryParagraph(string text, int level, OdfNode? template)
    {
        var p = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        string styleName = template?.GetAttribute("style-name", OdfNamespaces.Text) ?? $"Index_{level}";
        p.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");

        if (template is not null && template.Children.Count > 0)
        {
            foreach (var child in template.Children)
            {
                if (child.LocalName == "index-entry-text")
                {
                    p.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
                }
                else if (child.LocalName == "index-entry-tab-stop")
                {
                    p.AppendChild(OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text"));
                }
                else if (child.LocalName == "index-entry-page-number")
                {
                    p.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "1" });
                }
                else if (child.LocalName == "index-entry-span")
                {
                    var span = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
                    span.TextContent = child.TextContent;
                    p.AppendChild(span);
                }
            }
        }
        else
        {
            p.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
            p.AppendChild(OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text"));
            p.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "1" });
        }

        return p;
    }
}


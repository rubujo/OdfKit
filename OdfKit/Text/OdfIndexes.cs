using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文件中索引的抽象基底類別。
/// </summary>
public abstract class OdfIndex
{
    /// <summary>
    /// 取得與此索引相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; }

    /// <summary>
    /// 所屬的文字文件。
    /// </summary>
    protected readonly TextDocument Doc;

    /// <summary>
    /// 初始化 <see cref="OdfIndex"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">相關聯的 OdfNode 節點</param>
    /// <param name="doc">所屬的文字文件</param>
    protected OdfIndex(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得或設定索引的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// 取得索引的來源節點。
    /// </summary>
    public OdfNode? SourceNode => FindChild(Node, GetSourceLocalName(), OdfNamespaces.Text);

    /// <summary>
    /// 取得索引的本文節點。
    /// </summary>
    public OdfNode? BodyNode => FindChild(Node, "index-body", OdfNamespaces.Text);

    /// <summary>
    /// 取得索引來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>XML 本地名稱</returns>
    protected abstract string GetSourceLocalName();

    /// <summary>
    /// 尋找符合指定 XML 本地名稱與命名空間的第一個子項目。
    /// </summary>
    /// <param name="parent">父節點</param>
    /// <param name="localName">XML 本地名稱</param>
    /// <param name="ns">命名空間 URI</param>
    /// <returns>符合條件的子項目，若無則傳回 <c>null</c></returns>
    protected OdfNode? FindChild(OdfNode parent, string localName, string ns)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        return null;
    }

    /// <summary>
    /// 尋找或建立符合指定 XML 本地名稱與命名空間的子項目。
    /// </summary>
    /// <param name="parent">父節點</param>
    /// <param name="localName">XML 本地名稱</param>
    /// <param name="ns">命名空間 URI</param>
    /// <param name="prefix">命名空間前綴</param>
    /// <returns>現有的或新建立的子節點</returns>
    protected OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        var existing = FindChild(parent, localName, ns);
        if (existing is not null) return existing;

        var child = OdfNodeFactory.CreateElement(localName, ns, prefix);
        parent.AppendChild(child);
        return child;
    }
    
    /// <summary>
    /// 更新索引內容。
    /// </summary>
    public abstract void Update();
}

/// <summary>
/// 表示 ODF 文件中的目錄。
/// </summary>
public class OdfTableOfContents : OdfIndex
{
    /// <summary>
    /// 初始化 <see cref="OdfTableOfContents"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">目錄的 OdfNode 節點</param>
    /// <param name="doc">所屬的文字文件</param>
    public OdfTableOfContents(OdfNode node, TextDocument doc) : base(node, doc)
    {
        FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
    }

    /// <summary>
    /// 取得目錄來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>XML 本地名稱</returns>
    protected override string GetSourceLocalName() => "table-of-content-source";

    /// <summary>
    /// 取得或設定目錄的大綱階層。
    /// </summary>
    public int OutlineLevel
    {
        get => int.TryParse(SourceNode?.GetAttribute("outline-level", OdfNamespaces.Text), out var lvl) ? lvl : 10;
        set => SourceNode?.SetAttribute("outline-level", OdfNamespaces.Text, value.ToString(), "text");
    }

    /// <summary>
    /// 取得或設定一個值，指出是否使用大綱階層來產生目錄。
    /// </summary>
    public bool UseOutlineLevel
    {
        get => SourceNode?.GetAttribute("use-outline-level", OdfNamespaces.Text) != "false";
        set => SourceNode?.SetAttribute("use-outline-level", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// 取得或設定一個值，指出是否使用索引標記來產生目錄。
    /// </summary>
    public bool UseIndexMarks
    {
        get => SourceNode?.GetAttribute("use-index-marks", OdfNamespaces.Text) == "true";
        set => SourceNode?.SetAttribute("use-index-marks", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// 更新目錄的內容。
    /// </summary>
    public override void Update()
    {
        var body = FindChild(Node, "index-body", OdfNamespaces.Text);
        if (body is null) return;

        var title = FindChild(body, "index-title", OdfNamespaces.Text) ?? body.Children.FirstOrDefault(c => c.LocalName == "p" && c.NamespaceUri == OdfNamespaces.Text);
        body.Children.Clear();
        if (title is not null) body.AppendChild(title);

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
            if (heading.Level > maxLevel) continue;

            templates.TryGetValue(heading.Level, out var template);
            var entryPara = BuildTocEntryParagraph(heading, template);
            body.AppendChild(entryPara);
        }
    }

    private class OdfHeadingInfo(string text, int level, string anchor)
    {
        public string Text { get; } = text;
        public int Level { get; } = level;
        public string Anchor { get; } = anchor;
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
                anchor = $"_Toc_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                var newRef = OdfNodeFactory.CreateElement("reference-mark", OdfNamespaces.Text, "text");
                newRef.SetAttribute("name", OdfNamespaces.Text, anchor, "text");
                if (node.Children.Count > 0)
                    node.InsertBefore(newRef, node.Children[0]);
                else
                    node.AppendChild(newRef);
            }
            
            headings.Add(new OdfHeadingInfo(node.TextContent, level, anchor!));
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

        var link = OdfNodeFactory.CreateElement("a", OdfNamespaces.Text, "text");
        link.SetAttribute("href", OdfNamespaces.XLink, $"#{heading.Anchor}", "xlink");
        link.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        p.AppendChild(link);

        if (template is not null && template.Children.Count > 0)
        {
            foreach (var child in template.Children)
            {
                if (child.LocalName == "index-entry-text")
                {
                    var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = heading.Text };
                    link.AppendChild(textNode);
                }
                else if (child.LocalName == "index-entry-tab-stop")
                {
                    var tab = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
                    link.AppendChild(tab);
                }
                else if (child.LocalName == "index-entry-page-number")
                {
                    var pageNumText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "1" };
                    link.AppendChild(pageNumText);
                }
                else if (child.LocalName == "index-entry-span")
                {
                    var span = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
                    span.TextContent = child.TextContent;
                    link.AppendChild(span);
                }
            }
        }
        else
        {
            var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = heading.Text };
            link.AppendChild(textNode);

            var tab = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
            link.AppendChild(tab);

            var pageNumText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "1" };
            link.AppendChild(pageNumText);
        }

        return p;
    }
}

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
    /// 取得或設定一個值，指出是否合併相同的索引項目。
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
    /// 新增字母索引項目範本。
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
        if (body is null) return;

        var title = FindChild(body, "index-title", OdfNamespaces.Text) ?? body.Children.FirstOrDefault(c => c.LocalName == "p" && c.NamespaceUri == OdfNamespaces.Text);
        body.Children.Clear();
        if (title is not null) body.AppendChild(title);

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

/// <summary>
/// 表示索引標記的資訊。
/// </summary>
/// <param name="term">索引詞彙</param>
/// <param name="key1">主要鍵值</param>
/// <param name="key2">次要鍵值</param>
public class OdfIndexMarkInfo(string term, string? key1, string? key2)
{
    /// <summary>
    /// 取得索引詞彙。
    /// </summary>
    public string Term { get; } = term;

    /// <summary>
    /// 取得主要鍵值。
    /// </summary>
    public string? Key1 { get; } = key1;

    /// <summary>
    /// 取得次要鍵值。
    /// </summary>
    public string? Key2 { get; } = key2;
}

/// <summary>
/// 用於建構索引項目範本的建立器。
/// </summary>
/// <param name="template">目標範本 OdfNode 節點</param>
public class OdfIndexTemplateBuilder(OdfNode template)
{
    private readonly OdfNode _template = template;

    /// <summary>
    /// 在範本中新增文字欄位項目。
    /// </summary>
    /// <returns>目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddText()
    {
        _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-text", OdfNamespaces.Text, "text"));
        return this;
    }

    /// <summary>
    /// 在範本中新增定位點項目。
    /// </summary>
    /// <param name="type">定位類型</param>
    /// <param name="leaderChar">前置字元</param>
    /// <returns>目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddTabStop(string type = "right", char leaderChar = '.')
    {
        var tab = OdfNodeFactory.CreateElement("index-entry-tab-stop", OdfNamespaces.Text, "text");
        tab.SetAttribute("type", OdfNamespaces.Style, type, "style");
        tab.SetAttribute("leader-char", OdfNamespaces.Style, leaderChar.ToString(), "style");
        _template.AppendChild(tab);
        return this;
    }

    /// <summary>
    /// 在範本中新增頁碼項目。
    /// </summary>
    /// <returns>目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddPageNumber()
    {
        _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-page-number", OdfNamespaces.Text, "text"));
        return this;
    }

    /// <summary>
    /// 在範本中新增自訂文字字串項目。
    /// </summary>
    /// <param name="text">自訂的文字內容</param>
    /// <returns>目前的建立器執行個體，以支援鏈結呼叫</returns>
    public OdfIndexTemplateBuilder AddSpan(string text)
    {
        var span = OdfNodeFactory.CreateElement("index-entry-span", OdfNamespaces.Text, "text");
        span.TextContent = text;
        _template.AppendChild(span);
        return this;
    }
}

/// <summary>
/// 表示 ODF 文件中的文獻目錄。
/// </summary>
public class OdfBibliography : OdfIndex
{
    /// <summary>
    /// 初始化 <see cref="OdfBibliography"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">文獻目錄的 OdfNode 節點</param>
    /// <param name="doc">所屬的文字文件</param>
    public OdfBibliography(OdfNode node, TextDocument doc) : base(node, doc)
    {
        FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
        FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
    }

    /// <summary>
    /// 取得文獻目錄來源節點的 XML 本地名稱。
    /// </summary>
    /// <returns>XML 本地名稱</returns>
    protected override string GetSourceLocalName() => "bibliography-source";

    /// <summary>
    /// 新增文獻目錄項目範本。
    /// </summary>
    /// <param name="bibType">文獻類型</param>
    /// <param name="styleName">樣式名稱</param>
    /// <returns>用於建構文獻範本的 <see cref="OdfBibliographyTemplateBuilder"/> 執行個體</returns>
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
    /// 更新文獻目錄的內容。
    /// </summary>
    public override void Update()
    {
        var body = FindChild(Node, "index-body", OdfNamespaces.Text);
        if (body is null) return;

        var title = FindChild(body, "index-title", OdfNamespaces.Text) ?? body.Children.FirstOrDefault(c => c.LocalName == "p" && c.NamespaceUri == OdfNamespaces.Text);
        body.Children.Clear();
        if (title is not null) body.AppendChild(title);

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
            if (bib.Metadata.TryGetValue("author", out var author)) text += $"{author}, ";
            if (bib.Metadata.TryGetValue("title", out var title)) text += $"\"{title}\"";
            if (bib.Metadata.TryGetValue("year", out var year)) text += $" ({year})";
            
            p.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
        }

        return p;
    }
}

/// <summary>
/// 表示文獻目錄標記的資訊。
/// </summary>
/// <param name="id">識別碼</param>
/// <param name="type">文獻類型</param>
/// <param name="meta">屬性詮釋資料字典</param>
public class OdfBibliographyMarkInfo(string id, string type, Dictionary<string, string> meta)
{
    /// <summary>
    /// 取得識別碼。
    /// </summary>
    public string Identifier { get; } = id;

    /// <summary>
    /// 取得文獻類型。
    /// </summary>
    public string Type { get; } = type;

    /// <summary>
    /// 取得屬性詮釋資料字典。
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = meta;
}

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

/// <summary>
/// 表示 ODF 文件中的字母索引標記。
/// </summary>
/// <param name="node">字母索引標記的 OdfNode 節點</param>
public class OdfAlphabeticalIndexMark(OdfNode node)
{
    /// <summary>
    /// 取得與此標記相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    /// <summary>
    /// 取得或設定此索引標記的字串值。
    /// </summary>
    public string StringValue
    {
        get => Node.GetAttribute("string-value", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("string-value", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// 取得或設定此索引標記的主要鍵值。
    /// </summary>
    public string? Key1
    {
        get => Node.GetAttribute("key1", OdfNamespaces.Text);
        set => Node.SetAttribute("key1", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定此索引標記的次要鍵值。
    /// </summary>
    public string? Key2
    {
        get => Node.GetAttribute("key2", OdfNamespaces.Text);
        set => Node.SetAttribute("key2", OdfNamespaces.Text, value ?? string.Empty, "text");
    }
}

/// <summary>
/// 表示 ODF 文件中的文獻標記。
/// </summary>
/// <param name="node">文獻標記 the OdfNode 節點</param>
public class OdfBibliographyMark(OdfNode node)
{
    /// <summary>
    /// 取得與此標記相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    /// <summary>
    /// 取得或設定此文獻標記的識別碼。
    /// </summary>
    public string Identifier
    {
        get => Node.GetAttribute("identifier", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("identifier", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的類型。
    /// </summary>
    public string BibliographyType
    {
        get => Node.GetAttribute("bibliography-type", OdfNamespaces.Text) ?? "book";
        set => Node.SetAttribute("bibliography-type", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的作者。
    /// </summary>
    public string? Author
    {
        get => Node.GetAttribute("author", OdfNamespaces.Text);
        set => Node.SetAttribute("author", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的標題。
    /// </summary>
    public string? Title
    {
        get => Node.GetAttribute("title", OdfNamespaces.Text);
        set => Node.SetAttribute("title", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的年份。
    /// </summary>
    public string? Year
    {
        get => Node.GetAttribute("year", OdfNamespaces.Text);
        set => Node.SetAttribute("year", OdfNamespaces.Text, value ?? string.Empty, "text");
    }
}

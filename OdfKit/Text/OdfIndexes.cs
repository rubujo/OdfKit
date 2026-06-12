#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text
{
    public abstract class OdfIndex
    {
        public OdfNode Node { get; }
        protected readonly TextDocument Doc;

        protected OdfIndex(OdfNode node, TextDocument doc)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public string Name
        {
            get => Node.GetAttribute("name", OdfNamespaces.Text) ?? string.Empty;
            set => Node.SetAttribute("name", OdfNamespaces.Text, value, "text");
        }

        public OdfNode? SourceNode => FindChild(Node, GetSourceLocalName(), OdfNamespaces.Text);
        public OdfNode? BodyNode => FindChild(Node, "index-body", OdfNamespaces.Text);

        protected abstract string GetSourceLocalName();

        protected OdfNode? FindChild(OdfNode parent, string localName, string ns)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            return null;
        }

        protected OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
        {
            var existing = FindChild(parent, localName, ns);
            if (existing != null) return existing;

            var child = OdfNodeFactory.CreateElement(localName, ns, prefix);
            parent.AppendChild(child);
            return child;
        }
        
        public abstract void Update();
    }

    public class OdfTableOfContents : OdfIndex
    {
        public OdfTableOfContents(OdfNode node, TextDocument doc) : base(node, doc)
        {
            FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
            FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
        }

        protected override string GetSourceLocalName() => "table-of-content-source";

        public int OutlineLevel
        {
            get => int.TryParse(SourceNode?.GetAttribute("outline-level", OdfNamespaces.Text), out var lvl) ? lvl : 10;
            set => SourceNode?.SetAttribute("outline-level", OdfNamespaces.Text, value.ToString(), "text");
        }

        public bool UseOutlineLevel
        {
            get => SourceNode?.GetAttribute("use-outline-level", OdfNamespaces.Text) != "false";
            set => SourceNode?.SetAttribute("use-outline-level", OdfNamespaces.Text, value ? "true" : "false", "text");
        }

        public bool UseIndexMarks
        {
            get => SourceNode?.GetAttribute("use-index-marks", OdfNamespaces.Text) == "true";
            set => SourceNode?.SetAttribute("use-index-marks", OdfNamespaces.Text, value ? "true" : "false", "text");
        }

        public override void Update()
        {
            var body = FindChild(Node, "index-body", OdfNamespaces.Text);
            if (body == null) return;

            var title = FindChild(body, "index-title", OdfNamespaces.Text) ?? body.Children.FirstOrDefault(c => c.LocalName == "p" && c.NamespaceUri == OdfNamespaces.Text);
            body.Children.Clear();
            if (title != null) body.AppendChild(title);

            var headings = new List<OdfHeadingInfo>();
            ScanHeadings(Doc.BodyTextRoot, headings);

            int maxLevel = OutlineLevel;
            var templates = new Dictionary<int, OdfNode>();
            var source = SourceNode;
            if (source != null)
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

        private class OdfHeadingInfo
        {
            public string Text { get; }
            public int Level { get; }
            public string Anchor { get; }

            public OdfHeadingInfo(string text, int level, string anchor)
            {
                Text = text;
                Level = level;
                Anchor = anchor;
            }
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
                if (refMark != null)
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

            if (template != null && template.Children.Count > 0)
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

    public class OdfAlphabeticalIndex : OdfIndex
    {
        public OdfAlphabeticalIndex(OdfNode node, TextDocument doc) : base(node, doc)
        {
            FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
            FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
        }

        protected override string GetSourceLocalName() => "alphabetical-index-source";

        public bool AlphabeticalSeparators
        {
            get => SourceNode?.GetAttribute("alphabetical-separators", OdfNamespaces.Text) == "true";
            set => SourceNode?.SetAttribute("alphabetical-separators", OdfNamespaces.Text, value ? "true" : "false", "text");
        }

        public bool CombineEntries
        {
            get => SourceNode?.GetAttribute("combine-entries", OdfNamespaces.Text) == "true";
            set => SourceNode?.SetAttribute("combine-entries", OdfNamespaces.Text, value ? "true" : "false", "text");
        }

        public bool IgnoreCase
        {
            get => SourceNode?.GetAttribute("ignore-case", OdfNamespaces.Text) == "true";
            set => SourceNode?.SetAttribute("ignore-case", OdfNamespaces.Text, value ? "true" : "false", "text");
        }

        public void ConfigureSource(bool commaSeparated = false, bool ignoreCase = false)
        {
            var src = FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
            src.SetAttribute("comma-separated", OdfNamespaces.Text, commaSeparated ? "true" : "false", "text");
            src.SetAttribute("ignore-case", OdfNamespaces.Text, ignoreCase ? "true" : "false", "text");
        }

        public OdfIndexTemplateBuilder AddEntryTemplate(string outlineLevel, string styleName)
        {
            var src = FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
            var template = OdfNodeFactory.CreateElement("alphabetical-index-entry-template", OdfNamespaces.Text, "text");
            template.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel, "text");
            template.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
            src.AppendChild(template);
            return new OdfIndexTemplateBuilder(template);
        }

        public override void Update()
        {
            var body = FindChild(Node, "index-body", OdfNamespaces.Text);
            if (body == null) return;

            var title = FindChild(body, "index-title", OdfNamespaces.Text) ?? body.Children.FirstOrDefault(c => c.LocalName == "p" && c.NamespaceUri == OdfNamespaces.Text);
            body.Children.Clear();
            if (title != null) body.AppendChild(title);

            var marks = new List<OdfIndexMarkInfo>();
            ScanIndexMarks(Doc.BodyTextRoot, marks);

            var comparer = IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var sorted = marks
                .OrderBy(m => m.Key1 ?? m.Term, comparer)
                .ThenBy(m => m.Key2 ?? string.Empty, comparer)
                .ThenBy(m => m.Term, comparer)
                .ToList();

            // Read templates
            var templates = new Dictionary<string, OdfNode>();
            var source = SourceNode;
            if (source != null)
            {
                foreach (var child in source.Children)
                {
                    if (child.LocalName == "alphabetical-index-entry-template" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        string? ol = child.GetAttribute("outline-level", OdfNamespaces.Text);
                        if (ol != null)
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

            if (template != null && template.Children.Count > 0)
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

    public class OdfIndexMarkInfo
    {
        public string Term { get; }
        public string? Key1 { get; }
        public string? Key2 { get; }
        
        public OdfIndexMarkInfo(string term, string? key1, string? key2)
        {
            Term = term;
            Key1 = key1;
            Key2 = key2;
        }
    }

    public class OdfIndexTemplateBuilder
    {
        private readonly OdfNode _template;
        public OdfIndexTemplateBuilder(OdfNode template) => _template = template;

        public OdfIndexTemplateBuilder AddText()
        {
            _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-text", OdfNamespaces.Text, "text"));
            return this;
        }

        public OdfIndexTemplateBuilder AddTabStop(string type = "right", char leaderChar = '.')
        {
            var tab = OdfNodeFactory.CreateElement("index-entry-tab-stop", OdfNamespaces.Text, "text");
            tab.SetAttribute("type", OdfNamespaces.Style, type, "style");
            tab.SetAttribute("leader-char", OdfNamespaces.Style, leaderChar.ToString(), "style");
            _template.AppendChild(tab);
            return this;
        }

        public OdfIndexTemplateBuilder AddPageNumber()
        {
            _template.AppendChild(OdfNodeFactory.CreateElement("index-entry-page-number", OdfNamespaces.Text, "text"));
            return this;
        }

        public OdfIndexTemplateBuilder AddSpan(string text)
        {
            var span = OdfNodeFactory.CreateElement("index-entry-span", OdfNamespaces.Text, "text");
            span.TextContent = text;
            _template.AppendChild(span);
            return this;
        }
    }

    public class OdfBibliography : OdfIndex
    {
        public OdfBibliography(OdfNode node, TextDocument doc) : base(node, doc)
        {
            FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
            FindOrCreateChild(Node, "index-body", OdfNamespaces.Text, "text");
        }

        protected override string GetSourceLocalName() => "bibliography-source";

        public OdfBibliographyTemplateBuilder AddEntryTemplate(string bibType, string styleName)
        {
            var src = FindOrCreateChild(Node, GetSourceLocalName(), OdfNamespaces.Text, "text");
            var template = OdfNodeFactory.CreateElement("bibliography-entry-template", OdfNamespaces.Text, "text");
            template.SetAttribute("bibliography-type", OdfNamespaces.Text, bibType, "text");
            template.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
            src.AppendChild(template);
            return new OdfBibliographyTemplateBuilder(template);
        }

        public override void Update()
        {
            var body = FindChild(Node, "index-body", OdfNamespaces.Text);
            if (body == null) return;

            var title = FindChild(body, "index-title", OdfNamespaces.Text) ?? body.Children.FirstOrDefault(c => c.LocalName == "p" && c.NamespaceUri == OdfNamespaces.Text);
            body.Children.Clear();
            if (title != null) body.AppendChild(title);

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

            // Default sorting: alphabetically by Identifier
            uniqueMarks = uniqueMarks.OrderBy(m => m.Identifier, StringComparer.OrdinalIgnoreCase).ToList();

            var templates = new Dictionary<string, OdfNode>();
            var source = SourceNode;
            if (source != null)
            {
                foreach (var child in source.Children)
                {
                    if (child.LocalName == "bibliography-entry-template" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        string? bt = child.GetAttribute("bibliography-type", OdfNamespaces.Text);
                        if (bt != null)
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

            if (template != null && template.Children.Count > 0)
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
                        if (field != null && bib.Metadata.TryGetValue(field, out var val))
                        {
                            var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = val };
                            p.AppendChild(textNode);
                        }
                    }
                }
            }
            else
            {
                // Fallback default format: [Identifier] Author, Title (Year)
                string text = $"[{bib.Identifier}] ";
                if (bib.Metadata.TryGetValue("author", out var author)) text += $"{author}, ";
                if (bib.Metadata.TryGetValue("title", out var title)) text += $"\"{title}\"";
                if (bib.Metadata.TryGetValue("year", out var year)) text += $" ({year})";
                
                p.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
            }

            return p;
        }
    }

    public class OdfBibliographyMarkInfo
    {
        public string Identifier { get; }
        public string Type { get; }
        public Dictionary<string, string> Metadata { get; }

        public OdfBibliographyMarkInfo(string id, string type, Dictionary<string, string> meta)
        {
            Identifier = id;
            Type = type;
            Metadata = meta;
        }
    }

    public class OdfBibliographyTemplateBuilder
    {
        private readonly OdfNode _template;
        public OdfBibliographyTemplateBuilder(OdfNode template) => _template = template;

        public OdfBibliographyTemplateBuilder AddSpan(string text)
        {
            var span = OdfNodeFactory.CreateElement("index-entry-span", OdfNamespaces.Text, "text");
            span.TextContent = text;
            _template.AppendChild(span);
            return this;
        }

        public OdfBibliographyTemplateBuilder AddBibliographyField(string dataField)
        {
            var field = OdfNodeFactory.CreateElement("index-entry-bibliography", OdfNamespaces.Text, "text");
            field.SetAttribute("bibliography-data-field", OdfNamespaces.Text, dataField, "text");
            _template.AppendChild(field);
            return this;
        }
    }

    public class OdfAlphabeticalIndexMark
    {
        public OdfNode Node { get; }

        public OdfAlphabeticalIndexMark(OdfNode node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public string StringValue
        {
            get => Node.GetAttribute("string-value", OdfNamespaces.Text) ?? string.Empty;
            set => Node.SetAttribute("string-value", OdfNamespaces.Text, value, "text");
        }

        public string? Key1
        {
            get => Node.GetAttribute("key1", OdfNamespaces.Text);
            set => Node.SetAttribute("key1", OdfNamespaces.Text, value ?? string.Empty, "text");
        }

        public string? Key2
        {
            get => Node.GetAttribute("key2", OdfNamespaces.Text);
            set => Node.SetAttribute("key2", OdfNamespaces.Text, value ?? string.Empty, "text");
        }
    }

    public class OdfBibliographyMark
    {
        public OdfNode Node { get; }

        public OdfBibliographyMark(OdfNode node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public string Identifier
        {
            get => Node.GetAttribute("identifier", OdfNamespaces.Text) ?? string.Empty;
            set => Node.SetAttribute("identifier", OdfNamespaces.Text, value, "text");
        }

        public string BibliographyType
        {
            get => Node.GetAttribute("bibliography-type", OdfNamespaces.Text) ?? "book";
            set => Node.SetAttribute("bibliography-type", OdfNamespaces.Text, value, "text");
        }

        public string? Author
        {
            get => Node.GetAttribute("author", OdfNamespaces.Text);
            set => Node.SetAttribute("author", OdfNamespaces.Text, value ?? string.Empty, "text");
        }

        public string? Title
        {
            get => Node.GetAttribute("title", OdfNamespaces.Text);
            set => Node.SetAttribute("title", OdfNamespaces.Text, value ?? string.Empty, "text");
        }

        public string? Year
        {
            get => Node.GetAttribute("year", OdfNamespaces.Text);
            set => Node.SetAttribute("year", OdfNamespaces.Text, value ?? string.Empty, "text");
        }
    }
}

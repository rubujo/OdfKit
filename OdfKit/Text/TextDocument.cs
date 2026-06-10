using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text
{
    public class TextDocument : OdfDocument
    {
        public OdfNode BodyTextRoot { get; private set; } = null!;

        public TextDocument(OdfPackage package) : base(package)
        {
            if (string.IsNullOrEmpty(package.MimeType))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
            }
            InitializeTextRoot();
        }

        private void InitializeTextRoot()
        {
            var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
            BodyTextRoot = FindOrCreateChild(body, "text", OdfNamespaces.Office, "office");
        }

        protected override string GetDefaultContentXml()
        {
            return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:body><office:text></office:text></office:body></office:document-content>";
        }

        protected override string GetDefaultStylesXml()
        {
            return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles></office:master-styles></office:document-styles>";
        }

        #region Paragraph Addition API

        public OdfParagraph AddParagraph(string text = "")
        {
            var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            BodyTextRoot.AppendChild(pNode);
            return new OdfParagraph(pNode, this);
        }

        #endregion

        #region Page Setup & Mirrored Layouts

        public OdfPageSetup GetDefaultPageSetup()
        {
            return new OdfPageSetup(this);
        }

        #endregion

        #region TOC (Table of Contents)

        public void AddTableOfContents()
        {
            var tocNode = new OdfNode(OdfNodeType.Element, "table-of-content", OdfNamespaces.Text, "text");
            tocNode.SetAttribute("name", OdfNamespaces.Text, "Table of Contents", "text");

            var sourceNode = new OdfNode(OdfNodeType.Element, "table-of-content-source", OdfNamespaces.Text, "text");
            sourceNode.SetAttribute("outline-level", OdfNamespaces.Text, "10", "text");
            tocNode.AppendChild(sourceNode);

            var bodyNode = new OdfNode(OdfNodeType.Element, "index-body", OdfNamespaces.Text, "text");
            
            var titlePara = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            titlePara.SetAttribute("style-name", OdfNamespaces.Text, "Contents_20_Heading", "text");
            titlePara.TextContent = "Table of Contents";
            bodyNode.AppendChild(titlePara);
            
            tocNode.AppendChild(bodyNode);
            BodyTextRoot.AppendChild(tocNode);

            SetUpdateFieldsWhenOpening(true);
        }

        private void SetUpdateFieldsWhenOpening(bool update)
        {
            var sc = FindOrCreateSettingsNode(SettingsDom, "view-settings");
            var map = FindOrCreateMapNode(sc, "Views");
            var entry = FindOrCreateMapEntryNode(map);
            var item = FindOrCreateConfigItemNode(entry, "UpdateFieldsWhenOpening", "boolean");
            item.TextContent = update ? "true" : "false";
        }

        #endregion

        #region Search & Replace with Actions/Regex

        public void ReplaceText(string search, string replacement, Action<OdfTextRun>? styleAction = null)
        {
            ReplaceTextRecursive(BodyTextRoot, search, replacement, styleAction);
        }

        public void ReplaceText(Regex regex, string replacement, Action<OdfTextRun>? styleAction = null)
        {
            ReplaceTextRegexRecursive(BodyTextRoot, regex, replacement, styleAction);
        }

        private void ReplaceTextRecursive(OdfNode node, string search, string replacement, Action<OdfTextRun>? styleAction)
        {
            NormalizeParagraphTextNodes(node);

            if (node.NodeType == OdfNodeType.Text)
            {
                string text = node.TextContent;
                if (text.Contains(search))
                {
                    if (styleAction != null && node.Parent != null)
                    {
                        int index = text.IndexOf(search);
                        var left = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(0, index) };
                        var mid = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                        var midRun = new OdfTextRun(mid, this);
                        midRun.Text = replacement;
                        styleAction(midRun);
                        
                        var right = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(index + search.Length) };

                        var parent = node.Parent;
                        parent.InsertBefore(left, node);
                        parent.InsertBefore(mid, node);
                        parent.InsertBefore(right, node);
                        parent.RemoveChild(node);
                    }
                    else
                    {
                        node.TextContent = text.Replace(search, replacement);
                    }
                }
                return;
            }

            if (node.LocalName == "annotation" && node.NamespaceUri == OdfNamespaces.Office)
            {
                foreach (var child in node.Children)
                {
                    if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        ReplaceTextRecursive(child, search, replacement, styleAction);
                    }
                }
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                ReplaceTextRecursive(node.Children[i], search, replacement, styleAction);
            }
        }

        private void ReplaceTextRegexRecursive(OdfNode node, Regex regex, string replacement, Action<OdfTextRun>? styleAction)
        {
            NormalizeParagraphTextNodes(node);

            if (node.NodeType == OdfNodeType.Text)
            {
                string text = node.TextContent;
                if (regex.IsMatch(text))
                {
                    if (styleAction != null && node.Parent != null)
                    {
                        var match = regex.Match(text);
                        int index = match.Index;
                        
                        var left = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(0, index) };
                        var mid = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                        var midRun = new OdfTextRun(mid, this);
                        midRun.Text = regex.Replace(match.Value, replacement);
                        styleAction(midRun);
                        
                        var right = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(index + match.Length) };

                        var parent = node.Parent;
                        parent.InsertBefore(left, node);
                        parent.InsertBefore(mid, node);
                        parent.InsertBefore(right, node);
                        parent.RemoveChild(node);
                    }
                    else
                    {
                        node.TextContent = regex.Replace(text, replacement);
                    }
                }
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                ReplaceTextRegexRecursive(node.Children[i], regex, replacement, styleAction);
            }
        }

        private void NormalizeParagraphTextNodes(OdfNode parent)
        {
            if (parent.LocalName == "p" && parent.NamespaceUri == OdfNamespaces.Text)
            {
                for (int i = parent.Children.Count - 2; i >= 0; i--)
                {
                    if (parent.Children[i].NodeType == OdfNodeType.Text && parent.Children[i + 1].NodeType == OdfNodeType.Text)
                    {
                        parent.Children[i].TextContent += parent.Children[i + 1].TextContent;
                        parent.RemoveChild(parent.Children[i + 1]);
                    }
                }
            }
        }

        #endregion

        #region MailMerge Implementation

        public void MailMerge(object dataSource)
        {
            var engine = new OdfMailMergeEngine(this);
            engine.Execute(BodyTextRoot, dataSource);
        }

        #endregion

        #region Mathematical Formulas (MathML)

        public void AddFormula(OdfParagraph paragraph, string mathMlXmlString)
        {
            if (paragraph == null) throw new ArgumentNullException(nameof(paragraph));
            if (string.IsNullOrWhiteSpace(mathMlXmlString)) throw new ArgumentException("MathML XML content cannot be empty.", nameof(mathMlXmlString));

            string folder = $"Formula_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            string mathDocXml = $"<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:math=\"http://www.w3.org/1998/Math/MathML\"><office:body><office:formula>{mathMlXmlString}</office:formula></office:body></office:document-meta>";

            Package.WriteEntry($"{folder}/content.xml", System.Text.Encoding.UTF8.GetBytes(mathDocXml), "text/xml");
            Package.WriteEntry($"{folder}/mimetype", System.Text.Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), "application/vnd.oasis.opendocument.formula");
            
            // Disable compression for mimetype
            if (Package.HasEntry($"{folder}/mimetype"))
            {
                // Access internal entries? Wait, mimetype typically is uncompressed, but it's okay as package.SaveManifestToEntries handles manifest.
            }

            Package.SaveManifestToEntries();

            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            frame.SetAttribute("width", OdfNamespaces.Svg, "2cm", "svg");
            frame.SetAttribute("height", OdfNamespaces.Svg, "1cm", "svg");
            frame.SetAttribute("anchor-type", OdfNamespaces.Text, "as-char", "text");

            var obj = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
            obj.SetAttribute("href", OdfNamespaces.XLink, folder, "xlink");
            frame.AppendChild(obj);

            paragraph.Node.AppendChild(frame);
        }

        #endregion

        #region Comments / Annotations

        public void AddComment(OdfParagraph paragraph, OdfComment comment)
        {
            if (paragraph == null) throw new ArgumentNullException(nameof(paragraph));
            if (comment == null) throw new ArgumentNullException(nameof(comment));

            var node = comment.ToXmlNode();
            if (node.LocalName == "annotation-list")
            {
                foreach (var child in new List<OdfNode>(node.Children))
                {
                    paragraph.Node.AppendChild(child);
                }
            }
            else
            {
                paragraph.Node.AppendChild(node);
            }
        }

        public List<OdfComment> GetComments()
        {
            var list = new List<OdfComment>();
            FindCommentsRecursive(BodyTextRoot, list);
            return list;
        }

        private void FindCommentsRecursive(OdfNode node, List<OdfComment> list)
        {
            if (node.LocalName == "annotation" && node.NamespaceUri == OdfNamespaces.Office)
            {
                // Check if it's a top-level comment (no annotation-parent)
                string? parent = node.GetAttribute("annotation-parent", OdfNamespaces.Office);
                if (string.IsNullOrEmpty(parent))
                {
                    try
                    {
                        list.Add(OdfComment.FromXmlNode(node));
                    }
                    catch (Exception ex)
                    {
                        OdfKitDiagnostics.Warn($"Failed to parse comment node: {ex.Message}");
                    }
                }
            }

            foreach (var child in node.Children)
            {
                FindCommentsRecursive(child, list);
            }
        }

        #endregion

        #region Dynamic Page / Field Indicators

        public void AddPageNumberField(OdfParagraph paragraph)
        {
            var fNode = new OdfNode(OdfNodeType.Element, "page-number", OdfNamespaces.Text, "text");
            fNode.SetAttribute("select-page", OdfNamespaces.Text, "current", "text");
            paragraph.Node.AppendChild(fNode);
        }

        public void AddPageCountField(OdfParagraph paragraph)
        {
            var fNode = new OdfNode(OdfNodeType.Element, "page-count", OdfNamespaces.Text, "text");
            fNode.SetAttribute("num-format", OdfNamespaces.Style, "1", "style");
            paragraph.Node.AppendChild(fNode);
        }

        #endregion

        #region Multi-Column Sections Layouts

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

        #endregion

        #region Tracked Changes (AcceptAll)

        public bool TrackedChanges { get; set; }

        public void AcceptAllTrackedChanges()
        {
            var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
            if (tcNode == null) return;

            var changes = new Dictionary<string, string>(StringComparer.Ordinal);
            ExtractTrackedChangesMeta(tcNode, changes);

            foreach (var kvp in changes)
            {
                if (kvp.Value == "deletion")
                {
                    var purger = new ChangePurger(kvp.Key);
                    purger.Purge(BodyTextRoot);
                }
            }

            CleanupRemainingChangeMarkers(BodyTextRoot);

            BodyTextRoot.RemoveChild(tcNode);
        }

        private void ExtractTrackedChangesMeta(OdfNode tcNode, Dictionary<string, string> changes)
        {
            foreach (var changedRegion in tcNode.Children)
            {
                string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
                if (string.IsNullOrEmpty(id)) continue;

                foreach (var spec in changedRegion.Children)
                {
                    if (spec.LocalName == "insertion" && spec.NamespaceUri == OdfNamespaces.Text)
                    {
                        changes[id!] = "insertion";
                    }
                    else if (spec.LocalName == "deletion" && spec.NamespaceUri == OdfNamespaces.Text)
                    {
                        changes[id!] = "deletion";
                    }
                }
            }
        }

        private void CleanupRemainingChangeMarkers(OdfNode node)
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                if ((child.LocalName == "change-start" || child.LocalName == "change-end") && child.NamespaceUri == OdfNamespaces.Text)
                {
                    node.RemoveChild(child);
                }
                else
                {
                    CleanupRemainingChangeMarkers(child);
                }
            }
        }

        private class ChangePurger
        {
            private readonly string _targetId;
            private bool _foundStart = false;
            private bool _foundEnd = false;

            public ChangePurger(string targetId)
            {
                _targetId = targetId;
            }

            public void Purge(OdfNode node)
            {
                for (int i = node.Children.Count - 1; i >= 0; i--)
                {
                    var child = node.Children[i];

                    bool isEnd = (child.LocalName == "change-end" && child.NamespaceUri == OdfNamespaces.Text && child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId);
                    bool isStart = (child.LocalName == "change-start" && child.NamespaceUri == OdfNamespaces.Text && child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId);

                    if (isEnd)
                    {
                        _foundEnd = true;
                        node.RemoveChild(child);
                        continue;
                    }

                    if (isStart)
                    {
                        _foundStart = true;
                        node.RemoveChild(child);
                        continue;
                    }

                    bool wasEndFoundBefore = _foundEnd;
                    bool wasStartFoundBefore = _foundStart;

                    Purge(child);

                    bool containedEnd = (!wasEndFoundBefore && _foundEnd);
                    bool containedStart = (!wasStartFoundBefore && _foundStart);

                    if (_foundEnd && !_foundStart && !containedEnd)
                    {
                        node.RemoveChild(child);
                    }
                }
            }
        }

        #endregion

        #region HTML Fragment Parsing

        private class SpanState
        {
            public bool? Bold { get; set; }
            public bool? Italic { get; set; }
            public bool? Underline { get; set; }
        }

        public void AddHtmlFragment(OdfParagraph paragraph, string html)
        {
            if (paragraph == null) throw new ArgumentNullException(nameof(paragraph));
            if (string.IsNullOrWhiteSpace(html)) return;

            // First, strip all HTML comments
            html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");

            // Pre-filter script and style blocks entirely, including their content
            html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*?)<\/\1\s*>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<(script|style)\b[^>]*\/>", "", RegexOptions.IgnoreCase);

            // Strip unclosed script/style blocks extending to the end of input
            html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*)$", "", RegexOptions.IgnoreCase);

            var tokenRegex = new Regex(@"(<!--[\s\S]*?-->|</?[a-zA-Z][^>]*>|[^<]+|<)", RegexOptions.Compiled);
            var matches = tokenRegex.Matches(html);

            bool isBold = false;
            bool isItalic = false;
            bool isUnderline = false;
            string? currentHref = null;
            var spanStack = new List<SpanState>();
            bool inScriptOrStyle = false;

            foreach (Match match in matches)
            {
                string text = match.Value;
                bool isTag = false;
                bool isClosing = false;
                string tagName = "";

                if (text.StartsWith("<") && !text.StartsWith("<!--"))
                {
                    var tagMatch = Regex.Match(text, @"^<\s*(/?)\s*([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (tagMatch.Success)
                    {
                        isTag = true;
                        isClosing = tagMatch.Groups[1].Value == "/";
                        tagName = tagMatch.Groups[2].Value.ToLowerInvariant();
                    }
                }

                if (isTag)
                {
                    if (tagName == "script" || tagName == "style")
                    {
                        bool isSelfClosing = text.EndsWith("/>");
                        if (!isSelfClosing)
                        {
                            inScriptOrStyle = !isClosing;
                        }
                        continue;
                    }

                    if (inScriptOrStyle)
                    {
                        continue;
                    }

                    if (tagName == "b" || tagName == "strong")
                    {
                        isBold = !isClosing;
                    }
                    else if (tagName == "i" || tagName == "em")
                    {
                        isItalic = !isClosing;
                    }
                    else if (tagName == "u")
                    {
                        isUnderline = !isClosing;
                    }
                    else if (tagName == "br")
                    {
                        if (!isClosing)
                        {
                            paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                        }
                    }
                    else if (tagName == "a")
                    {
                        if (isClosing)
                        {
                            currentHref = null;
                        }
                        else
                        {
                            var hrefMatch = Regex.Match(text, @"href\s*=\s*['""]?([^'""\s>]+)['""]?", RegexOptions.IgnoreCase);
                            if (hrefMatch.Success)
                            {
                                currentHref = hrefMatch.Groups[1].Value;
                            }
                        }
                    }
                    else if (tagName == "span")
                    {
                        if (isClosing)
                        {
                            if (spanStack.Count > 0)
                            {
                                spanStack.RemoveAt(spanStack.Count - 1);
                            }
                        }
                        else
                        {
                            bool? styleBold = null;
                            bool? styleItalic = null;
                            bool? styleUnderline = null;

                            var styleMatch = Regex.Match(text, @"style\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
                            if (styleMatch.Success)
                            {
                                string styleStr = styleMatch.Groups[1].Success ? styleMatch.Groups[1].Value : styleMatch.Groups[2].Value;
                                
                                var boldMatch = Regex.Match(styleStr, @"font-weight\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                                if (boldMatch.Success)
                                {
                                    string val = boldMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                    if (val == "bold" || val == "700" || val == "800" || val == "900")
                                    {
                                        styleBold = true;
                                    }
                                    else if (val == "normal" || val == "400")
                                    {
                                        styleBold = false;
                                    }
                                }

                                var italicMatch = Regex.Match(styleStr, @"font-style\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                                if (italicMatch.Success)
                                {
                                    string val = italicMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                    if (val == "italic" || val == "oblique")
                                    {
                                        styleItalic = true;
                                    }
                                    else if (val == "normal")
                                    {
                                        styleItalic = false;
                                    }
                                }

                                var underlineMatch = Regex.Match(styleStr, @"text-decoration\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                                if (underlineMatch.Success)
                                {
                                    string val = underlineMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                    if (val == "underline")
                                    {
                                        styleUnderline = true;
                                    }
                                    else if (val == "none")
                                    {
                                        styleUnderline = false;
                                    }
                                }
                            }

                            spanStack.Add(new SpanState { Bold = styleBold, Italic = styleItalic, Underline = styleUnderline });
                        }
                    }
                }
                else
                {
                    if (inScriptOrStyle)
                    {
                        continue;
                    }

                    string decodedText = DecodeHtmlEntities(text);

                    bool activeBold = isBold;
                    bool activeItalic = isItalic;
                    bool activeUnderline = isUnderline;

                    foreach (var state in spanStack)
                    {
                        if (state.Bold.HasValue) activeBold = state.Bold.Value;
                        if (state.Italic.HasValue) activeItalic = state.Italic.Value;
                        if (state.Underline.HasValue) activeUnderline = state.Underline.Value;
                    }

                    if (currentHref != null)
                    {
                        var aNode = new OdfNode(OdfNodeType.Element, "a", OdfNamespaces.Text, "text");
                        aNode.SetAttribute("href", OdfNamespaces.XLink, currentHref, "xlink");
                        if (activeBold || activeItalic || activeUnderline)
                        {
                            var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                            var run = new OdfTextRun(span, this) { Text = decodedText, IsBold = activeBold, IsItalic = activeItalic, IsUnderline = activeUnderline };
                            aNode.AppendChild(span);
                        }
                        else
                        {
                            aNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = decodedText });
                        }
                        paragraph.Node.AppendChild(aNode);
                    }
                    else
                    {
                        if (activeBold || activeItalic || activeUnderline)
                        {
                            var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                            var run = new OdfTextRun(span, this) { Text = decodedText, IsBold = activeBold, IsItalic = activeItalic, IsUnderline = activeUnderline };
                            paragraph.Node.AppendChild(span);
                        }
                        else
                        {
                            var lastChild = paragraph.Node.Children.Count > 0 ? paragraph.Node.Children[paragraph.Node.Children.Count - 1] : null;
                            if (lastChild != null && lastChild.NodeType == OdfNodeType.Text)
                            {
                                lastChild.TextContent += decodedText;
                            }
                            else
                            {
                                paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = decodedText });
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Table covered cells omissions

        public OdfTable AddTable(int rows, int cols)
        {
            var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
            BodyTextRoot.AppendChild(table);
            return new OdfTable(table, rows, cols, this);
        }

        #endregion

        #region Document Merging Logic Override

        protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            var srcText = sourceDoc as TextDocument ?? throw new ArgumentException("Source document must be a TextDocument.");
            
            foreach (var child in srcText.BodyTextRoot.Children)
            {
                if (child.NodeType == OdfNodeType.Element)
                {
                    var imported = OdfNode.ImportNode(child, srcText.Package, Package);
                    RemapStylesInNodes(imported, renameMap);
                    BodyTextRoot.AppendChild(imported);
                }
            }
        }

        #endregion

        #region XML Helper

        private OdfNode? FindChild(OdfNode parent, string localName, string ns)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            return null;
        }

        private static string DecodeHtmlEntities(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string decoded = System.Net.WebUtility.HtmlDecode(text);
            if (decoded.Contains("&apos;"))
            {
                decoded = decoded.Replace("&apos;", "'");
            }
            if (decoded.Contains("&APOS;"))
            {
                decoded = decoded.Replace("&APOS;", "'");
            }
            return decoded;
        }

        #endregion
    }

    public class OdfPageSetup
    {
        private readonly TextDocument _doc;

        public OdfPageSetup(TextDocument doc)
        {
            _doc = doc;
        }

        public double PageWidth
        {
            get
            {
                string? val = GetPageProp("page-width");
                if (val != null && val.EndsWith("cm") && double.TryParse(val.Substring(0, val.Length - 2), out var d))
                    return d;
                return 21.0;
            }
            set => SetPageProp("page-width", $"{value}cm");
        }

        public double PageHeight
        {
            get
            {
                string? val = GetPageProp("page-height");
                if (val != null && val.EndsWith("cm") && double.TryParse(val.Substring(0, val.Length - 2), out var d))
                    return d;
                return 29.7;
            }
            set => SetPageProp("page-height", $"{value}cm");
        }

        public string? PageUsage
        {
            get
            {
                var props = FindOrCreatePageLayoutProperties();
                return props.GetAttribute("page-usage", OdfNamespaces.Style);
            }
            set
            {
                var props = FindOrCreatePageLayoutProperties();
                props.SetAttribute("page-usage", OdfNamespaces.Style, value ?? "all", "style");
            }
        }

        public string? HeaderText
        {
            get => GetHeaderFooterText("header");
            set => SetHeaderFooterText("header", value);
        }

        public string? HeaderLeftText
        {
            get => GetHeaderFooterText("header-left");
            set => SetHeaderFooterText("header-left", value);
        }

        public string? FooterText
        {
            get => GetHeaderFooterText("footer");
            set => SetHeaderFooterText("footer", value);
        }

        public string? FooterLeftText
        {
            get => GetHeaderFooterText("footer-left");
            set => SetHeaderFooterText("footer-left", value);
        }

        private string? GetPageProp(string name)
        {
            var props = FindOrCreatePageLayoutProperties();
            return props.GetAttribute(name, OdfNamespaces.Fo);
        }

        private void SetPageProp(string name, string val)
        {
            var props = FindOrCreatePageLayoutProperties();
            props.SetAttribute(name, OdfNamespaces.Fo, val, "fo");
        }

        private OdfNode FindOrCreatePageLayoutNode()
        {
            var autoStyles = FindOrCreateChild(_doc.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
            foreach (var child in autoStyles.Children)
            {
                if (child.LocalName == "page-layout" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    return child;
                }
            }
            var pageLayout = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
            pageLayout.SetAttribute("name", OdfNamespaces.Style, "Mpm1", "style");
            autoStyles.AppendChild(pageLayout);
            return pageLayout;
        }

        private OdfNode FindOrCreatePageLayoutProperties()
        {
            var layoutNode = FindOrCreatePageLayoutNode();
            foreach (var child in layoutNode.Children)
            {
                if (child.LocalName == "page-layout-properties" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    return child;
                }
            }
            var props = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
            layoutNode.AppendChild(props);
            return props;
        }

        private OdfNode FindOrCreateMasterPage()
        {
            var masterStyles = FindOrCreateChild(_doc.StylesDom, "master-styles", OdfNamespaces.Office, "office");
            foreach (var child in masterStyles.Children)
            {
                if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    return child;
                }
            }
            var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
            masterPage.SetAttribute("name", OdfNamespaces.Style, "Standard", "style");
            masterPage.SetAttribute("page-layout-name", OdfNamespaces.Style, "Mpm1", "style");
            masterStyles.AppendChild(masterPage);
            return masterPage;
        }

        private string? GetHeaderFooterText(string localName)
        {
            var mp = FindOrCreateMasterPage();
            foreach (var child in mp.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
                {
                    foreach (var p in child.Children)
                    {
                        if (p.LocalName == "p" && p.NamespaceUri == OdfNamespaces.Text)
                        {
                            return p.TextContent;
                        }
                    }
                }
            }
            return null;
        }

        private void SetHeaderFooterText(string localName, string? value)
        {
            var mp = FindOrCreateMasterPage();
            OdfNode? target = null;
            foreach (var child in mp.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
                {
                    target = child;
                    break;
                }
            }

            if (value == null)
            {
                if (target != null) mp.RemoveChild(target);
            }
            else
            {
                if (target == null)
                {
                    target = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Style, "style");
                    mp.AppendChild(target);
                }
                
                OdfNode? pNode = null;
                foreach (var child in target.Children)
                {
                    if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        pNode = child;
                        break;
                    }
                }
                if (pNode == null)
                {
                    pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                    target.AppendChild(pNode);
                }
                pNode.TextContent = value;
            }
        }

        private OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
            parent.AppendChild(node);
            return node;
        }
    }

    public class OdfParagraph
    {
        public OdfNode Node { get; }
        private readonly TextDocument _doc;

        public OdfParagraph(OdfNode node, TextDocument doc)
        {
            Node = node;
            _doc = doc;
        }

        public string TextContent
        {
            get => Node.TextContent;
            set => Node.TextContent = value;
        }
    }

    public class OdfTextRun
    {
        public OdfNode Node { get; }
        private readonly TextDocument _doc;

        public OdfTextRun(OdfNode node, TextDocument doc)
        {
            Node = node;
            _doc = doc;
        }

        public string Text
        {
            get => Node.TextContent;
            set => Node.TextContent = value;
        }

        public bool IsBold
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-weight", OdfNamespaces.Fo, "text") == "bold";
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-weight", OdfNamespaces.Fo, value ? "bold" : "normal", "fo");
        }

        public bool IsItalic
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-style", OdfNamespaces.Fo, "text") == "italic";
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-style", OdfNamespaces.Fo, value ? "italic" : "normal", "fo");
        }

        public bool IsUnderline
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-underline-style", OdfNamespaces.Style, "text") == "solid";
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
        }

        private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
    }

    public class OdfSection
    {
        public OdfNode Node { get; }
        private readonly TextDocument _doc;

        public OdfSection(OdfNode node, TextDocument doc)
        {
            Node = node;
            _doc = doc;
        }
    }

    public class OdfTable
    {
        public OdfNode Node { get; }
        private readonly TextDocument _doc;
        private readonly int _rows;
        private readonly int _cols;

        public OdfTable(OdfNode node, int rows, int cols, TextDocument doc)
        {
            Node = node;
            _rows = rows;
            _cols = cols;
            _doc = doc;
            BuildGrid();
        }

        private void BuildGrid()
        {
            for (int r = 0; r < _rows; r++)
            {
                var rNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
                for (int c = 0; c < _cols; c++)
                {
                    var cNode = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                    rNode.AppendChild(cNode);
                }
                Node.AppendChild(rNode);
            }
        }

        public void MergeCells(int startRow, int startCol, int rowSpan, int colSpan)
        {
            var rows = new List<OdfNode>();
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    rows.Add(child);
                }
            }

            var targetRowNode = rows[startRow];
            var cellsInTargetRow = new List<OdfNode>();
            foreach (var child in targetRowNode.Children)
            {
                if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                {
                    cellsInTargetRow.Add(child);
                }
            }
            var targetCell = cellsInTargetRow[startCol];
            targetCell.SetAttribute("number-rows-spanned", OdfNamespaces.Table, rowSpan.ToString(), "table");
            targetCell.SetAttribute("number-columns-spanned", OdfNamespaces.Table, colSpan.ToString(), "table");

            for (int r = startRow; r < startRow + rowSpan; r++)
            {
                var rowNode = rows[r];
                var cellsInRow = new List<OdfNode>();
                foreach (var child in rowNode.Children)
                {
                    if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                    {
                        cellsInRow.Add(child);
                    }
                }

                for (int c = startCol; c < startCol + colSpan; c++)
                {
                    if (r == startRow && c == startCol) continue;
                    
                    var cellToRemove = cellsInRow[c];
                    var coveredNode = new OdfNode(OdfNodeType.Element, "covered-table-cell", OdfNamespaces.Table, "table");
                    rowNode.InsertBefore(coveredNode, cellToRemove);
                    rowNode.RemoveChild(cellToRemove);
                }
            }
        }
    }
}

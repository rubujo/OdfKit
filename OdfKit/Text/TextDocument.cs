using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
            StyleEngine.OnStyleChanging = TrackFormatChange;
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
            return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles></office:document-styles>";
        }

        #region Document Elements Addition API

        public OdfParagraph AddParagraph(string text = "")
        {
            var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            if (TrackedChanges)
            {
                string changeId = RecordTrackedChange("insertion");
                var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
                startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
                var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
                endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
                
                BodyTextRoot.AppendChild(startNode);
                BodyTextRoot.AppendChild(pNode);
                BodyTextRoot.AppendChild(endNode);
            }
            else
            {
                BodyTextRoot.AppendChild(pNode);
            }
            return new OdfParagraph(pNode, this);
        }

        public OdfHeading AddHeading(string text, int outlineLevel)
        {
            var hNode = OdfNodeFactory.CreateElement("h", OdfNamespaces.Text, "text");
            hNode.TextContent = text;
            hNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
            if (TrackedChanges)
            {
                string changeId = RecordTrackedChange("insertion");
                var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
                startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
                var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
                endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
                
                BodyTextRoot.AppendChild(startNode);
                BodyTextRoot.AppendChild(hNode);
                BodyTextRoot.AppendChild(endNode);
            }
            else
            {
                BodyTextRoot.AppendChild(hNode);
            }
            return new OdfHeading(hNode, this);
        }

        public OdfList AddList(string? styleName = null)
        {
            var listNode = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
            if (styleName != null)
            {
                listNode.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
            }
            BodyTextRoot.AppendChild(listNode);
            return new OdfList(listNode, this);
        }

        public void AddDateField(OdfParagraph paragraph)
        {
            var fNode = OdfNodeFactory.CreateElement("date", OdfNamespaces.Text, "text");
            paragraph.Node.AppendChild(fNode);
        }

        public void AddTimeField(OdfParagraph paragraph)
        {
            var fNode = OdfNodeFactory.CreateElement("time", OdfNamespaces.Text, "text");
            paragraph.Node.AppendChild(fNode);
        }

        public void AddAuthorField(OdfParagraph paragraph)
        {
            var fNode = OdfNodeFactory.CreateElement("author-name", OdfNamespaces.Text, "text");
            paragraph.Node.AppendChild(fNode);
        }

        public void AddChapterField(OdfParagraph paragraph)
        {
            var fNode = OdfNodeFactory.CreateElement("chapter", OdfNamespaces.Text, "text");
            paragraph.Node.AppendChild(fNode);
        }

        public void AddSequenceField(OdfParagraph paragraph, string name, string numFormat = "1")
        {
            var fNode = OdfNodeFactory.CreateElement("sequence", OdfNamespaces.Text, "text");
            fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
            fNode.SetAttribute("num-format", OdfNamespaces.Style, numFormat, "style");
            paragraph.Node.AppendChild(fNode);
        }

        public void AddReferenceField(OdfParagraph paragraph, string refName)
        {
            var fNode = OdfNodeFactory.CreateElement("reference-ref", OdfNamespaces.Text, "text");
            fNode.SetAttribute("ref-name", OdfNamespaces.Text, refName, "text");
            paragraph.Node.AppendChild(fNode);
        }

        public void AddVariableSetField(OdfParagraph paragraph, string name, string value)
        {
            var fNode = OdfNodeFactory.CreateElement("variable-set", OdfNamespaces.Text, "text");
            fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
            fNode.TextContent = value;
            paragraph.Node.AppendChild(fNode);
        }

        public void AddVariableGetField(OdfParagraph paragraph, string name)
        {
            var fNode = OdfNodeFactory.CreateElement("variable-get", OdfNamespaces.Text, "text");
            fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
            paragraph.Node.AppendChild(fNode);
        }

        public OdfAlphabeticalIndex AddAlphabeticalIndex(string title = "Alphabetical Index")
        {
            var idxNode = OdfNodeFactory.CreateElement("alphabetical-index", OdfNamespaces.Text, "text");
            idxNode.SetAttribute("name", OdfNamespaces.Text, title, "text");
            
            var sourceNode = OdfNodeFactory.CreateElement("alphabetical-index-source", OdfNamespaces.Text, "text");
            idxNode.AppendChild(sourceNode);

            var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
            var titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            titlePara.TextContent = title;
            bodyNode.AppendChild(titlePara);
            idxNode.AppendChild(bodyNode);

            BodyTextRoot.AppendChild(idxNode);
            SetUpdateFieldsWhenOpening(true);
            return new OdfAlphabeticalIndex(idxNode, this);
        }

        public OdfBibliography AddBibliography(string title = "Bibliography")
        {
            var bibNode = OdfNodeFactory.CreateElement("bibliography", OdfNamespaces.Text, "text");
            bibNode.SetAttribute("name", OdfNamespaces.Text, title, "text");
            
            var sourceNode = OdfNodeFactory.CreateElement("bibliography-source", OdfNamespaces.Text, "text");
            bibNode.AppendChild(sourceNode);

            var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
            var titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            titlePara.TextContent = title;
            bodyNode.AppendChild(titlePara);
            bibNode.AppendChild(bodyNode);

            BodyTextRoot.AppendChild(bibNode);
            SetUpdateFieldsWhenOpening(true);
            return new OdfBibliography(bibNode, this);
        }

        public List<OdfIndex> GetIndexes()
        {
            var list = new List<OdfIndex>();
            FindIndexesRecursive(BodyTextRoot, list);
            return list;
        }

        private void FindIndexesRecursive(OdfNode node, List<OdfIndex> list)
        {
            if (node.NamespaceUri == OdfNamespaces.Text)
            {
                if (node.LocalName == "table-of-content")
                    list.Add(new OdfTableOfContents(node, this));
                else if (node.LocalName == "alphabetical-index")
                    list.Add(new OdfAlphabeticalIndex(node, this));
                else if (node.LocalName == "bibliography")
                    list.Add(new OdfBibliography(node, this));
            }
            foreach (var child in node.Children)
            {
                FindIndexesRecursive(child, list);
            }
        }

        public OdfAlphabeticalIndexMark AddAlphabeticalIndexMark(OdfParagraph paragraph, string stringValue, string? key1 = null, string? key2 = null)
        {
            var markNode = OdfNodeFactory.CreateElement("alphabetical-index-mark", OdfNamespaces.Text, "text");
            markNode.SetAttribute("string-value", OdfNamespaces.Text, stringValue, "text");
            if (key1 != null) markNode.SetAttribute("key1", OdfNamespaces.Text, key1, "text");
            if (key2 != null) markNode.SetAttribute("key2", OdfNamespaces.Text, key2, "text");
            
            paragraph.Node.AppendChild(markNode);
            return new OdfAlphabeticalIndexMark(markNode);
        }

        public OdfBibliographyMark AddBibliographyMark(OdfParagraph paragraph, string identifier, string bibliographyType, string author, string title, string year)
        {
            var markNode = OdfNodeFactory.CreateElement("bibliography-mark", OdfNamespaces.Text, "text");
            markNode.SetAttribute("identifier", OdfNamespaces.Text, identifier, "text");
            markNode.SetAttribute("bibliography-type", OdfNamespaces.Text, bibliographyType, "text");
            markNode.SetAttribute("author", OdfNamespaces.Text, author, "text");
            markNode.SetAttribute("title", OdfNamespaces.Text, title, "text");
            markNode.SetAttribute("year", OdfNamespaces.Text, year, "text");

            paragraph.Node.AppendChild(markNode);
            return new OdfBibliographyMark(markNode);
        }

        public void AddTableIndex()
        {
            var idxNode = OdfNodeFactory.CreateElement("table-index", OdfNamespaces.Text, "text");
            idxNode.SetAttribute("name", OdfNamespaces.Text, "Index of Tables", "text");
            var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
            idxNode.AppendChild(bodyNode);
            BodyTextRoot.AppendChild(idxNode);
            SetUpdateFieldsWhenOpening(true);
        }

        public void AddCommentStart(OdfParagraph paragraph, string name)
        {
            var startNode = OdfNodeFactory.CreateElement("annotation-start", OdfNamespaces.Office, "office");
            startNode.SetAttribute("name", OdfNamespaces.Office, name, "office");
            paragraph.Node.AppendChild(startNode);
        }

        public void AddCommentEnd(OdfParagraph paragraph, string name)
        {
            var endNode = OdfNodeFactory.CreateElement("annotation-end", OdfNamespaces.Office, "office");
            endNode.SetAttribute("name", OdfNamespaces.Office, name, "office");
            paragraph.Node.AppendChild(endNode);
        }

        public void AddBookmark(OdfParagraph paragraph, string name)
        {
            var bNode = OdfNodeFactory.CreateElement("bookmark", OdfNamespaces.Text, "text");
            bNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
            paragraph.Node.AppendChild(bNode);
        }

        public void AddReferenceMark(OdfParagraph paragraph, string name)
        {
            var rNode = OdfNodeFactory.CreateElement("reference-mark", OdfNamespaces.Text, "text");
            rNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
            paragraph.Node.AppendChild(rNode);
        }

        public void AddHyperlink(OdfParagraph paragraph, string url, string text)
        {
            var aNode = OdfNodeFactory.CreateElement("a", OdfNamespaces.Text, "text");
            aNode.SetAttribute("href", OdfNamespaces.XLink, url, "xlink");
            aNode.TextContent = text;
            paragraph.Node.AppendChild(aNode);
        }

        public OdfImage AddImage(OdfParagraph paragraph, string packagePath, string widthCm, string heightCm, string? name = null)
        {
            var frameNode = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
            if (name != null)
            {
                frameNode.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
            }
            frameNode.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
            frameNode.SetAttribute("width", OdfNamespaces.Svg, widthCm, "svg");
            frameNode.SetAttribute("height", OdfNamespaces.Svg, heightCm, "svg");

            var imageNode = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
            imageNode.SetAttribute("href", OdfNamespaces.XLink, packagePath, "xlink");
            imageNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            imageNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
            imageNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

            frameNode.AppendChild(imageNode);
            paragraph.Node.AppendChild(frameNode);

            return new OdfImage(frameNode, imageNode);
        }

        public OdfRuby AddRuby(OdfParagraph paragraph, string baseText, string rubyText)
        {
            var rubyNode = OdfNodeFactory.CreateElement("ruby", OdfNamespaces.Text, "text");
            
            var baseNode = OdfNodeFactory.CreateElement("ruby-base", OdfNamespaces.Text, "text");
            baseNode.TextContent = baseText;
            rubyNode.AppendChild(baseNode);

            var textNode = OdfNodeFactory.CreateElement("ruby-text", OdfNamespaces.Text, "text");
            textNode.TextContent = rubyText;
            rubyNode.AppendChild(textNode);

            paragraph.Node.AppendChild(rubyNode);
            return new OdfRuby(rubyNode, this);
        }

        #endregion

        #region Page Setup & Mirrored Layouts

        public OdfPageSetup GetDefaultPageSetup()
        {
            return new OdfPageSetup(this);
        }

        #endregion

        #region TOC (Table of Contents)

        public OdfTableOfContents AddTableOfContents(string title = "Table of Contents", int outlineLevel = 10)
        {
            var tocNode = OdfNodeFactory.CreateElement("table-of-content", OdfNamespaces.Text, "text");
            tocNode.SetAttribute("name", OdfNamespaces.Text, title, "text");

            var sourceNode = OdfNodeFactory.CreateElement("table-of-content-source", OdfNamespaces.Text, "text");
            sourceNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
            tocNode.AppendChild(sourceNode);

            var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");
            
            var titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            titlePara.SetAttribute("style-name", OdfNamespaces.Text, "Contents_20_Heading", "text");
            titlePara.TextContent = title;
            bodyNode.AppendChild(titlePara);
            
            tocNode.AppendChild(bodyNode);
            BodyTextRoot.AppendChild(tocNode);

            SetUpdateFieldsWhenOpening(true);
            return new OdfTableOfContents(tocNode, this);
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

            // Validate that mathMlXmlString is well-formed XML
            try
            {
                XElement.Parse(mathMlXmlString);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Invalid MathML XML: " + ex.Message, nameof(mathMlXmlString), ex);
            }

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

        #region CJK Font Fallback

        public void ApplyCjkFontFallback()
        {
            // Declare default CJK fonts in font-face-decls if they are not present
            AddFontFace("PMingLiU", "PMingLiU", "system-serif", "variable");
            AddFontFace("Microsoft JhengHei", "Microsoft JhengHei", "system-sans-serif", "variable");
            AddFontFace("MS Mincho", "MS Mincho", "system-serif", "variable");
            AddFontFace("MS Gothic", "MS Gothic", "system-sans-serif", "variable");
            AddFontFace("SimSun", "SimSun", "system-serif", "variable");
            AddFontFace("Microsoft YaHei", "Microsoft YaHei", "system-sans-serif", "variable");
            AddFontFace("Malgun Gothic", "Malgun Gothic", "system-sans-serif", "variable");
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

        #region Tracked Changes (Accept/Reject)

        public bool TrackedChanges { get; set; }

        public string RecordTrackedChange(string changeType, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null)
        {
            OdfNode? tcNode = null;
            foreach (var child in BodyTextRoot.Children)
            {
                if (child.LocalName == "tracked-changes" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    tcNode = child;
                    break;
                }
            }
            if (tcNode == null)
            {
                tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
                if (BodyTextRoot.Children.Count > 0)
                    BodyTextRoot.InsertBefore(tcNode, BodyTextRoot.Children[0]);
                else
                    BodyTextRoot.AppendChild(tcNode);
            }

            string changeId = "ct_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
            changedRegion.SetAttribute("id", OdfNamespaces.Text, changeId, "text");

            var typeNode = new OdfNode(OdfNodeType.Element, changeType, OdfNamespaces.Text, "text");
            if (changeType == "deletion" && extraContent != null)
            {
                typeNode.AppendChild(extraContent.CloneNode(true));
            }
            else if (changeType == "format-change")
            {
                if (originalStyleName != null)
                {
                    typeNode.SetAttribute("style-name", OdfNamespaces.Text, originalStyleName, "text");
                }
                if (targetFamily != null)
                {
                    typeNode.SetAttribute("target-family", OdfNamespaces.Text, targetFamily, "text");
                }
            }
            changedRegion.AppendChild(typeNode);

            var changeInfo = new OdfNode(OdfNodeType.Element, "change-info", OdfNamespaces.Office, "office");
            typeNode.AppendChild(changeInfo);

            var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
            creator.TextContent = "Author";
            changeInfo.AppendChild(creator);

            var date = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
            date.TextContent = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            changeInfo.AppendChild(date);

            tcNode.AppendChild(changedRegion);
            return changeId;
        }

        public void TrackFormatChange(OdfNode node, string family)
        {
            if (!TrackedChanges) return;

            string styleAttr = "style-name";
            string styleNs = family switch
            {
                "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
                "graphic" => OdfNamespaces.Draw,
                _ => OdfNamespaces.Text
            };
            string? originalStyleName = node.GetAttribute(styleAttr, styleNs);

            string changeId = RecordTrackedChange("format-change", null, originalStyleName, family);

            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            if (node.LocalName == "p" || node.LocalName == "h")
            {
                if (node.Children.Count > 0)
                {
                    node.InsertBefore(startNode, node.Children[0]);
                }
                else
                {
                    node.AppendChild(startNode);
                }
                node.AppendChild(endNode);
            }
            else
            {
                var parent = node.Parent;
                if (parent != null)
                {
                    parent.InsertBefore(startNode, node);
                    parent.InsertAfter(endNode, node);
                }
            }
        }

        public void DeleteNode(OdfNode node)
        {
            if (node.Parent == null) return;
            var parent = node.Parent;

            if (TrackedChanges)
            {
                string changeId = RecordTrackedChange("deletion", node);

                var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
                startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
                var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
                endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

                parent.InsertBefore(startNode, node);
                parent.InsertAfter(endNode, node);
                parent.RemoveChild(node);
            }
            else
            {
                parent.RemoveChild(node);
            }
        }

        private List<OdfNode> FindAffectedNodesForFormatChange(string changeId)
        {
            var affected = new List<OdfNode>();
            
            // Look up targetFamily from tracked changes metadata
            string? targetFamily = null;
            var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
            if (tcNode != null)
            {
                foreach (var region in tcNode.Children)
                {
                    if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
                    {
                        foreach (var spec in region.Children)
                        {
                            if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                            {
                                targetFamily = spec.GetAttribute("target-family", OdfNamespaces.Text);
                                break;
                            }
                        }
                    }
                }
            }

            var startNode = FindChangeNode(BodyTextRoot, "change-start", changeId);
            if (startNode == null || startNode.Parent == null) return affected;

            var parent = startNode.Parent;
            
            if (targetFamily == "paragraph")
            {
                affected.Add(parent);
                return affected;
            }

            if (parent.LocalName == "p" || parent.LocalName == "h")
            {
                var endNode = FindChangeNode(parent, "change-end", changeId);
                if (endNode != null && endNode.Parent == parent)
                {
                    var siblingsBetween = new List<OdfNode>();
                    bool collect = false;
                    foreach (var child in parent.Children)
                    {
                        if (child == startNode) { collect = true; continue; }
                        if (child == endNode) { collect = false; break; }
                        if (collect) siblingsBetween.Add(child);
                    }

                    if (siblingsBetween.Count > 0)
                    {
                        foreach (var sibling in siblingsBetween)
                        {
                            if (sibling.LocalName == "span")
                            {
                                affected.Add(sibling);
                            }
                        }
                    }
                    else
                    {
                        affected.Add(parent);
                    }
                }
            }
            else
            {
                var endNode = FindChangeNode(BodyTextRoot, "change-end", changeId);
                if (endNode != null && endNode.Parent == parent)
                {
                    bool collect = false;
                    foreach (var child in parent.Children)
                    {
                        if (child == startNode) { collect = true; continue; }
                        if (child == endNode) { collect = false; break; }
                        if (collect) affected.Add(child);
                    }
                }
            }

            if (affected.Count == 0)
            {
                affected.Add(parent);
            }

            return affected;
        }

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

        public void RejectAllTrackedChanges()
        {
            var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
            if (tcNode == null) return;

            var changes = new Dictionary<string, string>(StringComparer.Ordinal);
            ExtractTrackedChangesMeta(tcNode, changes);

            foreach (var kvp in changes)
            {
                if (kvp.Value == "insertion")
                {
                    var purger = new ChangePurger(kvp.Key);
                    purger.Purge(BodyTextRoot);
                }
                else if (kvp.Value == "deletion")
                {
                    RestoreDeletedContent(tcNode, kvp.Key);
                }
                else if (kvp.Value == "format-change")
                {
                    string? originalStyleName = null;
                    foreach (var changedRegion in tcNode.Children)
                    {
                        if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == kvp.Key)
                        {
                            foreach (var spec in changedRegion.Children)
                            {
                                if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                                {
                                    originalStyleName = spec.GetAttribute("style-name", OdfNamespaces.Text);
                                    break;
                                }
                            }
                        }
                    }

                    var affected = FindAffectedNodesForFormatChange(kvp.Key);
                    foreach (var node in affected)
                    {
                        string styleAttr = "style-name";
                        string styleNs = OdfNamespaces.Text;
                        if (node.LocalName == "table-cell" || node.LocalName == "table-row" || node.LocalName == "table-column")
                        {
                            styleNs = OdfNamespaces.Table;
                        }
                        else if (node.LocalName == "object" || node.LocalName == "frame")
                        {
                            styleNs = OdfNamespaces.Draw;
                        }

                        if (originalStyleName != null)
                        {
                            node.SetAttribute(styleAttr, styleNs, originalStyleName);
                        }
                        else
                        {
                            node.RemoveAttribute(styleAttr, styleNs);
                        }
                    }
                }
            }

            CleanupRemainingChangeMarkers(BodyTextRoot);
            BodyTextRoot.RemoveChild(tcNode);
        }

        public void AcceptChange(string changeId)
        {
            var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
            if (tcNode == null) return;

            var changes = new Dictionary<string, string>(StringComparer.Ordinal);
            ExtractTrackedChangesMeta(tcNode, changes);

            if (!changes.TryGetValue(changeId, out var type)) return;

            if (type == "deletion")
            {
                var purger = new ChangePurger(changeId);
                purger.Purge(BodyTextRoot);
            }

            RemoveChangeMarkersForId(BodyTextRoot, changeId);

            OdfNode? regionToRemove = null;
            foreach (var region in tcNode.Children)
            {
                if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    regionToRemove = region;
                    break;
                }
            }
            if (regionToRemove != null) tcNode.RemoveChild(regionToRemove);
            if (tcNode.Children.Count == 0) BodyTextRoot.RemoveChild(tcNode);
        }

        public void RejectChange(string changeId)
        {
            var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
            if (tcNode == null) return;

            var changes = new Dictionary<string, string>(StringComparer.Ordinal);
            ExtractTrackedChangesMeta(tcNode, changes);

            if (!changes.TryGetValue(changeId, out var type)) return;

            if (type == "insertion")
            {
                var purger = new ChangePurger(changeId);
                purger.Purge(BodyTextRoot);
            }
            else if (type == "deletion")
            {
                RestoreDeletedContent(tcNode, changeId);
            }
            else if (type == "format-change")
            {
                string? originalStyleName = null;
                foreach (var changedRegion in tcNode.Children)
                {
                    if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == changeId)
                    {
                        foreach (var spec in changedRegion.Children)
                        {
                            if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                            {
                                originalStyleName = spec.GetAttribute("style-name", OdfNamespaces.Text);
                                break;
                            }
                        }
                    }
                }

                var affected = FindAffectedNodesForFormatChange(changeId);
                foreach (var node in affected)
                {
                    string styleAttr = "style-name";
                    string styleNs = OdfNamespaces.Text;
                    if (node.LocalName == "table-cell" || node.LocalName == "table-row" || node.LocalName == "table-column")
                    {
                        styleNs = OdfNamespaces.Table;
                    }
                    else if (node.LocalName == "object" || node.LocalName == "frame")
                    {
                        styleNs = OdfNamespaces.Draw;
                    }

                    if (originalStyleName != null)
                    {
                        node.SetAttribute(styleAttr, styleNs, originalStyleName);
                    }
                    else
                    {
                        node.RemoveAttribute(styleAttr, styleNs);
                    }
                }
            }

            RemoveChangeMarkersForId(BodyTextRoot, changeId);

            OdfNode? regionToRemove = null;
            foreach (var region in tcNode.Children)
            {
                if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    regionToRemove = region;
                    break;
                }
            }
            if (regionToRemove != null) tcNode.RemoveChild(regionToRemove);
            if (tcNode.Children.Count == 0) BodyTextRoot.RemoveChild(tcNode);
        }

        private void RestoreDeletedContent(OdfNode tcNode, string changeId)
        {
            OdfNode? deletionContent = null;
            foreach (var changedRegion in tcNode.Children)
            {
                if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    foreach (var spec in changedRegion.Children)
                    {
                        if (spec.LocalName == "deletion" && spec.NamespaceUri == OdfNamespaces.Text)
                        {
                            deletionContent = spec;
                            break;
                        }
                    }
                }
            }

            if (deletionContent == null) return;

            OdfNode? startNode = FindChangeNode(BodyTextRoot, "change-start", changeId);
            if (startNode != null && startNode.Parent != null)
            {
                var parent = startNode.Parent;
                foreach (var child in deletionContent.Children)
                {
                    if (child.LocalName != "change-info")
                    {
                        var imported = OdfNode.ImportNode(child, Package, Package);
                        parent.InsertBefore(imported, startNode);
                    }
                }
            }
        }

        private OdfNode? FindChangeNode(OdfNode root, string localName, string changeId)
        {
            if (root.LocalName == localName && root.NamespaceUri == OdfNamespaces.Text && root.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
            {
                return root;
            }
            foreach (var child in root.Children)
            {
                var found = FindChangeNode(child, localName, changeId);
                if (found != null) return found;
            }
            return null;
        }

        private void RemoveChangeMarkersForId(OdfNode node, string changeId)
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                if ((child.LocalName == "change-start" || child.LocalName == "change-end") && 
                    child.NamespaceUri == OdfNamespaces.Text && 
                    child.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
                {
                    node.RemoveChild(child);
                }
                else
                {
                    RemoveChangeMarkersForId(child, changeId);
                }
            }
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
                    else if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                    {
                        changes[id!] = "format-change";
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

        public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
        {
            void AddToDom(OdfNode domRoot)
            {
                var fontDecls = FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
                foreach (var child in fontDecls.Children)
                {
                    if (child.LocalName == "font-face" && child.NamespaceUri == OdfNamespaces.Style && child.GetAttribute("name", OdfNamespaces.Style) == name)
                    {
                        child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                        if (genericFamily != null) child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                        if (pitch != null) child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                        return;
                    }
                }

                var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
                fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
                fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                if (genericFamily != null) fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                if (pitch != null) fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                fontDecls.AppendChild(fontFace);
            }

            AddToDom(ContentDom);
            if (StylesDom != null) AddToDom(StylesDom);
        }

        #endregion
    }

    public class OdfPageSetup
    {
        private readonly TextDocument _doc;
        private OdfNode ContentDom => _doc.ContentDom;
        private OdfNode StylesDom => _doc.StylesDom;

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

        public string? WritingMode
        {
            get
            {
                var props = FindOrCreatePageLayoutProperties();
                return props.GetAttribute("writing-mode", OdfNamespaces.Style);
            }
            set
            {
                var props = FindOrCreatePageLayoutProperties();
                props.SetAttribute("writing-mode", OdfNamespaces.Style, value ?? "lr-tb", "style");
            }
        }

        private string? GetPageStyleProp(string name)
        {
            var props = FindOrCreatePageLayoutProperties();
            return props.GetAttribute(name, OdfNamespaces.Style);
        }

        private void SetPageStyleProp(string name, string? val)
        {
            var props = FindOrCreatePageLayoutProperties();
            if (val != null)
            {
                props.SetAttribute(name, OdfNamespaces.Style, val, "style");
            }
            else
            {
                props.RemoveAttribute(name, OdfNamespaces.Style);
            }
        }

        public string? LayoutGridMode
        {
            get => GetPageStyleProp("layout-grid-mode");
            set => SetPageStyleProp("layout-grid-mode", value);
        }

        public string? LayoutGridBaseHeight
        {
            get => GetPageStyleProp("layout-grid-base-height");
            set => SetPageStyleProp("layout-grid-base-height", value);
        }

        public string? LayoutGridBaseWidth
        {
            get => GetPageStyleProp("layout-grid-base-width");
            set => SetPageStyleProp("layout-grid-base-width", value);
        }

        public string? LayoutGridRubyHeight
        {
            get => GetPageStyleProp("layout-grid-ruby-height");
            set => SetPageStyleProp("layout-grid-ruby-height", value);
        }

        public int? LayoutGridLines
        {
            get => int.TryParse(GetPageStyleProp("layout-grid-lines"), out var val) ? val : (int?)null;
            set => SetPageStyleProp("layout-grid-lines", value?.ToString());
        }

        public int? LayoutGridCharacters
        {
            get => int.TryParse(GetPageStyleProp("layout-grid-characters"), out var val) ? val : (int?)null;
            set => SetPageStyleProp("layout-grid-characters", value?.ToString());
        }

        public bool? LayoutGridDisplay
        {
            get => GetPageStyleProp("layout-grid-display") == "true" ? true : (GetPageStyleProp("layout-grid-display") == "false" ? false : (bool?)null);
            set => SetPageStyleProp("layout-grid-display", value == null ? null : (value.Value ? "true" : "false"));
        }

        public bool? LayoutGridPrint
        {
            get => GetPageStyleProp("layout-grid-print") == "true" ? true : (GetPageStyleProp("layout-grid-print") == "false" ? false : (bool?)null);
            set => SetPageStyleProp("layout-grid-print", value == null ? null : (value.Value ? "true" : "false"));
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
            if (localName == "font-face-decls" && parent.Children.Count > 0)
            {
                parent.InsertBefore(node, parent.Children[0]);
            }
            else
            {
                parent.AppendChild(node);
            }
            return node;
        }

        public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
        {
            void AddToDom(OdfNode domRoot)
            {
                var fontDecls = FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
                foreach (var child in fontDecls.Children)
                {
                    if (child.LocalName == "font-face" && child.NamespaceUri == OdfNamespaces.Style && child.GetAttribute("name", OdfNamespaces.Style) == name)
                    {
                        child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                        if (genericFamily != null) child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                        if (pitch != null) child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                        return;
                    }
                }

                var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
                fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
                fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                if (genericFamily != null) fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                if (pitch != null) fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                fontDecls.AppendChild(fontFace);
            }

            AddToDom(_doc.ContentDom);
            if (_doc.StylesDom != null) AddToDom(_doc.StylesDom);
        }
    }

    public class OdfParagraph
    {
        public OdfNode Node { get; }
        protected readonly TextDocument Doc;

        public OdfParagraph(OdfNode node, TextDocument doc)
        {
            Node = node;
            Doc = doc;
        }

        public string TextContent
        {
            get => Node.TextContent;
            set => Node.TextContent = value;
        }

        public string? StyleName
        {
            get => Node.GetAttribute("style-name", OdfNamespaces.Text);
            set
            {
                if (Doc.TrackedChanges)
                {
                    Doc.TrackFormatChange(Node, "paragraph");
                }
                Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
            }
        }

        public string? HorizontalAlignment
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "text-align", OdfNamespaces.Fo, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "text-align", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }

        public string? WritingMode
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "writing-mode", OdfNamespaces.Style, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "writing-mode", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        public string? FontName
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name", OdfNamespaces.Style, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        public string? FontNameAsian
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-asian", OdfNamespaces.Style, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        public string? FontNameComplex
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-complex", OdfNamespaces.Style, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        public string? FontSize
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size", OdfNamespaces.Fo, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }

        public string? FontSizeAsian
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-asian", OdfNamespaces.Fo, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size-asian", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }

        public string? FontSizeComplex
        {
            get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-complex", OdfNamespaces.Fo, "paragraph");
            set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size-complex", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }

        public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
        {
            FontName = westernFont;
            FontNameAsian = asianFont ?? westernFont;
            FontNameComplex = complexFont ?? westernFont;
        }

        public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
        {
            FontSize = westernSize;
            FontSizeAsian = asianSize ?? westernSize;
            FontSizeComplex = complexSize ?? westernSize;
        }

        public OdfTextRun AddTextRun(string text)
        {
            var spanNode = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
            spanNode.TextContent = text;
            if (Doc.TrackedChanges)
            {
                string changeId = Doc.RecordTrackedChange("insertion");
                var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
                startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
                var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
                endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
                
                Node.AppendChild(startNode);
                Node.AppendChild(spanNode);
                Node.AppendChild(endNode);
            }
            else
            {
                Node.AppendChild(spanNode);
            }
            return new OdfTextRun(spanNode, Doc);
        }

        public void AddSoftPageBreak()
        {
            var node = OdfNodeFactory.CreateElement("soft-page-break", OdfNamespaces.Text, "text");
            Node.AppendChild(node);
        }

        public void AddTab()
        {
            var node = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
            Node.AppendChild(node);
        }

        public void AddLineBreak()
        {
            var node = OdfNodeFactory.CreateElement("line-break", OdfNamespaces.Text, "text");
            Node.AppendChild(node);
        }

        public void AddSpace(int count = 1)
        {
            var node = OdfNodeFactory.CreateElement("s", OdfNamespaces.Text, "text");
            if (count > 1)
            {
                node.SetAttribute("c", OdfNamespaces.Text, count.ToString(), "text");
            }
            Node.AppendChild(node);
        }

        public void Delete()
        {
            Doc.DeleteNode(Node);
        }
    }

    public class OdfHeading : OdfParagraph
    {
        public OdfHeading(OdfNode node, TextDocument doc) : base(node, doc)
        {
        }

        public int OutlineLevel
        {
            get => int.TryParse(Node.GetAttribute("outline-level", OdfNamespaces.Text), out var lvl) ? lvl : 1;
            set => Node.SetAttribute("outline-level", OdfNamespaces.Text, value.ToString(), "text");
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

        public string? StyleName
        {
            get => Node.GetAttribute("style-name", OdfNamespaces.Text);
            set
            {
                if (_doc.TrackedChanges)
                {
                    _doc.TrackFormatChange(Node, "text");
                }
                Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
            }
        }

        public string? FontName
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name", OdfNamespaces.Style, "text");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        public string? FontSize
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size", OdfNamespaces.Fo, "text");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }

        public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
        {
            FontName = westernFont;
            FontNameAsian = asianFont ?? westernFont;
            FontNameComplex = complexFont ?? westernFont;
        }

        public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
        {
            FontSize = westernSize;
            FontSizeAsian = asianSize ?? westernSize;
            FontSizeComplex = complexSize ?? westernSize;
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

        public string? FontNameAsian
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-asian", OdfNamespaces.Style, "text");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        public string? FontNameComplex
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-complex", OdfNamespaces.Style, "text");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        public string? FontSizeAsian
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-asian", OdfNamespaces.Fo, "text");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-asian", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }

        public string? FontSizeComplex
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-complex", OdfNamespaces.Fo, "text");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-complex", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }

        private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;

        public void Delete()
        {
            _doc.DeleteNode(Node);
        }
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

        public string? WritingMode
        {
            get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "writing-mode", OdfNamespaces.Style, "section");
            set => _doc.StyleEngine.SetLocalStyleProperty(Node, "section", "section-properties", "writing-mode", OdfNamespaces.Style, value ?? string.Empty, "style");
        }

        private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
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

        public OdfTable AddNestedTable(int row, int col, int nestedRows, int nestedCols)
        {
            var cellNode = GetCellNode(row, col);
            var nestedTableNode = OdfNodeFactory.CreateElement("table", OdfNamespaces.Table, "table");
            cellNode.AppendChild(nestedTableNode);
            return new OdfTable(nestedTableNode, nestedRows, nestedCols, _doc);
        }

        public void SetCellStyle(int row, int col, string styleName)
        {
            var cellNode = GetCellNode(row, col);
            cellNode.SetAttribute("style-name", OdfNamespaces.Table, styleName, "table");
        }

        public void SetRowRepeat(int row, int repeatCount)
        {
            var rows = new List<OdfNode>();
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                    rows.Add(child);
            }
            var rowNode = rows[row];
            rowNode.SetAttribute("number-rows-repeated", OdfNamespaces.Table, repeatCount.ToString(), "table");
        }

        private OdfNode GetCellNode(int row, int col)
        {
            var rows = new List<OdfNode>();
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                    rows.Add(child);
            }
            var rowNode = rows[row];
            var cells = new List<OdfNode>();
            foreach (var child in rowNode.Children)
            {
                if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                    cells.Add(child);
            }
            return cells[col];
        }

        public OdfTableCell GetCell(int row, int col)
        {
            var cellNode = GetCellNode(row, col);
            return new OdfTableCell(cellNode, _doc);
        }

        public void SetColumnWidth(int col, OdfLength width)
        {
            var colNode = GetOrCreateColumnNode(col);
            _doc.StyleEngine.SetLocalStyleProperty(colNode, "table-column", "table-column-properties", "column-width", OdfNamespaces.Style, width.ToString(), "style");
        }

        private OdfNode GetOrCreateColumnNode(int col)
        {
            var cols = new List<OdfNode>();
            OdfNode? firstNonCol = null;
            foreach (var child in Node.Children)
            {
                if (child.NodeType == OdfNodeType.Element)
                {
                    if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                    {
                        cols.Add(child);
                    }
                    else if (firstNonCol == null)
                    {
                        firstNonCol = child;
                    }
                }
            }

            while (cols.Count <= col)
            {
                var newCol = OdfNodeFactory.CreateElement("table-column", OdfNamespaces.Table, "table");
                if (firstNonCol != null)
                {
                    Node.InsertBefore(newCol, firstNonCol);
                }
                else
                {
                    Node.AppendChild(newCol);
                }
                cols.Add(newCol);
            }

            return cols[col];
        }
    }

    public class OdfList
    {
        public OdfNode Node { get; }
        private readonly TextDocument _doc;

        public OdfList(OdfNode node, TextDocument doc)
        {
            Node = node;
            _doc = doc;
        }

        public string? StyleName
        {
            get => Node.GetAttribute("style-name", OdfNamespaces.Text);
            set => Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }

        public bool? ContinueNumbering
        {
            get => Node.GetAttribute("continue-numbering", OdfNamespaces.Text) == "true" ? true : (Node.GetAttribute("continue-numbering", OdfNamespaces.Text) == "false" ? false : null);
            set
            {
                if (value.HasValue)
                    Node.SetAttribute("continue-numbering", OdfNamespaces.Text, value.Value ? "true" : "false", "text");
                else
                    Node.RemoveAttribute("continue-numbering", OdfNamespaces.Text);
            }
        }

        public OdfListItem AddListItem(string text = "")
        {
            var itemNode = OdfNodeFactory.CreateElement("list-item", OdfNamespaces.Text, "text");
            Node.AppendChild(itemNode);
            var item = new OdfListItem(itemNode, _doc);
            if (!string.IsNullOrEmpty(text))
            {
                item.AddParagraph(text);
            }
            return item;
        }

        public void RestartNumbering(int startValue = 1)
        {
            ContinueNumbering = false;
            var firstItemNode = Node.Children.FirstOrDefault(c => c.LocalName == "list-item" && c.NamespaceUri == OdfNamespaces.Text);
            if (firstItemNode != null)
            {
                var item = new OdfListItem(firstItemNode, _doc);
                item.StartValue = startValue;
            }
        }
    }

    public class OdfListItem
    {
        public OdfNode Node { get; }
        private readonly TextDocument _doc;

        public OdfListItem(OdfNode node, TextDocument doc)
        {
            Node = node;
            _doc = doc;
        }

        public int? StartValue
        {
            get => int.TryParse(Node.GetAttribute("start-value", OdfNamespaces.Text), out var val) ? val : null;
            set
            {
                if (value.HasValue)
                    Node.SetAttribute("start-value", OdfNamespaces.Text, value.Value.ToString(), "text");
                else
                    Node.RemoveAttribute("start-value", OdfNamespaces.Text);
            }
        }

        public OdfParagraph AddParagraph(string text = "")
        {
            var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            Node.AppendChild(pNode);
            return new OdfParagraph(pNode, _doc);
        }

        public OdfList AddNestedList(string? styleName = null)
        {
            var listNode = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
            if (styleName != null)
            {
                listNode.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
            }
            Node.AppendChild(listNode);
            return new OdfList(listNode, _doc);
        }
    }

    public class OdfImage
    {
        public OdfNode FrameNode { get; }
        public OdfNode ImageNode { get; }

        public OdfImage(OdfNode frameNode, OdfNode imageNode)
        {
            FrameNode = frameNode;
            ImageNode = imageNode;
        }

        public string? Name
        {
            get => FrameNode.GetAttribute("name", OdfNamespaces.Draw);
            set => FrameNode.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
        }

        public string? AnchorType
        {
            get => FrameNode.GetAttribute("anchor-type", OdfNamespaces.Text);
            set => FrameNode.SetAttribute("anchor-type", OdfNamespaces.Text, value ?? "paragraph", "text");
        }

        public string? Width
        {
            get => FrameNode.GetAttribute("width", OdfNamespaces.Svg);
            set => FrameNode.SetAttribute("width", OdfNamespaces.Svg, value ?? string.Empty, "svg");
        }

        public string? Height
        {
            get => FrameNode.GetAttribute("height", OdfNamespaces.Svg);
            set => FrameNode.SetAttribute("height", OdfNamespaces.Svg, value ?? string.Empty, "svg");
        }

        public string? WrapStyle
        {
            get => FrameNode.GetAttribute("wrap-style", OdfNamespaces.Style);
            set => FrameNode.SetAttribute("wrap-style", OdfNamespaces.Style, value ?? "none", "style");
        }

        public string? CropTop
        {
            get => ImageNode.GetAttribute("clip", OdfNamespaces.Fo);
            set => ImageNode.SetAttribute("clip", OdfNamespaces.Fo, value ?? string.Empty, "fo");
        }
    }

    public class OdfTableCell
    {
        public OdfNode Node { get; }
        private readonly TextDocument _doc;

        public OdfTableCell(OdfNode node, TextDocument doc)
        {
            Node = node;
            _doc = doc;
        }

        public string TextContent
        {
            get => Node.TextContent;
            set => Node.TextContent = value;
        }

        public OdfParagraph AddParagraph(string text)
        {
            var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            pNode.TextContent = text;
            Node.AppendChild(pNode);
            return new OdfParagraph(pNode, _doc);
        }
    }
}

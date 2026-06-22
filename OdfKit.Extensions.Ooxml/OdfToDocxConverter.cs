using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace OdfKit.Conversion;

/// <summary>
/// 將 <see cref="TextDocument"/> (ODT) 轉換為 DOCX 格式的轉換器。
/// 支援段落樣式、字元格式、標題、表格、圖片與追蹤修訂。
/// </summary>
public static class OdfToDocxConverter
{
    /// <summary>
    /// 將 ODT 文字文件轉換並寫入 DOCX 資料流。
    /// </summary>
    /// <param name="odtDocument">來源 ODT 文字文件。</param>
    /// <param name="docxStream">要寫入 DOCX 的目標資料流。</param>
    /// <exception cref="ArgumentNullException">任一必要參數為 null 時引發。</exception>
    public static void Convert(TextDocument odtDocument, Stream docxStream)
    {
        if (odtDocument is null)
            throw new ArgumentNullException(nameof(odtDocument));
        if (docxStream is null)
            throw new ArgumentNullException(nameof(docxStream));

        var ctx = new ConversionContext(odtDocument);
        var imagePartCache = new Dictionary<string, ImagePart>();
        var trackedChanges = TrackedChangeRegistry.From(odtDocument.BodyTextRoot);

        using var wordDoc = WordprocessingDocument.Create(docxStream, WordprocessingDocumentType.Document, autoSave: false);
        var mainPart = wordDoc.AddMainDocumentPart();
        var body = new WP.Body();
        mainPart.Document = new WP.Document(body);

        AddDefaultStyles(mainPart);

        ConvertBodyNodes(odtDocument.BodyTextRoot, body, mainPart, ctx, odtDocument.Package, imagePartCache, trackedChanges);
        ConvertHeaderFooter(odtDocument, mainPart, body);

        EnsureBodyEndsWithParagraph(body);

        wordDoc.Save();
    }

    // -------------------------------------------------------------------------
    // 頁首與頁尾轉換
    // -------------------------------------------------------------------------

    private static void ConvertHeaderFooter(TextDocument odtDocument, MainDocumentPart mainPart, WP.Body body)
    {
        string? headerText = ExtractHeaderFooterText(odtDocument, "header");
        string? footerText = ExtractHeaderFooterText(odtDocument, "footer");
        if (string.IsNullOrEmpty(headerText) && string.IsNullOrEmpty(footerText))
        {
            return;
        }

        var sectionProperties = body.Elements<WP.SectionProperties>().LastOrDefault();
        if (sectionProperties is null)
        {
            sectionProperties = new WP.SectionProperties();
            body.AppendChild(sectionProperties);
        }

        if (!string.IsNullOrEmpty(headerText))
        {
            HeaderPart headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = new WP.Header(new WP.Paragraph(new WP.Run(new WP.Text(headerText!))));
            headerPart.Header.Save();
            sectionProperties.AppendChild(new WP.HeaderReference
            {
                Type = WP.HeaderFooterValues.Default,
                Id = mainPart.GetIdOfPart(headerPart)
            });
        }

        if (!string.IsNullOrEmpty(footerText))
        {
            FooterPart footerPart = mainPart.AddNewPart<FooterPart>();
            footerPart.Footer = new WP.Footer(new WP.Paragraph(new WP.Run(new WP.Text(footerText!))));
            footerPart.Footer.Save();
            sectionProperties.AppendChild(new WP.FooterReference
            {
                Type = WP.HeaderFooterValues.Default,
                Id = mainPart.GetIdOfPart(footerPart)
            });
        }
    }

    private static string? ExtractHeaderFooterText(TextDocument odtDocument, string localName)
    {
        string? fromStyles = FindStyleNamespaceText(odtDocument.StylesDom, localName);
        if (!string.IsNullOrEmpty(fromStyles))
        {
            return fromStyles;
        }

        return FindStyleNamespaceText(odtDocument.ContentDom, localName);
    }

    private static string? FindStyleNamespaceText(OdfNode root, string localName)
    {
        string? fromStyles = FindHeaderFooterInOfficeSection(root, "styles", localName);
        if (!string.IsNullOrEmpty(fromStyles))
        {
            return fromStyles;
        }

        return FindHeaderFooterInMasterStyles(root, localName);
    }

    private static string? FindHeaderFooterInOfficeSection(OdfNode root, string sectionLocalName, string localName)
    {
        OdfNode? section = FindOfficeChildSection(root, sectionLocalName);
        if (section is null)
        {
            return null;
        }

        foreach (var child in section.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
            {
                return child.TextContent;
            }
        }

        return null;
    }

    private static string? FindHeaderFooterInMasterStyles(OdfNode root, string localName)
    {
        OdfNode? masterStyles = FindOfficeChildSection(root, "master-styles");
        if (masterStyles is null)
        {
            return null;
        }

        foreach (var masterPage in masterStyles.Children)
        {
            if (masterPage.LocalName != "master-page" || masterPage.NamespaceUri != OdfNamespaces.Style)
            {
                continue;
            }

            foreach (var child in masterPage.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
                {
                    return child.TextContent;
                }
            }
        }

        return null;
    }

    private static OdfNode? FindOfficeChildSection(OdfNode root, string localName)
    {
        foreach (var child in root.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Office)
            {
                return child;
            }
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // 樣式內容
    // -------------------------------------------------------------------------

    private sealed class ConversionContext
    {
        private readonly Dictionary<string, StyleInfo> _styles = new Dictionary<string, StyleInfo>(StringComparer.Ordinal);

        public ConversionContext(TextDocument document)
        {
            LoadStyles(document.Package);
            LoadStyles(document.StylesDom);
            LoadStyles(document.ContentDom);
        }

        private void LoadStyles(OdfNode root)
        {
            RegisterStylesFromSection(FindOfficeChildSection(root, "automatic-styles"));
            RegisterStylesFromSection(FindOfficeChildSection(root, "styles"));
        }

        private void RegisterStylesFromSection(OdfNode? section)
        {
            if (section is null)
            {
                return;
            }

            foreach (var node in section.Children)
            {
                if (node.LocalName != "style" || node.NamespaceUri != OdfNamespaces.Style)
                {
                    continue;
                }

                string name = node.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                RegisterStyleNode(
                    name,
                    node.GetAttribute("family", OdfNamespaces.Style) ?? string.Empty,
                    node.GetAttribute("parent-style-name", OdfNamespaces.Style),
                    node);
            }
        }

        private void RegisterStyleNode(string name, string family, string? parentName, OdfNode node)
        {
            var info = new StyleInfo
            {
                Name = name,
                Family = family,
                ParentName = parentName
            };
            ReadStyleProperties(node, info);
            _styles[name] = info;
        }

        private void LoadStyles(OdfPackage package)
        {
            LoadStylesEntry(package, "styles.xml");
            LoadStylesEntry(package, "content.xml");
        }

        private void LoadStylesEntry(OdfPackage package, string entryName)
        {
            if (!package.HasEntry(entryName))
                return;
            try
            {
                using var stream = package.GetEntryStream(entryName);
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true,
                    IgnoreWhitespace = false,
                };
                using var reader = XmlReader.Create(stream, settings);
                var doc = new XPathDocument(reader);
                var nav = doc.CreateNavigator();

                var ns = new XmlNamespaceManager(new NameTable());
                ns.AddNamespace("style", OdfNamespaces.Style);
                ns.AddNamespace("fo", OdfNamespaces.Fo);

                var styleNodes = nav.Select("//style:style", ns);
                while (styleNodes.MoveNext())
                {
                    var n = styleNodes.Current!;
                    string name = n.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;
                    string family = n.GetAttribute("family", OdfNamespaces.Style) ?? string.Empty;

                    if (string.IsNullOrEmpty(name))
                        continue;

                    var info = new StyleInfo
                    {
                        Name = name,
                        Family = family,
                        ParentName = n.GetAttribute("parent-style-name", OdfNamespaces.Style)
                    };
                    ReadStyleProperties(n, ns, info);
                    _styles[name] = info;
                }
            }
            catch (XmlException)
            {
                // 忽略格式不正確的樣式 XML，並繼續使用預設樣式。
            }
        }

        private static void ReadStyleProperties(XPathNavigator styleNode,
            XmlNamespaceManager ns, StyleInfo info)
        {
            var paragraphProps = styleNode.SelectSingleNode("style:paragraph-properties", ns);
            ReadParagraphLayoutProperties(paragraphProps, info);

            var textProps = styleNode.SelectSingleNode("style:text-properties", ns);
            if (textProps == null)
                return;

            info.Bold = string.Equals(textProps.GetAttribute("font-weight", OdfNamespaces.Fo), "bold", StringComparison.OrdinalIgnoreCase);
            info.Italic = string.Equals(textProps.GetAttribute("font-style", OdfNamespaces.Fo), "italic", StringComparison.OrdinalIgnoreCase);

            string? underline = textProps.GetAttribute("text-underline-style", OdfNamespaces.Style);
            info.Underline = !string.IsNullOrEmpty(underline) && !string.Equals(underline, "none", StringComparison.OrdinalIgnoreCase);

            string? color = textProps.GetAttribute("color", OdfNamespaces.Fo);
            if (!string.IsNullOrEmpty(color) && color.StartsWith("#", StringComparison.Ordinal))
                info.Color = color!.Substring(1);

            string? fontSize = textProps.GetAttribute("font-size", OdfNamespaces.Fo);
            if (!string.IsNullOrEmpty(fontSize) && fontSize.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
            {
                string ptStr = fontSize!.Substring(0, fontSize.Length - 2);
                if (double.TryParse(ptStr, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double pt))
                    info.FontSizePt = pt;
            }
        }

        private static void ReadStyleProperties(OdfNode styleNode, StyleInfo info)
        {
            foreach (var child in styleNode.Children)
            {
                if (child.LocalName == "paragraph-properties" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    ReadParagraphLayoutProperties(child, info);
                    break;
                }
            }

            OdfNode? textProps = null;
            foreach (var child in styleNode.Children)
            {
                if (child.LocalName == "text-properties" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    textProps = child;
                    break;
                }
            }

            if (textProps is null)
            {
                return;
            }

            info.Bold = string.Equals(textProps.GetAttribute("font-weight", OdfNamespaces.Fo), "bold", StringComparison.OrdinalIgnoreCase);
            info.Italic = string.Equals(textProps.GetAttribute("font-style", OdfNamespaces.Fo), "italic", StringComparison.OrdinalIgnoreCase);

            string? underline = textProps.GetAttribute("text-underline-style", OdfNamespaces.Style);
            info.Underline = !string.IsNullOrEmpty(underline) && !string.Equals(underline, "none", StringComparison.OrdinalIgnoreCase);

            string? color = textProps.GetAttribute("color", OdfNamespaces.Fo);
            if (color is { Length: > 0 } && color.StartsWith("#", StringComparison.Ordinal))
                info.Color = color.Substring(1);

            string? fontSize = textProps.GetAttribute("font-size", OdfNamespaces.Fo);
            if (fontSize is { Length: > 0 } && fontSize.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
            {
                string ptStr = fontSize.Substring(0, fontSize.Length - 2);
                if (double.TryParse(ptStr, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double pt))
                    info.FontSizePt = pt;
            }
        }

        public StyleInfo? GetStyle(string? name)
        {
            return ResolveStyle(name, []);
        }

        private StyleInfo? ResolveStyle(string? name, HashSet<string> visited)
        {
            if (name is null || !_styles.TryGetValue(name, out StyleInfo? style))
            {
                return null;
            }

            if (!visited.Add(name))
            {
                return style;
            }

            StyleInfo? parent = ResolveStyle(style.ParentName, visited);
            return MergeStyles(parent, style);
        }
    }

    private sealed class StyleInfo
    {
        public string Name = string.Empty;
        public string Family = string.Empty;
        public string? ParentName;
        public bool Bold;
        public bool Italic;
        public bool Underline;
        public string? Color;
        public double? FontSizePt;
        public string? TextAlign;
        public string? MarginLeft;
        public string? MarginRight;
        public string? MarginTop;
        public string? LineHeight;
    }

    // -------------------------------------------------------------------------
    // 主體走訪
    // -------------------------------------------------------------------------

    private static void ConvertBodyNodes(OdfNode parent, OpenXmlElement target,
        MainDocumentPart mainPart, ConversionContext ctx, OdfPackage odtPackage,
        Dictionary<string, ImagePart> imagePartCache, TrackedChangeRegistry trackedChanges)
    {
        foreach (var child in parent.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
            {
                if (child.LocalName == "tracked-changes")
                {
                    continue;
                }

                if (child.LocalName == "p")
                {
                    target.AppendChild(ConvertParagraph(child, mainPart, ctx, odtPackage, imagePartCache, trackedChanges));
                }
                else if (child.LocalName == "h")
                {
                    target.AppendChild(ConvertHeading(child, mainPart, ctx, odtPackage, imagePartCache, trackedChanges));
                }
                else if (child.LocalName == "section")
                {
                    ConvertBodyNodes(child, target, mainPart, ctx, odtPackage, imagePartCache, trackedChanges);
                }
            }
            else if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
            {
                target.AppendChild(ConvertTable(child, mainPart, ctx, odtPackage, imagePartCache, trackedChanges));
            }
        }
    }

    private static WP.Paragraph ConvertParagraph(OdfNode node, MainDocumentPart mainPart,
        ConversionContext ctx, OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache,
        TrackedChangeRegistry trackedChanges)
    {
        var para = new WP.Paragraph();
        string? styleName = node.GetAttribute("style-name", OdfNamespaces.Text);

        if (!string.IsNullOrEmpty(styleName))
        {
            string docxStyle = MapOdtStyleToDocx(styleName!);
            WP.ParagraphProperties properties = GetOrCreateParagraphProperties(para);
            properties.AppendChild(new WP.ParagraphStyleId { Val = docxStyle });
        }

        StyleInfo? styleInfo = ctx.GetStyle(styleName);
        ApplyParagraphStyle(para, styleInfo);
        TryApplyParagraphFormatChangeRevision(para, node, ctx, trackedChanges);
        AppendRunsFromNode(node, para, styleInfo, mainPart, ctx, odtPackage, imagePartCache, trackedChanges);
        return para;
    }

    private static WP.Paragraph ConvertHeading(OdfNode node, MainDocumentPart mainPart,
        ConversionContext ctx, OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache,
        TrackedChangeRegistry trackedChanges)
    {
        var para = new WP.Paragraph();
        string? levelAttr = node.GetAttribute("outline-level", OdfNamespaces.Text);
        int level = 1;
        if (int.TryParse(levelAttr, out int lv))
            level = lv < 1 ? 1 : (lv > 6 ? 6 : lv);

        var pp = new WP.ParagraphProperties();
        pp.AppendChild(new WP.ParagraphStyleId { Val = "Heading" + level });
        para.AppendChild(pp);

        string? styleName = node.GetAttribute("style-name", OdfNamespaces.Text);
        ApplyParagraphStyle(para, ctx.GetStyle(styleName));
        AppendRunsFromNode(node, para, null, mainPart, ctx, odtPackage, imagePartCache, trackedChanges);
        return para;
    }

    private static void ApplyParagraphStyle(WP.Paragraph paragraph, StyleInfo? styleInfo)
    {
        WP.ParagraphProperties properties = GetOrCreateParagraphProperties(paragraph);
        ApplyParagraphStyleToProperties(properties, styleInfo);
    }

    private static void ReadParagraphLayoutProperties(XPathNavigator? paragraphProps, StyleInfo info)
    {
        if (paragraphProps is null)
        {
            return;
        }

        AssignParagraphLayoutProperties(
            info,
            paragraphProps.GetAttribute("text-align", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("margin-left", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("margin-right", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("margin-top", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("line-height", OdfNamespaces.Fo));
    }

    private static void ReadParagraphLayoutProperties(OdfNode? paragraphProps, StyleInfo info)
    {
        if (paragraphProps is null)
        {
            return;
        }

        AssignParagraphLayoutProperties(
            info,
            paragraphProps.GetAttribute("text-align", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("margin-left", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("margin-right", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("margin-top", OdfNamespaces.Fo),
            paragraphProps.GetAttribute("line-height", OdfNamespaces.Fo));
    }

    private static void AssignParagraphLayoutProperties(
        StyleInfo info,
        string? textAlign,
        string? marginLeft,
        string? marginRight,
        string? marginTop,
        string? lineHeight)
    {
        if (!string.IsNullOrEmpty(textAlign))
        {
            info.TextAlign = textAlign;
        }

        if (!string.IsNullOrEmpty(marginLeft))
        {
            info.MarginLeft = marginLeft;
        }

        if (!string.IsNullOrEmpty(marginRight))
        {
            info.MarginRight = marginRight;
        }

        if (!string.IsNullOrEmpty(marginTop))
        {
            info.MarginTop = marginTop;
        }

        if (!string.IsNullOrEmpty(lineHeight))
        {
            info.LineHeight = lineHeight;
        }
    }

    private static void ApplyParagraphStyleToProperties(WP.ParagraphProperties properties, StyleInfo? styleInfo)
    {
        if (styleInfo is null)
        {
            return;
        }

        if (styleInfo.TextAlign is not null)
        {
            WP.JustificationValues? justification = styleInfo.TextAlign switch
            {
                "center" => WP.JustificationValues.Center,
                "end" or "right" => WP.JustificationValues.Right,
                "justify" => WP.JustificationValues.Both,
                "start" or "left" => WP.JustificationValues.Left,
                _ => null,
            };
            if (justification is not null)
            {
                properties.AppendChild(new WP.Justification { Val = justification.Value });
            }
        }

        int? leftTwips = OoxmlUnitConverter.TryParseOdfLengthToTwips(styleInfo.MarginLeft);
        int? rightTwips = OoxmlUnitConverter.TryParseOdfLengthToTwips(styleInfo.MarginRight);
        if (leftTwips is not null || rightTwips is not null)
        {
            var indentation = new WP.Indentation();
            if (leftTwips is not null)
            {
                indentation.Left = leftTwips.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (rightTwips is not null)
            {
                indentation.Right = rightTwips.Value.ToString(CultureInfo.InvariantCulture);
            }

            properties.AppendChild(indentation);
        }

        int? beforeTwips = OoxmlUnitConverter.TryParseOdfLengthToTwips(styleInfo.MarginTop);
        (int? lineTwips, bool isAutoRule) = OoxmlUnitConverter.TryParseOdfLineHeight(styleInfo.LineHeight);
        if (beforeTwips is not null || lineTwips is not null)
        {
            WP.SpacingBetweenLines spacing = properties.GetFirstChild<WP.SpacingBetweenLines>()
                ?? properties.AppendChild(new WP.SpacingBetweenLines());
            if (beforeTwips is not null)
            {
                spacing.Before = beforeTwips.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (lineTwips is not null)
            {
                spacing.Line = lineTwips.Value.ToString(CultureInfo.InvariantCulture);
                spacing.LineRule = isAutoRule ? WP.LineSpacingRuleValues.Auto : WP.LineSpacingRuleValues.Exact;
            }
        }
    }

    private static void ApplyParagraphStyleToExtendedProperties(WP.ParagraphPropertiesExtended properties,
        StyleInfo? styleInfo)
    {
        var wrapper = new WP.ParagraphProperties();
        ApplyParagraphStyleToProperties(wrapper, styleInfo);
        foreach (OpenXmlElement child in wrapper.ChildElements.ToList())
        {
            child.Remove();
            properties.AppendChild(child);
        }
    }

    private static void TryApplyParagraphFormatChangeRevision(WP.Paragraph paragraph, OdfNode paragraphNode,
        ConversionContext ctx, TrackedChangeRegistry trackedChanges)
    {
        foreach (OdfNode child in paragraphNode.Children)
        {
            if (child.NodeType != OdfNodeType.Element)
            {
                continue;
            }

            if (child.LocalName != "change-start" || child.NamespaceUri != OdfNamespaces.Text)
            {
                continue;
            }

            string? changeId = child.GetAttribute("change-id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(changeId) ||
                !trackedChanges.TryGet(changeId!, out TrackedChangeEntry? entry) ||
                entry.ChangeType != "format-change" ||
                entry.TargetFamily != "paragraph")
            {
                continue;
            }

            var oldParagraphProperties = new WP.ParagraphPropertiesExtended();
            if (entry.OriginalStyleName is { Length: > 0 } originalStyleName)
            {
                oldParagraphProperties.AppendChild(
                    new WP.ParagraphStyleId { Val = MapOdtStyleToDocx(originalStyleName) });
            }

            ApplyParagraphStyleToExtendedProperties(oldParagraphProperties, ctx.GetStyle(entry.OriginalStyleName));

            var change = new WP.ParagraphPropertiesChange
            {
                Author = string.IsNullOrEmpty(entry.Author) ? "Author" : entry.Author,
                Date = ToOpenXmlDate(entry.ChangedAt),
                Id = trackedChanges.GetRevisionId(entry.ChangeId),
                ParagraphPropertiesExtended = oldParagraphProperties,
            };

            WP.ParagraphProperties properties = GetOrCreateParagraphProperties(paragraph);
            properties.AppendChild(change);
            return;
        }
    }

    private static WP.ParagraphProperties GetOrCreateParagraphProperties(WP.Paragraph paragraph)
    {
        WP.ParagraphProperties? properties = paragraph.GetFirstChild<WP.ParagraphProperties>();
        if (properties is not null)
        {
            return properties;
        }

        properties = new WP.ParagraphProperties();
        paragraph.PrependChild(properties);
        return properties;
    }

    private static void AppendRunsFromNode(OdfNode node, OpenXmlCompositeElement runTarget, StyleInfo? parentStyle,
        MainDocumentPart mainPart, ConversionContext ctx, OdfPackage odtPackage,
        Dictionary<string, ImagePart> imagePartCache, TrackedChangeRegistry trackedChanges,
        TrackedChangeEntry? formatChangeRevision = null)
    {
        for (int i = 0; i < node.Children.Count; i++)
        {
            OdfNode child = node.Children[i];
            if (child.NodeType == OdfNodeType.Text)
            {
                string txt = child.TextContent ?? string.Empty;
                if (txt.Length > 0)
                {
                    runTarget.AppendChild(MakeRun(txt, parentStyle, ctx, formatChangeRevision, trackedChanges));
                }
            }
            else if (child.NodeType == OdfNodeType.Element)
            {
                if (child.LocalName == "change-start" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    string? changeId = child.GetAttribute("change-id", OdfNamespaces.Text);
                    if (string.IsNullOrEmpty(changeId) || !trackedChanges.TryGet(changeId!, out TrackedChangeEntry? entry))
                    {
                        continue;
                    }

                    if (entry.ChangeType == "insertion")
                    {
                        var insertedRun = new WP.InsertedRun
                        {
                            Author = string.IsNullOrEmpty(entry.Author) ? "Author" : entry.Author,
                            Date = ToOpenXmlDate(entry.ChangedAt)
                        };
                        i = AppendRunsUntilChangeEnd(node, i + 1, changeId!, insertedRun, parentStyle,
                            mainPart, ctx, odtPackage, imagePartCache, trackedChanges);
                        if (insertedRun.HasChildren)
                        {
                            runTarget.AppendChild(insertedRun);
                        }
                    }
                    else if (entry.ChangeType == "deletion")
                    {
                        string deletedText = ExtractDeletionText(entry.SpecNode);
                        if (deletedText.Length > 0)
                        {
                            runTarget.AppendChild(new WP.DeletedRun(new WP.Run(new WP.DeletedText(deletedText)))
                            {
                                Author = string.IsNullOrEmpty(entry.Author) ? "Author" : entry.Author,
                                Date = ToOpenXmlDate(entry.ChangedAt)
                            });
                        }

                        i = SkipToChangeEnd(node, i + 1, changeId!);
                    }
                    else if (entry.ChangeType == "format-change" && entry.TargetFamily == "text")
                    {
                        i = AppendRunsUntilChangeEnd(node, i + 1, changeId!, runTarget, parentStyle,
                            mainPart, ctx, odtPackage, imagePartCache, trackedChanges, entry);
                    }
                    else
                    {
                        i = AppendRunsUntilChangeEnd(node, i + 1, changeId!, runTarget, parentStyle,
                            mainPart, ctx, odtPackage, imagePartCache, trackedChanges, formatChangeRevision);
                    }
                }
                else if (child.LocalName == "change-end" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    continue;
                }
                else if (child.LocalName == "span" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    string? spanStyleName = child.GetAttribute("style-name", OdfNamespaces.Text);
                    StyleInfo? spanStyle = ResolveSpanStyle(ctx, spanStyleName);
                    AppendRunsFromNode(child, runTarget, MergeStyles(parentStyle, spanStyle),
                        mainPart, ctx, odtPackage, imagePartCache, trackedChanges, formatChangeRevision);
                }
                else if (child.LocalName == "s" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    string? countAttr = child.GetAttribute("c", OdfNamespaces.Text);
                    int count = int.TryParse(countAttr, out int c) ? c : 1;
                    runTarget.AppendChild(MakeRun(new string(' ', count), parentStyle, ctx, formatChangeRevision, trackedChanges));
                }
                else if (child.LocalName == "tab" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    var run = new WP.Run(new WP.TabChar());
                    run.RunProperties = BuildRunProperties(parentStyle, ctx, formatChangeRevision, trackedChanges);
                    runTarget.AppendChild(run);
                }
                else if (child.LocalName == "line-break" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    runTarget.AppendChild(new WP.Run(new WP.Break { Type = WP.BreakValues.TextWrapping }));
                }
                else if (child.LocalName == "frame" && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    WP.Drawing? drawing = TryConvertImage(child, mainPart, odtPackage, imagePartCache);
                    if (drawing != null)
                    {
                        runTarget.AppendChild(new WP.Run(drawing));
                    }
                }
                else
                {
                    string? textContent = child.TextContent;
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        runTarget.AppendChild(MakeRun(textContent!, parentStyle, ctx, formatChangeRevision, trackedChanges));
                    }
                }
            }
        }

        if (runTarget is WP.Paragraph para)
        {
            bool hasContent = false;
            foreach (OpenXmlElement el in para.ChildElements)
            {
                if (el is WP.Run or WP.Hyperlink or WP.InsertedRun or WP.DeletedRun)
                {
                    hasContent = true;
                    break;
                }
            }

            if (!hasContent)
            {
                para.AppendChild(new WP.Run());
            }
        }
    }

    private static int AppendRunsUntilChangeEnd(OdfNode parent, int startIndex, string changeId,
        OpenXmlCompositeElement runTarget, StyleInfo? parentStyle, MainDocumentPart mainPart,
        ConversionContext ctx, OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache,
        TrackedChangeRegistry trackedChanges, TrackedChangeEntry? formatChangeRevision = null)
    {
        for (int i = startIndex; i < parent.Children.Count; i++)
        {
            OdfNode child = parent.Children[i];
            if (child.LocalName == "change-end" && child.NamespaceUri == OdfNamespaces.Text &&
                child.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
            {
                return i;
            }

            ProcessChildAsRuns(child, runTarget, parentStyle, mainPart, ctx, odtPackage, imagePartCache,
                trackedChanges, formatChangeRevision);
        }

        return parent.Children.Count - 1;
    }

    private static void ProcessChildAsRuns(OdfNode child, OpenXmlCompositeElement runTarget, StyleInfo? parentStyle,
        MainDocumentPart mainPart, ConversionContext ctx, OdfPackage odtPackage,
        Dictionary<string, ImagePart> imagePartCache, TrackedChangeRegistry trackedChanges,
        TrackedChangeEntry? formatChangeRevision = null)
    {
        if (child.NodeType == OdfNodeType.Text)
        {
            string txt = child.TextContent ?? string.Empty;
            if (txt.Length > 0)
            {
                runTarget.AppendChild(MakeRun(txt, parentStyle, ctx, formatChangeRevision, trackedChanges));
            }

            return;
        }

        if (child.NodeType != OdfNodeType.Element)
        {
            return;
        }

        if (child.LocalName == "change-start" && child.NamespaceUri == OdfNamespaces.Text)
        {
            string? changeId = child.GetAttribute("change-id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(changeId) || !trackedChanges.TryGet(changeId!, out TrackedChangeEntry? entry))
            {
                return;
            }

            if (entry.ChangeType == "insertion")
            {
                var insertedRun = new WP.InsertedRun
                {
                    Author = string.IsNullOrEmpty(entry.Author) ? "Author" : entry.Author,
                    Date = ToOpenXmlDate(entry.ChangedAt)
                };
                OdfNode? parent = child.Parent;
                if (parent is not null)
                {
                    int startIndex = parent.Children.IndexOf(child) + 1;
                    AppendRunsUntilChangeEnd(parent, startIndex, changeId!, insertedRun, parentStyle,
                        mainPart, ctx, odtPackage, imagePartCache, trackedChanges);
                }

                if (insertedRun.HasChildren)
                {
                    runTarget.AppendChild(insertedRun);
                }
            }
            else if (entry.ChangeType == "deletion")
            {
                string deletedText = ExtractDeletionText(entry.SpecNode);
                if (deletedText.Length > 0)
                {
                    runTarget.AppendChild(new WP.DeletedRun(new WP.Run(new WP.DeletedText(deletedText)))
                    {
                        Author = string.IsNullOrEmpty(entry.Author) ? "Author" : entry.Author,
                        Date = ToOpenXmlDate(entry.ChangedAt)
                    });
                }
            }
            else if (entry.ChangeType == "format-change" && entry.TargetFamily == "text" &&
                     child.Parent is OdfNode parentNodeForText)
            {
                int startIndex = parentNodeForText.Children.IndexOf(child) + 1;
                AppendRunsUntilChangeEnd(parentNodeForText, startIndex, changeId!, runTarget, parentStyle,
                    mainPart, ctx, odtPackage, imagePartCache, trackedChanges, entry);
            }
            else if (child.Parent is OdfNode parentNode)
            {
                int startIndex = parentNode.Children.IndexOf(child) + 1;
                AppendRunsUntilChangeEnd(parentNode, startIndex, changeId!, runTarget, parentStyle,
                    mainPart, ctx, odtPackage, imagePartCache, trackedChanges, formatChangeRevision);
            }

            return;
        }

        if (child.LocalName == "change-end" && child.NamespaceUri == OdfNamespaces.Text)
        {
            return;
        }

        if (child.LocalName == "span" && child.NamespaceUri == OdfNamespaces.Text)
        {
            string? spanStyleName = child.GetAttribute("style-name", OdfNamespaces.Text);
            StyleInfo? spanStyle = ResolveSpanStyle(ctx, spanStyleName);
            AppendRunsFromNode(child, runTarget, MergeStyles(parentStyle, spanStyle),
                mainPart, ctx, odtPackage, imagePartCache, trackedChanges, formatChangeRevision);
            return;
        }

        if (child.LocalName == "s" && child.NamespaceUri == OdfNamespaces.Text)
        {
            string? countAttr = child.GetAttribute("c", OdfNamespaces.Text);
            int count = int.TryParse(countAttr, out int c) ? c : 1;
            runTarget.AppendChild(MakeRun(new string(' ', count), parentStyle, ctx, formatChangeRevision, trackedChanges));
            return;
        }

        if (child.LocalName == "tab" && child.NamespaceUri == OdfNamespaces.Text)
        {
            var run = new WP.Run(new WP.TabChar());
            run.RunProperties = BuildRunProperties(parentStyle, ctx, formatChangeRevision, trackedChanges);
            runTarget.AppendChild(run);
            return;
        }

        if (child.LocalName == "line-break" && child.NamespaceUri == OdfNamespaces.Text)
        {
            runTarget.AppendChild(new WP.Run(new WP.Break { Type = WP.BreakValues.TextWrapping }));
            return;
        }

        if (child.LocalName == "frame" && child.NamespaceUri == OdfNamespaces.Draw)
        {
            WP.Drawing? drawing = TryConvertImage(child, mainPart, odtPackage, imagePartCache);
            if (drawing != null)
            {
                runTarget.AppendChild(new WP.Run(drawing));
            }

            return;
        }

        string? textContent = child.TextContent;
        if (!string.IsNullOrEmpty(textContent))
        {
            runTarget.AppendChild(MakeRun(textContent!, parentStyle, ctx, formatChangeRevision, trackedChanges));
        }
    }

    private static int SkipToChangeEnd(OdfNode parent, int startIndex, string changeId)
    {
        for (int i = startIndex; i < parent.Children.Count; i++)
        {
            OdfNode child = parent.Children[i];
            if (child.LocalName == "change-end" && child.NamespaceUri == OdfNamespaces.Text &&
                child.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
            {
                return i;
            }
        }

        return parent.Children.Count - 1;
    }

    private static string ExtractDeletionText(OdfNode deletionSpec)
    {
        var builder = new StringBuilder();
        OdfTrackedChangeTextExtractor.ExtractTextContentIgnoringChangeInfo(deletionSpec, builder);
        return builder.ToString();
    }

    private static DateTimeValue? ToOpenXmlDate(DateTime changedAt)
    {
        if (changedAt == DateTime.MinValue)
        {
            return null;
        }

        return new DateTimeValue(changedAt.ToUniversalTime());
    }

    private static WP.Run MakeRun(string text, StyleInfo? style, ConversionContext ctx,
        TrackedChangeEntry? formatChangeRevision = null, TrackedChangeRegistry? trackedChanges = null)
    {
        var runText = new WP.Text { Text = text, Space = SpaceProcessingModeValues.Preserve };
        var run = new WP.Run(runText);
        WP.RunProperties? runProperties = BuildRunProperties(style, ctx, formatChangeRevision, trackedChanges);
        if (runProperties is not null)
        {
            run.RunProperties = runProperties;
        }

        return run;
    }

    private static WP.RunProperties? BuildRunProperties(StyleInfo? style, ConversionContext ctx,
        TrackedChangeEntry? formatChangeRevision = null, TrackedChangeRegistry? trackedChanges = null)
    {
        WP.RunProperties? runProperties = style is not null ? BuildRunProps(style) : null;
        if (formatChangeRevision is null || trackedChanges is null)
        {
            return runProperties;
        }

        runProperties ??= new WP.RunProperties();
        AppendRunFormatChangeRevision(runProperties, formatChangeRevision, ctx, trackedChanges);
        return runProperties;
    }

    private static void AppendRunFormatChangeRevision(WP.RunProperties runProperties, TrackedChangeEntry entry,
        ConversionContext ctx, TrackedChangeRegistry trackedChanges)
    {
        StyleInfo? oldStyle = ctx.GetStyle(entry.OriginalStyleName);
        WP.PreviousRunProperties previousRunProperties = oldStyle is not null
            ? BuildPreviousRunProps(oldStyle)
            : new WP.PreviousRunProperties();

        var change = new WP.RunPropertiesChange
        {
            Author = string.IsNullOrEmpty(entry.Author) ? "Author" : entry.Author,
            Date = ToOpenXmlDate(entry.ChangedAt),
            Id = trackedChanges.GetRevisionId(entry.ChangeId),
            PreviousRunProperties = previousRunProperties,
        };
        runProperties.AppendChild(change);
    }

    private static WP.PreviousRunProperties BuildPreviousRunProps(StyleInfo style)
    {
        var previousRunProperties = new WP.PreviousRunProperties();
        if (style.Bold)
        {
            previousRunProperties.AppendChild(new WP.Bold());
        }

        if (style.Italic)
        {
            previousRunProperties.AppendChild(new WP.Italic());
        }

        if (style.Underline)
        {
            previousRunProperties.AppendChild(new WP.Underline { Val = WP.UnderlineValues.Single });
        }

        if (!string.IsNullOrEmpty(style.Color))
        {
            previousRunProperties.AppendChild(new WP.Color { Val = style.Color });
        }

        if (style.FontSizePt.HasValue)
        {
            int halfPoints = (int)(style.FontSizePt.Value * 2);
            previousRunProperties.AppendChild(new WP.FontSize { Val = halfPoints.ToString() });
        }

        return previousRunProperties;
    }

    private static WP.RunProperties BuildRunProps(StyleInfo style)
    {
        var rp = new WP.RunProperties();
        if (string.Equals(style.Family, "text", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(style.Name) &&
            !IsAutoGeneratedOdfStyleName(style.Name, "text"))
        {
            rp.AppendChild(new WP.RunStyle { Val = MapOdtStyleToDocx(style.Name) });
        }

        if (style.Bold)
            rp.AppendChild(new WP.Bold());
        if (style.Italic)
            rp.AppendChild(new WP.Italic());
        if (style.Underline)
            rp.AppendChild(new WP.Underline { Val = WP.UnderlineValues.Single });
        if (!string.IsNullOrEmpty(style.Color))
            rp.AppendChild(new WP.Color { Val = style.Color });
        if (style.FontSizePt.HasValue)
        {
            int halfPoints = (int)(style.FontSizePt.Value * 2);
            rp.AppendChild(new WP.FontSize { Val = halfPoints.ToString() });
        }
        return rp;
    }

    private static StyleInfo? MergeStyles(StyleInfo? parent, StyleInfo? child)
    {
        if (parent == null)
            return child;
        if (child == null)
            return parent;
        return new StyleInfo
        {
            Name = child.Name,
            Family = child.Family,
            Bold = child.Bold || parent.Bold,
            Italic = child.Italic || parent.Italic,
            Underline = child.Underline || parent.Underline,
            Color = child.Color ?? parent.Color,
            FontSizePt = child.FontSizePt ?? parent.FontSizePt,
            TextAlign = child.TextAlign ?? parent.TextAlign,
            MarginLeft = child.MarginLeft ?? parent.MarginLeft,
            MarginRight = child.MarginRight ?? parent.MarginRight,
            MarginTop = child.MarginTop ?? parent.MarginTop,
            LineHeight = child.LineHeight ?? parent.LineHeight,
        };
    }

    // -------------------------------------------------------------------------
    // 表格轉換
    // -------------------------------------------------------------------------

    private static WP.Table ConvertTable(OdfNode tableNode, MainDocumentPart mainPart,
        ConversionContext ctx, OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache,
        TrackedChangeRegistry trackedChanges)
    {
        var table = new WP.Table();
        var tableProps = new WP.TableProperties(
            new WP.TableBorders(
                new WP.TopBorder { Val = WP.BorderValues.Single, Size = 4 },
                new WP.BottomBorder { Val = WP.BorderValues.Single, Size = 4 },
                new WP.LeftBorder { Val = WP.BorderValues.Single, Size = 4 },
                new WP.RightBorder { Val = WP.BorderValues.Single, Size = 4 },
                new WP.InsideHorizontalBorder { Val = WP.BorderValues.Single, Size = 4 },
                new WP.InsideVerticalBorder { Val = WP.BorderValues.Single, Size = 4 }));
        table.AppendChild(tableProps);

        foreach (var rowNode in tableNode.Children)
        {
            if (rowNode.LocalName == "table-row" && rowNode.NamespaceUri == OdfNamespaces.Table)
            {
                var row = new WP.TableRow();
                foreach (var cellNode in rowNode.Children)
                {
                    if ((cellNode.LocalName == "table-cell" || cellNode.LocalName == "covered-table-cell")
                        && cellNode.NamespaceUri == OdfNamespaces.Table)
                    {
                        var cell = new WP.TableCell();
                        var cellPara = new WP.Paragraph();
                        AppendRunsFromNode(cellNode, cellPara, null, mainPart, ctx, odtPackage, imagePartCache, trackedChanges);
                        cell.AppendChild(cellPara);
                        row.AppendChild(cell);
                    }
                }
                if (row.HasChildren)
                    table.AppendChild(row);
            }
        }

        return table;
    }

    // -------------------------------------------------------------------------
    // 圖片轉換
    // -------------------------------------------------------------------------

    private static WP.Drawing? TryConvertImage(OdfNode frameNode, MainDocumentPart mainPart,
        OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache)
    {
        OdfNode? imageNode = null;
        foreach (var child in frameNode.Children)
        {
            if (child.LocalName == "image" && child.NamespaceUri == OdfNamespaces.Draw)
            {
                imageNode = child;
                break;
            }
        }
        if (imageNode == null)
            return null;

        string? href = imageNode.GetAttribute("href", OdfNamespaces.XLink);
        if (string.IsNullOrEmpty(href))
            return null;

        if (!imagePartCache.TryGetValue(href!, out ImagePart? imagePart))
        {
            string contentType = GuessContentType(href!);
            imagePart = mainPart.AddImagePart(contentType);
            try
            {
                if (odtPackage.HasEntry(href!))
                {
                    using Stream imgStream = odtPackage.GetEntryStream(href!);
                    imagePart.FeedData(imgStream);
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
            imagePartCache[href!] = imagePart;
        }

        string relId = mainPart.GetIdOfPart(imagePart);

        string? widthAttr = frameNode.GetAttribute("width", OdfNamespaces.Svg);
        string? heightAttr = frameNode.GetAttribute("height", OdfNamespaces.Svg);
        long cx = ParseEmu(widthAttr, 2_000_000);
        long cy = ParseEmu(heightAttr, 1_500_000);

        return BuildDrawing(relId, cx, cy);
    }

    private static WP.Drawing BuildDrawing(string relId, long cx, long cy)
    {
        return new WP.Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = 1, Name = "Image" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0, Name = "img.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(
                                    new A.AdjustValueList())
                                { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0 });
    }

    private static long ParseEmu(string? attrValue, long fallback)
    {
        if (string.IsNullOrEmpty(attrValue))
            return fallback;

        if (attrValue!.Length > 2 && string.Equals(attrValue.Substring(attrValue.Length - 2), "cm", StringComparison.OrdinalIgnoreCase))
        {
            string numStr = attrValue.Substring(0, attrValue.Length - 2);
            if (double.TryParse(numStr, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double cm))
                return (long)(cm * 360_000);
        }
        else if (attrValue.Length > 2 && string.Equals(attrValue.Substring(attrValue.Length - 2), "in", StringComparison.OrdinalIgnoreCase))
        {
            string numStr = attrValue.Substring(0, attrValue.Length - 2);
            if (double.TryParse(numStr, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double inches))
                return (long)(inches * 914_400);
        }

        return fallback;
    }

    private static string GuessContentType(string href)
    {
        string lower = href.ToLowerInvariant();
        if (lower.EndsWith(".png", StringComparison.Ordinal))
            return "image/png";
        if (lower.EndsWith(".jpg", StringComparison.Ordinal) || lower.EndsWith(".jpeg", StringComparison.Ordinal))
            return "image/jpeg";
        if (lower.EndsWith(".gif", StringComparison.Ordinal))
            return "image/gif";
        if (lower.EndsWith(".svg", StringComparison.Ordinal))
            return "image/svg+xml";
        return "image/png";
    }

    // -------------------------------------------------------------------------
    // 樣式
    // -------------------------------------------------------------------------

    private static void AddDefaultStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new WP.Styles();
        styles.AppendChild(MakeParaStyle("Normal", "Normal", isDefault: true, basedOn: null, halfPoints: null, bold: false));
        styles.AppendChild(MakeParaStyle("Heading1", "Heading 1", isDefault: false, basedOn: "Normal", halfPoints: 28, bold: true));
        styles.AppendChild(MakeParaStyle("Heading2", "Heading 2", isDefault: false, basedOn: "Normal", halfPoints: 26, bold: true));
        styles.AppendChild(MakeParaStyle("Heading3", "Heading 3", isDefault: false, basedOn: "Normal", halfPoints: 24, bold: true));
        styles.AppendChild(MakeParaStyle("Heading4", "Heading 4", isDefault: false, basedOn: "Normal", halfPoints: 22, bold: false));
        styles.AppendChild(MakeParaStyle("Heading5", "Heading 5", isDefault: false, basedOn: "Normal", halfPoints: 20, bold: false));
        styles.AppendChild(MakeParaStyle("Heading6", "Heading 6", isDefault: false, basedOn: "Normal", halfPoints: 18, bold: false));
        stylesPart.Styles = styles;
    }

    private static WP.Style MakeParaStyle(string id, string name, bool isDefault, string? basedOn, int? halfPoints, bool bold)
    {
        var style = new WP.Style
        {
            Type = WP.StyleValues.Paragraph,
            StyleId = id,
            Default = isDefault ? (bool?)true : null,
        };
        style.AppendChild(new WP.StyleName { Val = name });
        if (!string.IsNullOrEmpty(basedOn))
            style.AppendChild(new WP.BasedOn { Val = basedOn });

        if (halfPoints.HasValue || bold)
        {
            var rp = new WP.StyleRunProperties();
            if (halfPoints.HasValue)
                rp.AppendChild(new WP.FontSize { Val = halfPoints.Value.ToString() });
            if (bold)
                rp.AppendChild(new WP.Bold());
            style.AppendChild(rp);
        }

        return style;
    }

    // -------------------------------------------------------------------------
    // 輔助方法
    // -------------------------------------------------------------------------

    private static void EnsureBodyEndsWithParagraph(WP.Body body)
    {
        bool lastIsPara = false;
        foreach (var el in body.ChildElements)
            lastIsPara = el is WP.Paragraph;
        if (!lastIsPara)
            body.AppendChild(new WP.Paragraph());
    }

    private static StyleInfo? ResolveSpanStyle(ConversionContext ctx, string? spanStyleName)
    {
        StyleInfo? spanStyle = ctx.GetStyle(spanStyleName);
        if (spanStyle is not null || string.IsNullOrEmpty(spanStyleName))
        {
            return spanStyle;
        }

        return new StyleInfo
        {
            Name = spanStyleName!,
            Family = "text",
        };
    }

    private static bool IsAutoGeneratedOdfStyleName(string styleName, string family)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return true;
        }

        string suffix = styleName.Length > 1 ? styleName.Substring(1) : string.Empty;
        return family.ToLowerInvariant() switch
        {
            "paragraph" => styleName.Length > 1 &&
                styleName[0] == 'P' &&
                int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "text" => styleName.Length > 1 &&
                styleName[0] == 'T' &&
                int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            _ => false,
        };
    }

    private static string MapOdtStyleToDocx(string odtStyleName)
    {
        switch (odtStyleName)
        {
            case "Heading_20_1":
            case "Heading 1":
                return "Heading1";
            case "Heading_20_2":
            case "Heading 2":
                return "Heading2";
            case "Heading_20_3":
            case "Heading 3":
                return "Heading3";
            case "Heading_20_4":
            case "Heading 4":
                return "Heading4";
            case "Heading_20_5":
            case "Heading 5":
                return "Heading5";
            case "Heading_20_6":
            case "Heading 6":
                return "Heading6";
            case "Text_20_Body":
            case "Text Body":
                return "Normal";
            default:
                return SanitizeDocxStyleId(odtStyleName);
        }
    }

    private static string SanitizeDocxStyleId(string styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return "Normal";
        }

        return styleName.Replace(" ", "_");
    }

    private sealed class TrackedChangeEntry
    {
        public TrackedChangeEntry(string changeId, string changeType, string author, DateTime changedAt, OdfNode specNode)
        {
            ChangeId = changeId;
            ChangeType = changeType;
            Author = author;
            ChangedAt = changedAt;
            SpecNode = specNode;
            OriginalStyleName = specNode.GetAttribute("style-name", OdfNamespaces.Text);
            TargetFamily = specNode.GetAttribute("target-family", OdfNamespaces.Text) ?? string.Empty;
        }

        public string ChangeId { get; }
        public string ChangeType { get; }
        public string Author { get; }
        public DateTime ChangedAt { get; }
        public OdfNode SpecNode { get; }
        public string? OriginalStyleName { get; }
        public string TargetFamily { get; }
    }

    private sealed class TrackedChangeRegistry
    {
        private readonly Dictionary<string, TrackedChangeEntry> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _revisionIds = new(StringComparer.Ordinal);
        private int _nextRevisionId;

        public static TrackedChangeRegistry From(OdfNode bodyTextRoot)
        {
            var registry = new TrackedChangeRegistry();
            OdfNode? trackedChangesNode = null;
            foreach (OdfNode child in bodyTextRoot.Children)
            {
                if (child.LocalName == "tracked-changes" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    trackedChangesNode = child;
                    break;
                }
            }

            if (trackedChangesNode is null)
            {
                return registry;
            }

            foreach (OdfNode changedRegion in trackedChangesNode.Children)
            {
                string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                foreach (OdfNode spec in changedRegion.Children)
                {
                    if (spec.NamespaceUri != OdfNamespaces.Text)
                    {
                        continue;
                    }

                    if (spec.LocalName is not ("insertion" or "deletion" or "format-change"))
                    {
                        continue;
                    }

                    (string author, DateTime changedAt) = OdfTrackedChangeMetadataReader.Read(spec);
                    registry._entries[id!] = new TrackedChangeEntry(id!, spec.LocalName, author, changedAt, spec);
                    break;
                }
            }

            return registry;
        }

        public bool TryGet(string changeId, out TrackedChangeEntry entry) =>
            _entries.TryGetValue(changeId, out entry!);

        public string GetRevisionId(string changeId)
        {
            if (!_revisionIds.TryGetValue(changeId, out string? revisionId))
            {
                revisionId = _nextRevisionId.ToString(CultureInfo.InvariantCulture);
                _revisionIds[changeId] = revisionId;
                _nextRevisionId++;
            }

            return revisionId;
        }
    }
}

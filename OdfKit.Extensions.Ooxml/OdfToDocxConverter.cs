using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// 支援段落樣式、字元格式、標題、表格與圖片。
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

        using var wordDoc = WordprocessingDocument.Create(docxStream, WordprocessingDocumentType.Document, autoSave: false);
        var mainPart = wordDoc.AddMainDocumentPart();
        var body = new WP.Body();
        mainPart.Document = new WP.Document(body);

        AddDefaultStyles(mainPart);

        ConvertBodyNodes(odtDocument.BodyTextRoot, body, mainPart, ctx, odtDocument.Package, imagePartCache);
        ConvertHeaderFooter(odtDocument, mainPart, body);

        EnsureBodyEndsWithParagraph(body);

        wordDoc.Save();
    }

    // -------------------------------------------------------------------------
    // Header and footer conversion
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
        foreach (var node in odtDocument.StylesDom.Descendants())
        {
            if (node.LocalName == localName && node.NamespaceUri == OdfNamespaces.Style)
            {
                return node.TextContent;
            }
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Style context
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
            foreach (var node in root.Descendants())
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

                var info = new StyleInfo
                {
                    Name = name,
                    Family = node.GetAttribute("family", OdfNamespaces.Style) ?? string.Empty,
                    ParentName = node.GetAttribute("parent-style-name", OdfNamespaces.Style)
                };
                ReadStyleProperties(node, info);
                _styles[name] = info;
            }
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
            string? textAlign = paragraphProps?.GetAttribute("text-align", OdfNamespaces.Fo);
            if (!string.IsNullOrEmpty(textAlign))
            {
                info.TextAlign = textAlign;
            }

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
                if (double.TryParse(ptStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double pt))
                    info.FontSizePt = pt;
            }
        }

        private static void ReadStyleProperties(OdfNode styleNode, StyleInfo info)
        {
            foreach (var child in styleNode.Children)
            {
                if (child.LocalName == "paragraph-properties" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    string? textAlign = child.GetAttribute("text-align", OdfNamespaces.Fo);
                    if (!string.IsNullOrEmpty(textAlign))
                    {
                        info.TextAlign = textAlign;
                    }
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
                if (double.TryParse(ptStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double pt))
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
    }

    // -------------------------------------------------------------------------
    // Body traversal
    // -------------------------------------------------------------------------

    private static void ConvertBodyNodes(OdfNode parent, OpenXmlElement target,
        MainDocumentPart mainPart, ConversionContext ctx, OdfPackage odtPackage,
        Dictionary<string, ImagePart> imagePartCache)
    {
        foreach (var child in parent.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
            {
                if (child.LocalName == "p")
                {
                    target.AppendChild(ConvertParagraph(child, mainPart, ctx, odtPackage, imagePartCache));
                }
                else if (child.LocalName == "h")
                {
                    target.AppendChild(ConvertHeading(child, mainPart, ctx, odtPackage, imagePartCache));
                }
                else if (child.LocalName == "section")
                {
                    ConvertBodyNodes(child, target, mainPart, ctx, odtPackage, imagePartCache);
                }
            }
            else if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
            {
                target.AppendChild(ConvertTable(child, mainPart, ctx, odtPackage, imagePartCache));
            }
        }
    }

    private static WP.Paragraph ConvertParagraph(OdfNode node, MainDocumentPart mainPart,
        ConversionContext ctx, OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache)
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
        AppendRunsFromNode(node, para, styleInfo, mainPart, ctx, odtPackage, imagePartCache);
        return para;
    }

    private static WP.Paragraph ConvertHeading(OdfNode node, MainDocumentPart mainPart,
        ConversionContext ctx, OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache)
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
        AppendRunsFromNode(node, para, null, mainPart, ctx, odtPackage, imagePartCache);
        return para;
    }

    private static void ApplyParagraphStyle(WP.Paragraph paragraph, StyleInfo? styleInfo)
    {
        if (styleInfo?.TextAlign is null)
        {
            return;
        }

        WP.JustificationValues? justification = styleInfo.TextAlign switch
        {
            "center" => WP.JustificationValues.Center,
            "end" or "right" => WP.JustificationValues.Right,
            "justify" => WP.JustificationValues.Both,
            "start" or "left" => WP.JustificationValues.Left,
            _ => null
        };
        if (justification is null)
        {
            return;
        }

        WP.ParagraphProperties properties = GetOrCreateParagraphProperties(paragraph);
        properties.AppendChild(new WP.Justification { Val = justification.Value });
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

    private static void AppendRunsFromNode(OdfNode node, WP.Paragraph para, StyleInfo? parentStyle,
        MainDocumentPart mainPart, ConversionContext ctx, OdfPackage odtPackage,
        Dictionary<string, ImagePart> imagePartCache)
    {
        foreach (var child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                string txt = child.TextContent ?? string.Empty;
                if (txt.Length > 0)
                    para.AppendChild(MakeRun(txt, parentStyle));
            }
            else if (child.NodeType == OdfNodeType.Element)
            {
                if (child.LocalName == "span" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    string? spanStyleName = child.GetAttribute("style-name", OdfNamespaces.Text);
                    StyleInfo? spanStyle = ctx.GetStyle(spanStyleName);
                    AppendRunsFromNode(child, para, MergeStyles(parentStyle, spanStyle),
                        mainPart, ctx, odtPackage, imagePartCache);
                }
                else if (child.LocalName == "s" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    string? countAttr = child.GetAttribute("c", OdfNamespaces.Text);
                    int count = int.TryParse(countAttr, out int c) ? c : 1;
                    para.AppendChild(MakeRun(new string(' ', count), parentStyle));
                }
                else if (child.LocalName == "tab" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    var run = new WP.Run(new WP.TabChar());
                    if (parentStyle != null)
                        run.RunProperties = BuildRunProps(parentStyle);
                    para.AppendChild(run);
                }
                else if (child.LocalName == "line-break" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    para.AppendChild(new WP.Run(new WP.Break { Type = WP.BreakValues.TextWrapping }));
                }
                else if (child.LocalName == "frame" && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    var drawing = TryConvertImage(child, mainPart, odtPackage, imagePartCache);
                    if (drawing != null)
                        para.AppendChild(new WP.Run(drawing));
                }
                else
                {
                    string? textContent = child.TextContent;
                    if (!string.IsNullOrEmpty(textContent))
                        para.AppendChild(MakeRun(textContent!, parentStyle));
                }
            }
        }

        bool hasContent = false;
        foreach (var el in para.ChildElements)
        {
            if (el is WP.Run || el is WP.Hyperlink)
            { hasContent = true; break; }
        }
        if (!hasContent)
            para.AppendChild(new WP.Run());
    }

    private static WP.Run MakeRun(string text, StyleInfo? style)
    {
        var runText = new WP.Text { Text = text, Space = SpaceProcessingModeValues.Preserve };
        var run = new WP.Run(runText);
        if (style != null)
        {
            var rp = BuildRunProps(style);
            if (rp.HasChildren)
                run.RunProperties = rp;
        }
        return run;
    }

    private static WP.RunProperties BuildRunProps(StyleInfo style)
    {
        var rp = new WP.RunProperties();
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
        };
    }

    // -------------------------------------------------------------------------
    // Table conversion
    // -------------------------------------------------------------------------

    private static WP.Table ConvertTable(OdfNode tableNode, MainDocumentPart mainPart,
        ConversionContext ctx, OdfPackage odtPackage, Dictionary<string, ImagePart> imagePartCache)
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
                        AppendRunsFromNode(cellNode, cellPara, null, mainPart, ctx, odtPackage, imagePartCache);
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
    // Image conversion
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
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double cm))
                return (long)(cm * 360_000);
        }
        else if (attrValue.Length > 2 && string.Equals(attrValue.Substring(attrValue.Length - 2), "in", StringComparison.OrdinalIgnoreCase))
        {
            string numStr = attrValue.Substring(0, attrValue.Length - 2);
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double inches))
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
    // Styles
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
    // Helpers
    // -------------------------------------------------------------------------

    private static void EnsureBodyEndsWithParagraph(WP.Body body)
    {
        bool lastIsPara = false;
        foreach (var el in body.ChildElements)
            lastIsPara = el is WP.Paragraph;
        if (!lastIsPara)
            body.AppendChild(new WP.Paragraph());
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
                return "Normal";
        }
    }
}

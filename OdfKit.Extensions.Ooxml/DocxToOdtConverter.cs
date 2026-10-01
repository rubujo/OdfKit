using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using WP = DocumentFormat.OpenXml.Wordprocessing;
namespace OdfKit.Conversion;

/// <summary>
/// Converts DOCX documents into ODT text documents.
/// 將 DOCX 格式轉換為 <see cref="TextDocument"/> 的轉換器。
/// </summary>
public static partial class DocxToOdtConverter
{
    private const long MaxConverterXmlCharactersInPart = 64L * 1024 * 1024;

    /// <summary>
    /// Converts a DOCX document to ODT.
    /// 從 DOCX 資料流讀取並建立對應的 ODT 文字文件。
    /// </summary>
    /// <param name="docxStream">The source or target object. / DOCX 來源資料流</param>
    /// <returns>The result. / 轉換後的 <see cref="TextDocument"/> 執行個體</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="docxStream"/> 為 null 時擲出</exception>
    /// <exception cref="InvalidDataException">Thrown when the documented condition occurs. / 當 DOCX 缺少主要文件本文時擲出</exception>
    public static TextDocument Convert(Stream docxStream)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(docxStream, nameof(docxStream));

        using var wordDocument = WordprocessingDocument.Open(
            docxStream,
            false,
            new OpenSettings { MaxCharactersInPart = MaxConverterXmlCharactersInPart });
        MainDocumentPart mainPart = wordDocument.MainDocumentPart
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_DocxToOdtConverter_DocxNotFound"));
        WP.Document document = mainPart.Document
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_DocxToOdtConverter_DocxNotFound_2"));
        WP.Body body = document.Body
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_DocxToOdtConverter_DocxNotFound_3"));

        TextDocument odtDocument = TextDocument.Create();
        ConvertHeaderFooter(mainPart, odtDocument);
        var context = new BodyContext(mainPart, odtDocument);
        foreach (var child in body.ChildElements)
        {
            if (child is WP.Paragraph paragraph)
            {
                ConvertBodyParagraph(context, paragraph);
            }
            else if (child is WP.Table table)
            {
                ConvertBodyTable(context, table);
            }
        }

        return odtDocument;
    }

    private static void ConvertHeaderFooter(MainDocumentPart mainPart, TextDocument odtDocument)
    {
        string headerText = string.Concat(mainPart.HeaderParts.Select(part => part.Header?.InnerText ?? string.Empty));
        string footerText = string.Concat(mainPart.FooterParts.Select(part => part.Footer?.InnerText ?? string.Empty));
        if (string.IsNullOrEmpty(headerText) && string.IsNullOrEmpty(footerText))
        {
            return;
        }

        OdfPageSetup setup = odtDocument.GetDefaultPageSetup();
        if (!string.IsNullOrEmpty(headerText))
        {
            setup.HeaderText = headerText;
        }

        if (!string.IsNullOrEmpty(footerText))
        {
            setup.FooterText = footerText;
        }
    }

    private static void ConvertParagraph(MainDocumentPart mainPart, WP.Paragraph paragraph, TextDocument odtDocument)
    {
        int headingLevel = GetHeadingLevel(paragraph);
        OdfParagraph odtParagraph = headingLevel > 0
            ? odtDocument.AddHeading(string.Empty, headingLevel)
            : odtDocument.AddParagraph();
        ApplyParagraphProperties(paragraph.ParagraphProperties, odtParagraph, odtDocument, applyStyleName: headingLevel == 0);

        WP.ParagraphPropertiesChange? paragraphFormatChange =
            paragraph.ParagraphProperties?.GetFirstChild<WP.ParagraphPropertiesChange>();
        string? paragraphFormatChangeId = null;
        if (paragraphFormatChange is not null)
        {
            string? originalStyleName = MapDocxParagraphStyleToOdt(
                paragraphFormatChange.ParagraphPropertiesExtended?.GetFirstChild<WP.ParagraphStyleId>()?.Val?.Value);
            paragraphFormatChangeId = odtDocument.AddTrackedChange(
                "format-change",
                paragraphFormatChange.Author?.Value ?? "Author",
                paragraphFormatChange.Date?.Value ?? DateTime.UtcNow,
                extraContent: null,
                originalStyleName: originalStyleName,
                targetFamily: "paragraph");
            PrependChangeStart(odtParagraph.Node, paragraphFormatChangeId);

            string? newStyleName = MapDocxParagraphStyleToOdt(
                paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value);
            if (!string.IsNullOrEmpty(newStyleName))
            {
                odtParagraph.Node.SetAttribute("style-name", OdfNamespaces.Text, newStyleName!, "text");
            }
        }

        foreach (var child in paragraph.ChildElements)
        {
            if (child is WP.Run run)
            {
                ConvertRun(mainPart, run, odtDocument, odtParagraph);
            }
            else if (child is WP.InsertedRun insertedRun)
            {
                AppendInsertedRun(odtDocument, odtParagraph.Node, insertedRun);
            }
            else if (child is WP.DeletedRun deletedRun)
            {
                AppendDeletedRun(odtDocument, odtParagraph.Node, deletedRun);
            }
            else if (child is WP.Hyperlink hyperlink)
            {
                AppendHyperlink(mainPart, hyperlink, odtDocument, odtParagraph);
            }
            else if (IsInlineContainer(child))
            {
                ConvertInlineContainer(mainPart, child, odtDocument, odtParagraph);
            }
        }

        if (!odtParagraph.Node.Children.Any(child =>
                child.NodeType == OdfNodeType.Text ||
                (child.NodeType == OdfNodeType.Element && child.LocalName is not ("change-start" or "change-end"))))
        {
            AppendText(odtParagraph.Node, paragraph.InnerText ?? string.Empty);
        }

        if (paragraphFormatChangeId is not null)
        {
            AppendChangeEnd(odtParagraph.Node, paragraphFormatChangeId);
        }
    }

    private readonly struct ParagraphLayoutProperties
    {
        internal string? TextAlign { get; init; }

        internal string? MarginLeft { get; init; }

        internal string? MarginRight { get; init; }

        internal string? MarginTop { get; init; }

        internal string? LineHeight { get; init; }

        internal bool HasAny =>
            TextAlign is not null ||
            MarginLeft is not null ||
            MarginRight is not null ||
            MarginTop is not null ||
            LineHeight is not null;
    }

    private static void ApplyParagraphProperties(
        WP.ParagraphProperties? properties,
        OdfParagraph odtParagraph,
        TextDocument odtDocument,
        bool applyStyleName = true)
    {
        if (properties is null)
        {
            return;
        }

        ParagraphLayoutProperties layout = ReadParagraphLayout(properties);
        string? mappedStyle = null;
        if (applyStyleName)
        {
            string? styleId = properties.GetFirstChild<WP.ParagraphStyleId>()?.Val?.Value
                ?? properties.ParagraphStyleId?.Val?.Value;
            mappedStyle = MapDocxParagraphStyleToOdt(styleId);
        }

        if (!string.IsNullOrEmpty(mappedStyle))
        {
            if (layout.HasAny)
            {
                EnsureNamedParagraphStyleLayout(odtDocument, mappedStyle!, layout);
            }

            odtParagraph.StyleName = mappedStyle;
            return;
        }

        if (layout.HasAny)
        {
            ApplyParagraphLayoutViaLocalStyle(odtParagraph, layout);
        }
    }

    private static ParagraphLayoutProperties ReadParagraphLayout(WP.ParagraphProperties properties)
    {
        string? textAlign = null;
        if (properties.Justification?.Val is not null)
        {
            WP.JustificationValues value = properties.Justification.Val.Value;
            if (value == WP.JustificationValues.Center)
            {
                textAlign = "center";
            }
            else if (value == WP.JustificationValues.Right)
            {
                textAlign = "end";
            }
            else if (value == WP.JustificationValues.Both)
            {
                textAlign = "justify";
            }
            else
            {
                textAlign = "start";
            }
        }

        string? marginLeft = null;
        string? marginRight = null;
        WP.Indentation? indentation = properties.GetFirstChild<WP.Indentation>();
        if (TryReadOpenXmlTwips(indentation?.Left, out int leftTwips))
        {
            marginLeft = OoxmlUnitConverter.TryFormatTwipsAsOdfCentimeters(leftTwips);
        }

        if (TryReadOpenXmlTwips(indentation?.Right, out int rightTwips))
        {
            marginRight = OoxmlUnitConverter.TryFormatTwipsAsOdfCentimeters(rightTwips);
        }

        string? marginTop = null;
        string? lineHeight = null;
        WP.SpacingBetweenLines? spacing = properties.GetFirstChild<WP.SpacingBetweenLines>();
        if (TryReadOpenXmlTwips(spacing?.Before, out int beforeTwips))
        {
            marginTop = OoxmlUnitConverter.TryFormatTwipsAsOdfCentimeters(beforeTwips);
        }

        if (TryReadOpenXmlTwips(spacing?.Line, out int lineTwips))
        {
            bool isAutoRule = spacing?.LineRule?.Value == WP.LineSpacingRuleValues.Auto;
            lineHeight = OoxmlUnitConverter.TryFormatLineHeightFromTwips(lineTwips, isAutoRule);
        }

        return new ParagraphLayoutProperties
        {
            TextAlign = textAlign,
            MarginLeft = marginLeft,
            MarginRight = marginRight,
            MarginTop = marginTop,
            LineHeight = lineHeight,
        };
    }

    private static void EnsureNamedParagraphStyleLayout(
        TextDocument odtDocument,
        string styleName,
        ParagraphLayoutProperties layout)
    {
        OdfNode autoStyles = TextDocumentDomHelper.FindOrCreateChild(
            odtDocument.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");

        OdfNode? styleNode = FindAutomaticStyleByName(autoStyles, styleName);
        if (styleNode is null)
        {
            styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
            styleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
            styleNode.SetAttribute("family", OdfNamespaces.Style, "paragraph", "style");
            autoStyles.AppendChild(styleNode);
        }

        OdfNode propsNode = GetOrCreateParagraphPropertiesNode(styleNode);
        if (layout.TextAlign is not null)
        {
            propsNode.SetAttribute("text-align", OdfNamespaces.Fo, layout.TextAlign, "fo");
        }

        if (layout.MarginLeft is not null)
        {
            propsNode.SetAttribute("margin-left", OdfNamespaces.Fo, layout.MarginLeft, "fo");
        }

        if (layout.MarginRight is not null)
        {
            propsNode.SetAttribute("margin-right", OdfNamespaces.Fo, layout.MarginRight, "fo");
        }

        if (layout.MarginTop is not null)
        {
            propsNode.SetAttribute("margin-top", OdfNamespaces.Fo, layout.MarginTop, "fo");
        }

        if (layout.LineHeight is not null)
        {
            propsNode.SetAttribute("line-height", OdfNamespaces.Fo, layout.LineHeight, "fo");
        }

        odtDocument.StyleEngine.RebuildStyleIndex();
    }

    private static void ApplyParagraphLayoutViaLocalStyle(
        OdfParagraph odtParagraph,
        ParagraphLayoutProperties layout)
    {
        OdfStyleEngine styleEngine = odtParagraph.StyleEngine;
        OdfNode paragraphNode = odtParagraph.Node;
        if (layout.TextAlign is not null)
        {
            styleEngine.SetLocalStyleProperty(
                paragraphNode, "paragraph", "paragraph-properties", "text-align", OdfNamespaces.Fo, layout.TextAlign, "fo", deferSave: true);
        }

        if (layout.MarginLeft is not null)
        {
            styleEngine.SetLocalStyleProperty(
                paragraphNode, "paragraph", "paragraph-properties", "margin-left", OdfNamespaces.Fo, layout.MarginLeft, "fo", deferSave: true);
        }

        if (layout.MarginRight is not null)
        {
            styleEngine.SetLocalStyleProperty(
                paragraphNode, "paragraph", "paragraph-properties", "margin-right", OdfNamespaces.Fo, layout.MarginRight, "fo", deferSave: true);
        }

        if (layout.MarginTop is not null)
        {
            styleEngine.SetLocalStyleProperty(
                paragraphNode, "paragraph", "paragraph-properties", "margin-top", OdfNamespaces.Fo, layout.MarginTop, "fo", deferSave: true);
        }

        if (layout.LineHeight is not null)
        {
            styleEngine.SetLocalStyleProperty(
                paragraphNode, "paragraph", "paragraph-properties", "line-height", OdfNamespaces.Fo, layout.LineHeight, "fo", deferSave: true);
        }

        styleEngine.DeduplicateAndSaveStyles();
    }

    private static OdfNode? FindAutomaticStyleByName(OdfNode autoStyles, string styleName)
    {
        foreach (OdfNode child in autoStyles.Children)
        {
            if (string.Equals(child.GetAttribute("name", OdfNamespaces.Style), styleName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static OdfNode GetOrCreateParagraphPropertiesNode(OdfNode styleNode)
    {
        foreach (OdfNode child in styleNode.Children)
        {
            if (child.LocalName == "paragraph-properties" && child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }

        var propsNode = new OdfNode(OdfNodeType.Element, "paragraph-properties", OdfNamespaces.Style, "style");
        styleNode.AppendChild(propsNode);
        return propsNode;
    }

    private static bool TryReadOpenXmlTwips(OpenXmlSimpleType? value, out int twips)
    {
        twips = 0;
        if (value is null)
        {
            return false;
        }

        if (value is Int32Value int32Value)
        {
            twips = int32Value.Value;
            return true;
        }

        string? text = value.InnerText;
        return !string.IsNullOrEmpty(text) &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out twips);
    }

    private static void AppendInsertedRun(TextDocument odtDocument, OdfNode paragraphNode, WP.InsertedRun insertedRun)
    {
        string text = string.Concat(insertedRun.Descendants<WP.Text>().Select(node => node.Text));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        string changeId = odtDocument.AddTrackedChange(
            "insertion",
            insertedRun.Author?.Value ?? "Author",
            insertedRun.Date?.Value ?? DateTime.UtcNow);
        AppendChangeStart(paragraphNode, changeId);
        AppendText(paragraphNode, text);
        AppendChangeEnd(paragraphNode, changeId);
    }

    private static void AppendDeletedRun(TextDocument odtDocument, OdfNode paragraphNode, WP.DeletedRun deletedRun)
    {
        string text = string.Concat(deletedRun.Descendants<WP.DeletedText>().Select(node => node.Text));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var deletedParagraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        AppendText(deletedParagraph, text);

        string changeId = odtDocument.AddTrackedChange(
            "deletion",
            deletedRun.Author?.Value ?? "Author",
            deletedRun.Date?.Value ?? DateTime.UtcNow,
            deletedParagraph);
        AppendChangeStart(paragraphNode, changeId);
        AppendChangeEnd(paragraphNode, changeId);
    }

    private static bool IsInlineContainer(OpenXmlElement element) =>
        element is WP.SdtRun or WP.SdtContentRun or WP.SimpleField or WP.CustomXmlRun;

    /// <summary>
    /// 展開內容控制項、智慧標籤、簡單欄位與自訂 XML 等行內容器，避免其中的文字被整段丟棄。
    /// </summary>
    private static void ConvertInlineContainer(
        MainDocumentPart mainPart,
        OpenXmlElement container,
        TextDocument odtDocument,
        OdfParagraph odtParagraph)
    {
        foreach (OpenXmlElement child in container.ChildElements)
        {
            if (child is WP.Run run)
            {
                ConvertRun(mainPart, run, odtDocument, odtParagraph);
            }
            else if (child is WP.Hyperlink hyperlink)
            {
                AppendHyperlink(mainPart, hyperlink, odtDocument, odtParagraph);
            }
            else if (IsInlineContainer(child))
            {
                ConvertInlineContainer(mainPart, child, odtDocument, odtParagraph);
            }
        }
    }

    /// <summary>
    /// 將 DOCX 超連結轉為 <c>text:a</c>；目標無法解析或為不安全的協定時，退回保留連結文字。
    /// </summary>
    private static void AppendHyperlink(
        MainDocumentPart mainPart,
        WP.Hyperlink hyperlink,
        TextDocument odtDocument,
        OdfParagraph odtParagraph)
    {
        string? target = ResolveHyperlinkTarget(mainPart, hyperlink);
        if (target is null)
        {
            foreach (WP.Run run in hyperlink.Elements<WP.Run>())
            {
                ConvertRun(mainPart, run, odtDocument, odtParagraph);
            }

            return;
        }

        string text = string.Concat(hyperlink.Elements<WP.Run>().Select(ExtractRunText));
        if (text.Length > 0)
        {
            odtParagraph.AddHyperlink(target, text);
        }
    }

    private static string? ResolveHyperlinkTarget(MainDocumentPart mainPart, WP.Hyperlink hyperlink)
    {
        string? relationshipId = hyperlink.Id?.Value;
        if (!string.IsNullOrEmpty(relationshipId))
        {
            HyperlinkRelationship? relationship = mainPart.HyperlinkRelationships
                .FirstOrDefault(item => item.Id == relationshipId);
            if (relationship is not null)
            {
                string uri = relationship.Uri.OriginalString;
                string? fragment = hyperlink.Anchor?.Value;
                if (!string.IsNullOrEmpty(fragment) && uri.IndexOf('#') < 0)
                {
                    uri += "#" + fragment;
                }

                return IsSafeHyperlinkTarget(uri) ? uri : null;
            }
        }

        string? anchor = hyperlink.Anchor?.Value;
        return string.IsNullOrEmpty(anchor) ? null : "#" + anchor;
    }

    private static bool IsSafeHyperlinkTarget(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        int colon = uri.IndexOf(':');
        if (colon <= 0)
        {
            return true;
        }

        string scheme = uri.Substring(0, colon);
        return !scheme.Equals("javascript", StringComparison.OrdinalIgnoreCase)
            && !scheme.Equals("vbscript", StringComparison.OrdinalIgnoreCase)
            && !scheme.Equals("data", StringComparison.OrdinalIgnoreCase);
    }

    private static void ConvertRun(MainDocumentPart mainPart, WP.Run run, TextDocument odtDocument, OdfParagraph odtParagraph)
    {
        AppendRunText(odtDocument, odtParagraph, ExtractRunText(run), run.RunProperties);
        AppendNotes(mainPart, run, odtDocument, odtParagraph);

        foreach (var drawing in run.Descendants<WP.Drawing>())
        {
            AppendDrawing(mainPart, drawing, odtDocument, odtParagraph);
        }
    }

    private static void AppendDrawing(MainDocumentPart mainPart, WP.Drawing drawing, TextDocument odtDocument, OdfParagraph odtParagraph)
    {
        A.Blip? blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        string? relationshipId = blip?.Embed?.Value;
        if (string.IsNullOrEmpty(relationshipId))
        {
            return;
        }

        if (mainPart.GetPartById(relationshipId!) is not ImagePart imagePart)
        {
            return;
        }

        byte[] imageBytes;
        using (Stream stream = imagePart.GetStream(FileMode.Open, FileAccess.Read))
        using (var memory = new MemoryStream())
        {
            OdfBoundedStreamReader.CopyTo(stream, memory, odtDocument.Package.LoadOptions.MaxEntrySize, "Err_OdfPackage_InputStreamSizeLimitExceeded");
            imageBytes = memory.ToArray();
        }

        if (imageBytes.Length == 0)
        {
            return;
        }

        string preferredName = Path.GetFileName(imagePart.Uri.ToString());
        string packagePath = new OdfMediaManager(odtDocument.Package).AddImage(imageBytes, preferredName);
        (OdfLength width, OdfLength height) = GetDrawingSize(drawing);
        odtDocument.AddImage(odtParagraph, packagePath, width, height, preferredName);
    }

    private static (OdfLength Width, OdfLength Height) GetDrawingSize(WP.Drawing drawing)
    {
        DW.Extent? extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
        if (extent?.Cx is not null && extent.Cy is not null)
        {
            return (EmuToCentimeters(extent.Cx.Value), EmuToCentimeters(extent.Cy.Value));
        }

        A.Extents? pictureExtent = drawing.Descendants<A.Extents>().FirstOrDefault();
        if (pictureExtent?.Cx is not null && pictureExtent.Cy is not null)
        {
            return (EmuToCentimeters(pictureExtent.Cx.Value), EmuToCentimeters(pictureExtent.Cy.Value));
        }

        return (OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(2));
    }

    private static OdfLength EmuToCentimeters(long emu)
    {
        // 直接以 EMU/公分換算，避免經由 OdfLength.ConvertTo 的英吋→點→公分中介換算引入額外的浮點誤差。
        return OdfLength.FromCentimeters(emu / (OdfLength.EmusPerInch / 2.54d));
    }

    private static string ExtractRunText(WP.Run run)
    {
        var builder = new System.Text.StringBuilder();
        foreach (OpenXmlElement node in run.Descendants())
        {
            switch (node)
            {
                case WP.Text text:
                    builder.Append(text.Text);
                    break;
                case WP.TabChar:
                    builder.Append('\t');
                    break;
                case WP.CarriageReturn:
                    builder.Append('\n');
                    break;
                case WP.Break lineBreak when lineBreak.Type is null || lineBreak.Type.Value == WP.BreakValues.TextWrapping:
                    builder.Append('\n');
                    break;
                case WP.NoBreakHyphen:
                    builder.Append('\u2011');
                    break;
            }
        }

        return builder.ToString();
    }

    private readonly struct TextFormattingProperties
    {
        internal bool? Bold { get; init; }

        internal bool? Italic { get; init; }

        internal bool? Underline { get; init; }

        internal string? Color { get; init; }

        internal string? FontSize { get; init; }

        internal bool HasAny =>
            Bold is not null ||
            Italic is not null ||
            Underline is not null ||
            Color is not null ||
            FontSize is not null;
    }

    private static void AppendRunText(TextDocument odtDocument, OdfParagraph odtParagraph, string text, WP.RunProperties? properties)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        WP.RunPropertiesChange? formatChange = properties?.GetFirstChild<WP.RunPropertiesChange>();
        bool hasFormatting = properties is not null && HasRunFormatting(properties);
        if (formatChange is not null)
        {
            string? originalStyleName = CreateTextStyleFromPreviousRunProperties(
                odtDocument, formatChange.PreviousRunProperties);
            string changeId = odtDocument.AddTrackedChange(
                "format-change",
                formatChange.Author?.Value ?? "Author",
                formatChange.Date?.Value ?? DateTime.UtcNow,
                extraContent: null,
                originalStyleName: originalStyleName,
                targetFamily: "text");

            OdfTextRun run = odtParagraph.AddTextRun(text);
            ApplyRunPropertiesToTextRun(odtDocument, run, properties);
            WrapNodeWithChangeMarkers(odtParagraph.Node, run.Node, changeId);
            return;
        }

        if (!hasFormatting)
        {
            AppendText(odtParagraph.Node, text);
            return;
        }

        OdfTextRun formattedRun = odtParagraph.AddTextRun(text);
        ApplyRunPropertiesToTextRun(odtDocument, formattedRun, properties);
    }

    private static void ApplyRunPropertiesToTextRun(
        TextDocument odtDocument,
        OdfTextRun run,
        WP.RunProperties? properties)
    {
        if (properties is null)
        {
            return;
        }

        TextFormattingProperties formatting = ReadTextFormatting(properties);
        string? mappedStyle = MapDocxCharacterStyleToOdt(
            properties.GetFirstChild<WP.RunStyle>()?.Val?.Value);
        if (!string.IsNullOrEmpty(mappedStyle))
        {
            if (formatting.HasAny)
            {
                EnsureNamedTextStyleFormatting(odtDocument, mappedStyle!, formatting);
            }

            run.StyleName = mappedStyle;
            return;
        }

        if (formatting.HasAny)
        {
            ApplyTextFormattingViaLocalStyle(run, formatting);
        }
    }

    private static TextFormattingProperties ReadTextFormatting(WP.RunProperties properties)
    {
        bool? bold = null;
        if (properties.Bold is not null)
        {
            bold = properties.Bold.Val is null || properties.Bold.Val.Value;
        }

        bool? italic = null;
        if (properties.Italic is not null)
        {
            italic = properties.Italic.Val is null || properties.Italic.Val.Value;
        }

        bool? underline = null;
        if (properties.Underline?.Val is not null && properties.Underline.Val.Value != WP.UnderlineValues.None)
        {
            underline = true;
        }

        string? color = null;
        if (properties.Color?.Val?.Value is { Length: > 0 } rawColor &&
            !string.Equals(rawColor, "auto", StringComparison.OrdinalIgnoreCase))
        {
            color = "#" + rawColor;
        }

        string? fontSize = null;
        if (properties.FontSize?.Val?.Value is { Length: > 0 } halfPoints &&
            double.TryParse(halfPoints, NumberStyles.Float, CultureInfo.InvariantCulture, out double sizeValue))
        {
            fontSize = (sizeValue / 2d).ToString("0.##", CultureInfo.InvariantCulture) + "pt";
        }

        return new TextFormattingProperties
        {
            Bold = bold,
            Italic = italic,
            Underline = underline,
            Color = color,
            FontSize = fontSize,
        };
    }

    private static void EnsureNamedTextStyleFormatting(
        TextDocument odtDocument,
        string styleName,
        TextFormattingProperties formatting)
    {
        OdfNode autoStyles = TextDocumentDomHelper.FindOrCreateChild(
            odtDocument.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");

        OdfNode? styleNode = FindAutomaticStyleByName(autoStyles, styleName);
        if (styleNode is null)
        {
            styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
            styleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
            styleNode.SetAttribute("family", OdfNamespaces.Style, "text", "style");
            autoStyles.AppendChild(styleNode);
        }

        OdfNode propsNode = GetOrCreateTextPropertiesNode(styleNode);
        if (formatting.Bold is not null)
        {
            propsNode.SetAttribute("font-weight", OdfNamespaces.Fo, formatting.Bold.Value ? "bold" : "normal", "fo");
        }

        if (formatting.Italic is not null)
        {
            propsNode.SetAttribute("font-style", OdfNamespaces.Fo, formatting.Italic.Value ? "italic" : "normal", "fo");
        }

        if (formatting.Underline is not null)
        {
            propsNode.SetAttribute(
                "text-underline-style",
                OdfNamespaces.Style,
                formatting.Underline.Value ? "solid" : "none",
                "style");
        }

        if (formatting.Color is not null)
        {
            propsNode.SetAttribute("color", OdfNamespaces.Fo, formatting.Color, "fo");
        }

        if (formatting.FontSize is not null)
        {
            propsNode.SetAttribute("font-size", OdfNamespaces.Fo, formatting.FontSize, "fo");
        }

        odtDocument.StyleEngine.RebuildStyleIndex();
    }

    private static void ApplyTextFormattingViaLocalStyle(OdfTextRun run, TextFormattingProperties formatting)
    {
        if (formatting.Bold is not null)
        {
            run.IsBold = formatting.Bold.Value;
        }

        if (formatting.Italic is not null)
        {
            run.IsItalic = formatting.Italic.Value;
        }

        if (formatting.Underline is not null)
        {
            run.IsUnderline = formatting.Underline.Value;
        }

        if (formatting.Color is not null)
        {
            run.Color = formatting.Color;
        }

        if (formatting.FontSize is not null)
        {
            run.FontSize = formatting.FontSize;
        }
    }

    private static OdfNode GetOrCreateTextPropertiesNode(OdfNode styleNode)
    {
        foreach (OdfNode child in styleNode.Children)
        {
            if (child.LocalName == "text-properties" && child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }

        var propsNode = new OdfNode(OdfNodeType.Element, "text-properties", OdfNamespaces.Style, "style");
        styleNode.AppendChild(propsNode);
        return propsNode;
    }

    private static string? CreateTextStyleFromPreviousRunProperties(
        TextDocument odtDocument, WP.PreviousRunProperties? previous)
    {
        if (previous is null || !HasPreviousRunFormatting(previous))
        {
            return null;
        }

        var tempSpan = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
        ApplyRunFormattingToStyleEngine(odtDocument, tempSpan, previous);
        return tempSpan.GetAttribute("style-name", OdfNamespaces.Text);
    }

    private static void ApplyRunFormattingToStyleEngine(
        TextDocument odtDocument, OdfNode targetNode, OpenXmlCompositeElement properties)
    {
        if (properties.GetFirstChild<WP.Bold>() is { } bold)
        {
            bool isBold = bold.Val is null || bold.Val.Value;
            odtDocument.StyleEngine.SetLocalStyleProperty(
                targetNode, "text", "text-properties", "font-weight", OdfNamespaces.Fo, isBold ? "bold" : "normal", "fo", deferSave: true);
        }

        if (properties.GetFirstChild<WP.Italic>() is { } italic)
        {
            bool isItalic = italic.Val is null || italic.Val.Value;
            odtDocument.StyleEngine.SetLocalStyleProperty(
                targetNode, "text", "text-properties", "font-style", OdfNamespaces.Fo, isItalic ? "italic" : "normal", "fo", deferSave: true);
        }

        WP.Underline? underline = properties.GetFirstChild<WP.Underline>();
        if (underline?.Val is not null && underline.Val.Value != WP.UnderlineValues.None)
        {
            odtDocument.StyleEngine.SetLocalStyleProperty(
                targetNode, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, "solid", "style", deferSave: true);
        }

        if (properties.GetFirstChild<WP.Color>()?.Val?.Value is { Length: > 0 } color &&
            !string.Equals(color, "auto", StringComparison.OrdinalIgnoreCase))
        {
            odtDocument.StyleEngine.SetLocalStyleProperty(
                targetNode, "text", "text-properties", "color", OdfNamespaces.Fo, "#" + color, "fo", deferSave: true);
        }

        if (properties.GetFirstChild<WP.FontSize>()?.Val?.Value is { Length: > 0 } halfPoints &&
            double.TryParse(halfPoints, NumberStyles.Float, CultureInfo.InvariantCulture, out double sizeValue))
        {
            string fontSize = (sizeValue / 2d).ToString("0.##", CultureInfo.InvariantCulture) + "pt";
            odtDocument.StyleEngine.SetLocalStyleProperty(
                targetNode, "text", "text-properties", "font-size", OdfNamespaces.Fo, fontSize, "fo", deferSave: true);
        }

        odtDocument.StyleEngine.DeduplicateAndSaveStyles();
    }

    private static bool HasRunFormatting(WP.RunProperties properties)
    {
        return properties.GetFirstChild<WP.RunStyle>() is not null ||
            properties.Bold is not null ||
            properties.Italic is not null ||
            properties.Underline is not null ||
            properties.Color is not null ||
            properties.FontSize is not null;
    }

    private static bool HasPreviousRunFormatting(WP.PreviousRunProperties properties)
    {
        return properties.GetFirstChild<WP.Bold>() is not null ||
            properties.GetFirstChild<WP.Italic>() is not null ||
            properties.GetFirstChild<WP.Underline>() is not null ||
            properties.GetFirstChild<WP.Color>() is not null ||
            properties.GetFirstChild<WP.FontSize>() is not null;
    }

    private static string? MapDocxCharacterStyleToOdt(string? docxStyleId)
    {
        if (string.IsNullOrEmpty(docxStyleId))
        {
            return null;
        }

        return docxStyleId switch
        {
            "DefaultParagraphFont" => null,
            _ => docxStyleId,
        };
    }

    private static string? MapDocxParagraphStyleToOdt(string? docxStyleId)
    {
        if (string.IsNullOrEmpty(docxStyleId))
        {
            return null;
        }

        return docxStyleId switch
        {
            "Heading1" => "Heading_20_1",
            "Heading2" => "Heading_20_2",
            "Heading3" => "Heading_20_3",
            "Heading4" => "Heading_20_4",
            "Heading5" => "Heading_20_5",
            "Heading6" => "Heading_20_6",
            "Normal" => null,
            _ => docxStyleId,
        };
    }

    private static void AppendText(OdfNode parent, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (OdfTextWhitespace.NeedsEncoding(text))
        {
            OdfTextWhitespace.AppendEncoded(parent, text);
            return;
        }

        parent.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
    }

    private static void PrependChangeStart(OdfNode paragraphNode, string changeId)
    {
        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        if (paragraphNode.Children.Count > 0)
        {
            paragraphNode.InsertBefore(startNode, paragraphNode.Children[0]);
        }
        else
        {
            paragraphNode.AppendChild(startNode);
        }
    }

    private static void AppendChangeStart(OdfNode paragraphNode, string changeId)
    {
        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        paragraphNode.AppendChild(startNode);
    }

    private static void AppendChangeEnd(OdfNode paragraphNode, string changeId)
    {
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        paragraphNode.AppendChild(endNode);
    }

    private static void WrapNodeWithChangeMarkers(OdfNode parent, OdfNode node, string changeId)
    {
        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        parent.InsertBefore(startNode, node);
        parent.InsertAfter(endNode, node);
    }

    private const int MaxTableColumns = 1024;

    private sealed class TableCellPlacement(int row, int column, int columnSpan, WP.TableCell cell)
    {
        internal int Row { get; } = row;

        internal int Column { get; } = column;

        internal int ColumnSpan { get; } = columnSpan;

        internal int RowSpan { get; set; } = 1;

        internal WP.TableCell Cell { get; } = cell;
    }

    private static void ConvertTable(WP.Table wordTable, TextDocument odtDocument)
    {
        (int rowCount, int columnCount, List<TableCellPlacement> placements) = BuildTableGrid(wordTable);
        if (rowCount == 0 || columnCount == 0)
        {
            return;
        }

        OdfTable odtTable = odtDocument.AddTable(rowCount, columnCount);
        PopulateTable(odtTable, placements);
    }

    /// <summary>
    /// 依 <c>w:gridSpan</c>、<c>w:gridBefore</c> 與 <c>w:vMerge</c> 計算每個實際儲存格所在的格線位置與跨度，
    /// 而不是以儲存格在列中的順序當作欄索引（合併儲存格之後的欄位會整排錯位）。
    /// </summary>
    private static (int RowCount, int ColumnCount, List<TableCellPlacement> Placements) BuildTableGrid(WP.Table wordTable)
    {
        var rows = wordTable.Elements<WP.TableRow>().ToList();
        var placements = new List<TableCellPlacement>();
        var openMerges = new Dictionary<int, TableCellPlacement>();
        int columnCount = 0;
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            int column = Math.Min(
                MaxTableColumns,
                Math.Max(0, rows[rowIndex].TableRowProperties?.GetFirstChild<WP.GridBefore>()?.Val?.Value ?? 0));
            foreach (WP.TableCell cell in rows[rowIndex].Elements<WP.TableCell>())
            {
                if (column >= MaxTableColumns)
                {
                    break;
                }

                WP.TableCellProperties? properties = cell.TableCellProperties;
                int span = Math.Min(MaxTableColumns - column, Math.Max(1, properties?.GridSpan?.Val?.Value ?? 1));
                WP.VerticalMerge? merge = properties?.VerticalMerge;
                bool continuesMerge = merge is not null
                    && (merge.Val is null || merge.Val.Value == WP.MergedCellValues.Continue);
                if (continuesMerge
                    && openMerges.TryGetValue(column, out TableCellPlacement? origin)
                    && origin.ColumnSpan == span)
                {
                    origin.RowSpan++;
                }
                else
                {
                    var placement = new TableCellPlacement(rowIndex, column, span, cell);
                    placements.Add(placement);
                    if (merge?.Val is not null && merge.Val.Value == WP.MergedCellValues.Restart)
                    {
                        openMerges[column] = placement;
                    }
                    else
                    {
                        openMerges.Remove(column);
                    }
                }

                column += span;
            }

            columnCount = Math.Max(columnCount, column);
        }

        return (rows.Count, columnCount, placements);
    }

    private static void PopulateTable(OdfTable odtTable, List<TableCellPlacement> placements)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();
        foreach (TableCellPlacement placement in placements)
        {
            foreach (OpenXmlElement child in placement.Cell.ChildElements)
            {
                if (child is WP.Paragraph paragraph)
                {
                    string text = GetParagraphPlainText(paragraph);
                    if (!string.IsNullOrEmpty(text))
                    {
                        odtTable.GetCell(placement.Row, placement.Column).AddParagraph(text);
                    }
                }
                else if (child is WP.Table nestedWordTable)
                {
                    (int nestedRows, int nestedColumns, List<TableCellPlacement> nestedPlacements) = BuildTableGrid(nestedWordTable);
                    if (nestedRows > 0 && nestedColumns > 0)
                    {
                        OdfTable nestedTable = odtTable.AddNestedTable(placement.Row, placement.Column, nestedRows, nestedColumns);
                        PopulateTable(nestedTable, nestedPlacements);
                    }
                }
            }

            if (placement.RowSpan > 1 || placement.ColumnSpan > 1)
            {
                odtTable.MergeCells(placement.Row, placement.Column, placement.RowSpan, placement.ColumnSpan);
            }
        }
    }

    private static string GetParagraphPlainText(WP.Paragraph paragraph)
    {
        string text = string.Concat(paragraph.Descendants<WP.Run>()
            .Where(run => ReferenceEquals(run.Ancestors<WP.Paragraph>().FirstOrDefault(), paragraph))
            .Select(ExtractRunText));
        return text.Length > 0 ? text : paragraph.InnerText ?? string.Empty;
    }

    private static int GetHeadingLevel(WP.Paragraph paragraph)
    {
        string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrWhiteSpace(styleId))
        {
            return 0;
        }

        string normalized = styleId!.Replace(" ", string.Empty);
        if (!normalized.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string suffix = normalized.Substring("Heading".Length);
        if (!int.TryParse(suffix, out int level))
        {
            return 0;
        }

        if (level < 1)
        {
            return 1;
        }

        return level > 6 ? 6 : level;
    }
}

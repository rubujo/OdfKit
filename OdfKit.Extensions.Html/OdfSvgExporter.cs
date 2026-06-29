using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Styles;
namespace OdfKit.Export;

/// <summary>
/// Applies odf svg exporter.
/// 將 DrawingDocument 匯出為 SVG 的淨室轉換器。
/// </summary>
public static class OdfSvgExporter
{
    /// <summary>
    /// Applies export.
    /// 將指定繪圖文件匯出為 SVG 字串。
    /// </summary>
    /// <param name="document">The source or target object. / 來源繪圖文件</param>
    /// <param name="options">The value to use. / SVG 匯出選項；若為 null 則使用預設值</param>
    /// <returns>The result. / SVG 內容字串</returns>
    public static string Export(DrawingDocument document, OdfSvgExportOptions? options = null)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        options ??= new OdfSvgExportOptions();
        if (options.PageIndex < 0 || options.PageIndex >= document.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(options), OdfLocalizer.GetMessage("Err_OdfSvgExporter_SpecifiedDrawingPageIndex"));

        OdfDrawPage page = document.Pages[options.PageIndex];
        CanvasBounds bounds = MeasurePage(page, options);
        var context = new SvgExportContext(document, options);
        context.DiscoverGradientReferences(page.Node);
        var sb = new StringBuilder(4096);

        if (options.IncludeXmlDeclaration)
        {
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        }

        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ");
        sb.Append("xmlns:xlink=\"http://www.w3.org/1999/xlink\" ");
        sb.Append("width=\"").Append(FormatPoints(bounds.Width)).Append("pt\" ");
        sb.Append("height=\"").Append(FormatPoints(bounds.Height)).Append("pt\" ");
        sb.Append("viewBox=\"0 0 ").Append(FormatPoints(bounds.Width)).Append(' ').Append(FormatPoints(bounds.Height)).Append("\">");
        context.WriteDefinitions(sb);

        foreach (OdfNode child in page.Node.Children)
        {
            WriteDrawingNode(child, sb, context);
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Applies save.
    /// 將指定繪圖文件匯出為 SVG 檔案。
    /// </summary>
    public static void Save(DrawingDocument document, string path, OdfSvgExportOptions? options = null)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, Export(document, options), Encoding.UTF8);
    }

    private static void WriteDrawingNode(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        if (node.NodeType is not OdfNodeType.Element || node.NamespaceUri != OdfNamespaces.Draw)
        {
            return;
        }

        switch (node.LocalName)
        {
            case "g":
                WriteGroup(node, sb, context);
                break;
            case "rect":
                WriteRect(node, sb, context);
                break;
            case "ellipse":
                WriteEllipse(node, sb, context);
                break;
            case "line":
            case "connector":
                WriteLine(node, sb, context);
                break;
            case "path":
                WritePath(node, sb, context);
                break;
            case "polygon":
            case "polyline":
                WritePointShape(node, sb, context);
                break;
            case "frame":
                WriteFrame(node, sb, context);
                break;
            case "custom-shape":
                WriteCustomShape(node, sb, context);
                break;
        }
    }

    private static void WriteGroup(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        sb.Append("<g");
        AppendId(node, sb, context.Options);
        AppendTransform(node, sb);
        sb.Append('>');
        foreach (OdfNode child in node.Children)
        {
            WriteDrawingNode(child, sb, context);
        }
        sb.Append("</g>");
    }

    private static void WriteRect(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        double x = ToPoints(node.GetAttribute("x", OdfNamespaces.Svg));
        double y = ToPoints(node.GetAttribute("y", OdfNamespaces.Svg));
        double width = ToPoints(node.GetAttribute("width", OdfNamespaces.Svg));
        double height = ToPoints(node.GetAttribute("height", OdfNamespaces.Svg));

        sb.Append("<rect");
        AppendId(node, sb, context.Options);
        AppendTransform(node, sb);
        AppendAttribute(sb, "x", FormatPoints(x));
        AppendAttribute(sb, "y", FormatPoints(y));
        AppendAttribute(sb, "width", FormatPoints(width));
        AppendAttribute(sb, "height", FormatPoints(height));
        AppendShapeStyle(node, sb, context);
        sb.Append(" />");
    }

    private static void WriteEllipse(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        double x = ToPoints(node.GetAttribute("x", OdfNamespaces.Svg));
        double y = ToPoints(node.GetAttribute("y", OdfNamespaces.Svg));
        double width = ToPoints(node.GetAttribute("width", OdfNamespaces.Svg));
        double height = ToPoints(node.GetAttribute("height", OdfNamespaces.Svg));

        sb.Append("<ellipse");
        AppendId(node, sb, context.Options);
        AppendTransform(node, sb);
        AppendAttribute(sb, "cx", FormatPoints(x + (width / 2)));
        AppendAttribute(sb, "cy", FormatPoints(y + (height / 2)));
        AppendAttribute(sb, "rx", FormatPoints(width / 2));
        AppendAttribute(sb, "ry", FormatPoints(height / 2));
        AppendShapeStyle(node, sb, context);
        sb.Append(" />");
    }

    private static void WriteLine(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        sb.Append("<line");
        AppendId(node, sb, context.Options);
        AppendTransform(node, sb);
        AppendAttribute(sb, "x1", FormatPoints(ToPoints(node.GetAttribute("x1", OdfNamespaces.Svg))));
        AppendAttribute(sb, "y1", FormatPoints(ToPoints(node.GetAttribute("y1", OdfNamespaces.Svg))));
        AppendAttribute(sb, "x2", FormatPoints(ToPoints(node.GetAttribute("x2", OdfNamespaces.Svg))));
        AppendAttribute(sb, "y2", FormatPoints(ToPoints(node.GetAttribute("y2", OdfNamespaces.Svg))));
        AppendLineStyle(node, sb, context);
        sb.Append(" />");
    }

    private static void WritePath(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        string? pathData = node.GetAttribute("d", OdfNamespaces.Svg);
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return;
        }

        double x = ToPoints(node.GetAttribute("x", OdfNamespaces.Svg));
        double y = ToPoints(node.GetAttribute("y", OdfNamespaces.Svg));
        double width = ToPoints(node.GetAttribute("width", OdfNamespaces.Svg));
        double height = ToPoints(node.GetAttribute("height", OdfNamespaces.Svg));
        string viewBox = node.GetAttribute("viewBox", OdfNamespaces.Svg) ?? "0 0 1000 1000";

        sb.Append("<svg");
        AppendAttribute(sb, "x", FormatPoints(x));
        AppendAttribute(sb, "y", FormatPoints(y));
        AppendAttribute(sb, "width", FormatPoints(width));
        AppendAttribute(sb, "height", FormatPoints(height));
        AppendAttribute(sb, "viewBox", viewBox);
        sb.Append("><path");
        AppendId(node, sb, context.Options);
        AppendTransform(node, sb);
        AppendAttribute(sb, "d", pathData!);
        AppendShapeStyle(node, sb, context);
        sb.Append(" /></svg>");
    }

    private static void WriteCustomShape(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        OdfNode? geometry = FindChild(node, "enhanced-geometry", OdfNamespaces.Draw);
        string? pathData = geometry?.GetAttribute("enhanced-path", OdfNamespaces.Draw);
        if (geometry is null ||
            string.IsNullOrWhiteSpace(pathData) ||
            !TryResolveEnhancedPath(geometry, pathData!, out string resolvedPath))
        {
            WriteRect(node, sb, context);
            return;
        }

        double x = ToPoints(node.GetAttribute("x", OdfNamespaces.Svg));
        double y = ToPoints(node.GetAttribute("y", OdfNamespaces.Svg));
        double width = ToPoints(node.GetAttribute("width", OdfNamespaces.Svg));
        double height = ToPoints(node.GetAttribute("height", OdfNamespaces.Svg));
        string viewBox = geometry.GetAttribute("viewBox", OdfNamespaces.Svg) ?? $"0 0 {FormatPoints(width)} {FormatPoints(height)}";

        sb.Append("<svg");
        AppendAttribute(sb, "x", FormatPoints(x));
        AppendAttribute(sb, "y", FormatPoints(y));
        AppendAttribute(sb, "width", FormatPoints(width));
        AppendAttribute(sb, "height", FormatPoints(height));
        AppendAttribute(sb, "viewBox", viewBox);
        sb.Append("><path");
        AppendId(node, sb, context.Options);
        AppendTransform(node, sb);
        AppendAttribute(sb, "d", resolvedPath);
        AppendShapeStyle(node, sb, context);
        sb.Append(" /></svg>");
    }

    private static void WritePointShape(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        string? points = node.GetAttribute("points", OdfNamespaces.Draw);
        if (string.IsNullOrWhiteSpace(points))
        {
            return;
        }

        string svgName = node.LocalName == "polyline" ? "polyline" : "polygon";
        double x = ToPoints(node.GetAttribute("x", OdfNamespaces.Svg));
        double y = ToPoints(node.GetAttribute("y", OdfNamespaces.Svg));
        double width = ToPoints(node.GetAttribute("width", OdfNamespaces.Svg));
        double height = ToPoints(node.GetAttribute("height", OdfNamespaces.Svg));
        string viewBox = node.GetAttribute("viewBox", OdfNamespaces.Svg) ?? $"0 0 {FormatPoints(width)} {FormatPoints(height)}";

        sb.Append("<svg");
        AppendAttribute(sb, "x", FormatPoints(x));
        AppendAttribute(sb, "y", FormatPoints(y));
        AppendAttribute(sb, "width", FormatPoints(width));
        AppendAttribute(sb, "height", FormatPoints(height));
        AppendAttribute(sb, "viewBox", viewBox);
        sb.Append("><").Append(svgName);
        AppendId(node, sb, context.Options);
        AppendTransform(node, sb);
        AppendAttribute(sb, "points", points!);
        AppendShapeStyle(node, sb, context);
        sb.Append(" /></svg>");
    }

    private static void WriteFrame(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        OdfNode? textBox = FindChild(node, "text-box", OdfNamespaces.Draw);
        if (textBox is not null)
        {
            WriteTextBox(node, textBox, sb, context);
            return;
        }

        OdfNode? image = FindChild(node, "image", OdfNamespaces.Draw);
        if (image is not null)
        {
            WriteImage(node, image, sb, context);
        }
    }

    private static void WriteTextBox(OdfNode frame, OdfNode textBox, StringBuilder sb, SvgExportContext context)
    {
        double x = ToPoints(frame.GetAttribute("x", OdfNamespaces.Svg));
        double y = ToPoints(frame.GetAttribute("y", OdfNamespaces.Svg));
        double width = ToPoints(frame.GetAttribute("width", OdfNamespaces.Svg));
        double height = ToPoints(frame.GetAttribute("height", OdfNamespaces.Svg));
        IReadOnlyList<TextParagraph> paragraphs = ReadTextParagraphs(textBox, context.Document);
        ClipDefinition? clip = WriteClipDefinition(
            frame,
            frame.GetAttribute("clip", OdfNamespaces.Fo) ?? textBox.GetAttribute("clip", OdfNamespaces.Fo),
            x,
            y,
            width,
            height,
            sb,
            context);

        sb.Append("<g");
        AppendId(frame, sb, context.Options);
        AppendTransform(frame, sb);
        AppendOptionalAttribute(sb, "clip-path", clip is null ? null : "url(#" + clip.Id + ")");
        sb.Append("><rect");
        AppendAttribute(sb, "x", FormatPoints(x));
        AppendAttribute(sb, "y", FormatPoints(y));
        AppendAttribute(sb, "width", FormatPoints(width));
        AppendAttribute(sb, "height", FormatPoints(height));
        AppendShapeStyle(frame, sb, context);
        sb.Append(" /><text");
        AppendAttribute(sb, "x", FormatPoints(x + 4));
        AppendAttribute(sb, "y", FormatPoints(y + 16));
        AppendAttribute(sb, "font-family", "sans-serif");
        if (paragraphs.Count == 1 && paragraphs[0].Runs.Count == 1)
        {
            AppendTextStyle(paragraphs[0].Runs[0].Style, sb);
            sb.Append('>').Append(WebUtility.HtmlEncode(paragraphs[0].Runs[0].Text)).Append("</text></g>");
            return;
        }

        AppendAttribute(sb, "font-size", "12");
        sb.Append('>');
        for (int paragraphIndex = 0; paragraphIndex < paragraphs.Count; paragraphIndex++)
        {
            TextParagraph paragraph = paragraphs[paragraphIndex];
            double dy = paragraphIndex == 0 ? 0 : 16;
            foreach (TextRun run in paragraph.Runs)
            {
                sb.Append("<tspan");
                if (dy > 0)
                {
                    AppendAttribute(sb, "x", FormatPoints(x + 4));
                    AppendAttribute(sb, "dy", FormatPoints(dy));
                    dy = 0;
                }

                AppendTextStyle(run.Style, sb);
                sb.Append('>').Append(WebUtility.HtmlEncode(run.Text)).Append("</tspan>");
            }
        }

        sb.Append("</text></g>");
    }

    private static IReadOnlyList<TextParagraph> ReadTextParagraphs(OdfNode textBox, DrawingDocument document)
    {
        var paragraphs = new List<TextParagraph>();
        foreach (OdfNode child in textBox.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "p" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                paragraphs.Add(ReadTextParagraph(child, document));
            }
        }

        if (paragraphs.Count == 0)
        {
            paragraphs.Add(new TextParagraph([new TextRun(textBox.TextContent ?? string.Empty, TextStyle.Empty)]));
        }

        return paragraphs;
    }

    private static TextParagraph ReadTextParagraph(OdfNode paragraph, DrawingDocument document)
    {
        var runs = new List<TextRun>();
        TextStyle paragraphStyle = GetTextStyle(paragraph, document, "paragraph");
        foreach (OdfNode child in paragraph.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                if (!string.IsNullOrEmpty(child.TextContent))
                {
                    runs.Add(new TextRun(child.TextContent, paragraphStyle));
                }
            }
            else if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "span" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                runs.Add(new TextRun(child.TextContent, paragraphStyle.Merge(GetTextStyle(child, document, "text"))));
            }
        }

        if (runs.Count == 0)
        {
            runs.Add(new TextRun(paragraph.TextContent ?? string.Empty, paragraphStyle));
        }

        return new TextParagraph(runs);
    }

    private static TextStyle GetTextStyle(OdfNode node, DrawingDocument document, string family)
    {
        string? styleName = node.GetAttribute("style-name", OdfNamespaces.Text);
        if (styleName is not { Length: > 0 })
        {
            return TextStyle.Empty;
        }

        string name = styleName.Trim();
        if (name.Length == 0)
        {
            return TextStyle.Empty;
        }

        return new TextStyle(
            document.StyleEngine.GetStyleProperty(name, "font-weight", OdfNamespaces.Fo, family),
            document.StyleEngine.GetStyleProperty(name, "font-style", OdfNamespaces.Fo, family),
            document.StyleEngine.GetStyleProperty(name, "font-size", OdfNamespaces.Fo, family),
            NormalizeColor(document.StyleEngine.GetStyleProperty(name, "color", OdfNamespaces.Fo, family)));
    }

    private static void AppendTextStyle(TextStyle style, StringBuilder sb)
    {
        AppendOptionalAttribute(sb, "font-weight", style.FontWeight);
        AppendOptionalAttribute(sb, "font-style", style.FontStyle);
        AppendAttribute(sb, "font-size", FormatFontSize(style.FontSize));
        AppendOptionalAttribute(sb, "fill", style.ColorHex is null ? null : "#" + style.ColorHex);
    }

    private static void WriteImage(OdfNode frame, OdfNode image, StringBuilder sb, SvgExportContext context)
    {
        string? href = image.GetAttribute("href", OdfNamespaces.XLink);
        if (string.IsNullOrWhiteSpace(href))
        {
            return;
        }

        double x = ToPoints(frame.GetAttribute("x", OdfNamespaces.Svg));
        double y = ToPoints(frame.GetAttribute("y", OdfNamespaces.Svg));
        double width = ToPoints(frame.GetAttribute("width", OdfNamespaces.Svg));
        double height = ToPoints(frame.GetAttribute("height", OdfNamespaces.Svg));
        ClipDefinition? clip = WriteClipDefinition(
            frame,
            image.GetAttribute("clip", OdfNamespaces.Fo) ?? frame.GetAttribute("clip", OdfNamespaces.Fo),
            x,
            y,
            width,
            height,
            sb,
            context);

        sb.Append("<image");
        AppendId(frame, sb, context.Options);
        AppendTransform(frame, sb);
        AppendAttribute(sb, "x", FormatPoints(x));
        AppendAttribute(sb, "y", FormatPoints(y));
        AppendAttribute(sb, "width", FormatPoints(width));
        AppendAttribute(sb, "height", FormatPoints(height));
        AppendOptionalAttribute(sb, "clip-path", clip is null ? null : "url(#" + clip.Id + ")");
        string resolvedHref = ResolveImageHref(href!, context);
        AppendAttribute(sb, "href", resolvedHref);
        AppendAttribute(sb, "xlink:href", resolvedHref);
        sb.Append(" />");
    }

    private static ClipDefinition? WriteClipDefinition(
        OdfNode frame,
        string? clipValue,
        double frameX,
        double frameY,
        double frameWidth,
        double frameHeight,
        StringBuilder sb,
        SvgExportContext context)
    {
        ClipRectangle? clip = TryReadClipRectangle(clipValue, frameX, frameY);
        if (clip is not null)
        {
            string rectangleClipId = context.CreateClipId();
            sb.Append("<defs><clipPath");
            AppendAttribute(sb, "id", rectangleClipId);
            sb.Append("><rect");
            AppendAttribute(sb, "x", FormatPoints(clip.X));
            AppendAttribute(sb, "y", FormatPoints(clip.Y));
            AppendAttribute(sb, "width", FormatPoints(clip.Width));
            AppendAttribute(sb, "height", FormatPoints(clip.Height));
            sb.Append(" /></clipPath></defs>");
            return new ClipDefinition(rectangleClipId);
        }

        OdfNode? contour = FindChild(frame, "contour-path", OdfNamespaces.Draw) ??
            FindChild(frame, "contour-polygon", OdfNamespaces.Draw);
        if (contour is null)
        {
            return null;
        }

        string? contourMarkup = CreateContourClipShape(contour, frameX, frameY, frameWidth, frameHeight);
        if (contourMarkup is null)
        {
            return null;
        }

        string contourClipId = context.CreateClipId();
        sb.Append("<defs><clipPath");
        AppendAttribute(sb, "id", contourClipId);
        sb.Append('>').Append(contourMarkup).Append("</clipPath></defs>");
        return new ClipDefinition(contourClipId);
    }

    private static string? CreateContourClipShape(
        OdfNode contour,
        double frameX,
        double frameY,
        double frameWidth,
        double frameHeight)
    {
        ViewBox viewBox = ReadViewBox(contour.GetAttribute("viewBox", OdfNamespaces.Svg), frameWidth, frameHeight);
        string transform = CreateViewBoxTransform(viewBox, frameX, frameY, frameWidth, frameHeight);
        if (contour.LocalName == "contour-path")
        {
            string? pathData = contour.GetAttribute("d", OdfNamespaces.Svg);
            if (string.IsNullOrWhiteSpace(pathData) || !IsSvgCompatibleEnhancedPath(pathData!))
            {
                return null;
            }

            var pathBuilder = new StringBuilder();
            pathBuilder.Append("<path");
            AppendAttribute(pathBuilder, "d", pathData!);
            AppendAttribute(pathBuilder, "transform", transform);
            pathBuilder.Append(" />");
            return pathBuilder.ToString();
        }

        string? points = contour.GetAttribute("points", OdfNamespaces.Draw);
        if (string.IsNullOrWhiteSpace(points))
        {
            return null;
        }

        var polygonBuilder = new StringBuilder();
        polygonBuilder.Append("<polygon");
        AppendAttribute(polygonBuilder, "points", points!);
        AppendAttribute(polygonBuilder, "transform", transform);
        polygonBuilder.Append(" />");
        return polygonBuilder.ToString();
    }

    private static ViewBox ReadViewBox(string? value, double fallbackWidth, double fallbackHeight)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ViewBox(0, 0, Math.Max(fallbackWidth, 1d), Math.Max(fallbackHeight, 1d));
        }

        string[] parts = value!.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double width) &&
            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double height) &&
            width > 0 &&
            height > 0)
        {
            return new ViewBox(x, y, width, height);
        }

        return new ViewBox(0, 0, Math.Max(fallbackWidth, 1d), Math.Max(fallbackHeight, 1d));
    }

    private static string CreateViewBoxTransform(ViewBox viewBox, double x, double y, double width, double height)
    {
        double scaleX = width / viewBox.Width;
        double scaleY = height / viewBox.Height;
        double translateX = x - (viewBox.X * scaleX);
        double translateY = y - (viewBox.Y * scaleY);
        return "translate(" +
            FormatPoints(translateX) +
            " " +
            FormatPoints(translateY) +
            ") scale(" +
            FormatPoints(scaleX) +
            " " +
            FormatPoints(scaleY) +
            ")";
    }

    private static CanvasBounds MeasurePage(OdfDrawPage page, OdfSvgExportOptions options)
    {
        var bounds = new CanvasBounds(options.DefaultWidth.ToPoints(), options.DefaultHeight.ToPoints());
        MeasureChildren(page.Node, bounds);
        return bounds;
    }

    private static void MeasureChildren(OdfNode parent, CanvasBounds bounds)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Draw)
            {
                continue;
            }

            if (child.LocalName == "g")
            {
                MeasureChildren(child, bounds);
                continue;
            }

            if (child.LocalName is "line" or "connector")
            {
                bounds.Include(ToPoints(child.GetAttribute("x1", OdfNamespaces.Svg)), ToPoints(child.GetAttribute("y1", OdfNamespaces.Svg)));
                bounds.Include(ToPoints(child.GetAttribute("x2", OdfNamespaces.Svg)), ToPoints(child.GetAttribute("y2", OdfNamespaces.Svg)));
            }
            else
            {
                double x = ToPoints(child.GetAttribute("x", OdfNamespaces.Svg));
                double y = ToPoints(child.GetAttribute("y", OdfNamespaces.Svg));
                double width = ToPoints(child.GetAttribute("width", OdfNamespaces.Svg));
                double height = ToPoints(child.GetAttribute("height", OdfNamespaces.Svg));
                bounds.Include(x + width, y + height);
            }
        }
    }

    private static OdfNode? FindChild(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }

    private static void AppendId(OdfNode node, StringBuilder sb, OdfSvgExportOptions options)
    {
        if (!options.PreserveIds)
        {
            return;
        }

        string? id = node.GetAttribute("id", OdfNamespaces.Draw);
        if (!string.IsNullOrWhiteSpace(id))
        {
            AppendAttribute(sb, "id", id!);
        }
    }

    private static void AppendDefaultShapeStyle(StringBuilder sb)
    {
        AppendAttribute(sb, "fill", "none");
        AppendAttribute(sb, "stroke", "#000000");
        AppendAttribute(sb, "stroke-width", "1");
    }

    private static void AppendDefaultLineStyle(StringBuilder sb)
    {
        AppendAttribute(sb, "stroke", "#000000");
        AppendAttribute(sb, "stroke-width", "1");
    }

    private static void AppendShapeStyle(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        string? fillMode = node.GetAttribute("fill", OdfNamespaces.Draw);
        string? fillColor = node.GetAttribute("fill-color", OdfNamespaces.Draw);
        string? strokeMode = node.GetAttribute("stroke", OdfNamespaces.Draw);
        string? strokeColor = node.GetAttribute("stroke-color", OdfNamespaces.Svg);

        if (string.IsNullOrWhiteSpace(fillColor) || string.IsNullOrWhiteSpace(strokeColor))
        {
            var shape = new OdfShape(node, context.Document);
            fillColor ??= shape.FillColor;
            strokeColor ??= shape.StrokeColor;
        }

        string? gradientFill = context.TryGetGradientFill(node, out string? svgGradientFill) ? svgGradientFill : null;
        AppendAttribute(sb, "fill", string.Equals(fillMode, "none", StringComparison.Ordinal) ? "none" : gradientFill ?? fillColor ?? "none");
        AppendAttribute(sb, "stroke", string.Equals(strokeMode, "none", StringComparison.Ordinal) ? "none" : strokeColor ?? "#000000");
        AppendStrokeWidth(node, sb);
        AppendStrokePresentation(node, sb);
        AppendMarkers(node, sb, context);
        AppendOptionalAttribute(sb, "fill-rule", node.GetAttribute("fill-rule", OdfNamespaces.Svg));
        AppendOpacity(node, sb);
    }

    private static void AppendLineStyle(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        string? strokeMode = node.GetAttribute("stroke", OdfNamespaces.Draw);
        string? strokeColor = node.GetAttribute("stroke-color", OdfNamespaces.Svg);

        if (string.IsNullOrWhiteSpace(strokeColor))
        {
            strokeColor = new OdfShape(node, context.Document).StrokeColor;
        }

        AppendAttribute(sb, "stroke", string.Equals(strokeMode, "none", StringComparison.Ordinal) ? "none" : strokeColor ?? "#000000");
        AppendStrokeWidth(node, sb);
        AppendStrokePresentation(node, sb);
        AppendMarkers(node, sb, context);
        AppendOpacity(node, sb);
    }

    private static void AppendStrokeWidth(OdfNode node, StringBuilder sb)
    {
        double strokeWidth = ToPoints(node.GetAttribute("stroke-width", OdfNamespaces.Svg));
        AppendAttribute(sb, "stroke-width", strokeWidth > 0 ? FormatPoints(strokeWidth) : "1");
    }

    private static void AppendOpacity(OdfNode node, StringBuilder sb)
    {
        AppendOptionalAttribute(sb, "opacity", NormalizeOpacity(node.GetAttribute("opacity", OdfNamespaces.Draw)));
        AppendOptionalAttribute(sb, "fill-opacity", NormalizeOpacity(node.GetAttribute("fill-opacity", OdfNamespaces.Draw)));
        AppendOptionalAttribute(sb, "stroke-opacity", NormalizeOpacity(node.GetAttribute("stroke-opacity", OdfNamespaces.Svg)));
    }

    private static void AppendStrokePresentation(OdfNode node, StringBuilder sb)
    {
        AppendOptionalAttribute(sb, "stroke-linecap", node.GetAttribute("stroke-linecap", OdfNamespaces.Svg));
        AppendOptionalAttribute(sb, "stroke-linejoin", node.GetAttribute("stroke-linejoin", OdfNamespaces.Draw));
    }

    private static void AppendMarkers(OdfNode node, StringBuilder sb, SvgExportContext context)
    {
        AppendOptionalAttribute(sb, "marker-start", context.TryGetMarkerUrl(node, "marker-start", out string? markerStart) ? markerStart : null);
        AppendOptionalAttribute(sb, "marker-end", context.TryGetMarkerUrl(node, "marker-end", out string? markerEnd) ? markerEnd : null);
    }

    private static void AppendTransform(OdfNode node, StringBuilder sb)
    {
        string? transform = node.GetAttribute("transform", OdfNamespaces.Draw);
        if (!string.IsNullOrWhiteSpace(transform))
        {
            AppendAttribute(sb, "transform", transform!);
        }
    }

    private static string ResolveImageHref(string href, SvgExportContext context)
    {
        if (!context.Options.EmbedPackageImagesAsDataUri ||
            href.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            href.Contains("://", StringComparison.Ordinal))
        {
            return href;
        }

        if (!context.Document.Package.HasEntry(href))
        {
            return href;
        }

        byte[] bytes = context.Document.Package.ReadEntry(href);
        string mediaType = context.Document.Package.Manifest.TryGetValue(href, out string? declaredMediaType) &&
            !string.IsNullOrWhiteSpace(declaredMediaType)
            ? declaredMediaType!
            : GuessImageMediaType(href);

        return "data:" + mediaType + ";base64," + Convert.ToBase64String(bytes);
    }

    private static string GuessImageMediaType(string href)
    {
        string extension = Path.GetExtension(href).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            _ => "image/png",
        };
    }

    private static void AppendOptionalAttribute(StringBuilder sb, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            AppendAttribute(sb, name, value!);
        }
    }

    private static string? NormalizeOpacity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value!.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal))
        {
            string number = trimmed.Substring(0, trimmed.Length - 1);
            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            {
                double normalized = Math.Max(0, Math.Min(1, percent / 100.0));
                return FormatPoints(normalized);
            }
        }

        return trimmed;
    }

    private static void AppendAttribute(StringBuilder sb, string name, string value)
    {
        sb.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
    }

    private static double ToPoints(string? value)
    {
        return OdfLength.TryParse(value, out OdfLength length)
            ? length.ToPoints()
            : 0;
    }

    private static string FormatPoints(double value)
    {
        return Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(double value)
    {
        return FormatPoints(value) + "%";
    }

    private static string FormatFontSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "12";
        }

        return OdfLength.TryParse(value, out OdfLength length)
            ? FormatPoints(length.ToPoints())
            : value!.Trim();
    }

    private static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string color = value!.Trim();
        if (color.StartsWith("#", StringComparison.Ordinal))
        {
            color = color.Substring(1);
        }

        return color.Length == 6 ? color.ToUpperInvariant() : null;
    }

    private static ClipRectangle? TryReadClipRectangle(string? value, double frameX, double frameY)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string clip = value!.Trim();
        if (!clip.StartsWith("rect(", StringComparison.OrdinalIgnoreCase) ||
            !clip.EndsWith(")", StringComparison.Ordinal))
        {
            return null;
        }

        string[] parts = clip.Substring(5, clip.Length - 6).Split(',');
        if (parts.Length != 4 ||
            !TryReadClipLength(parts[0], out double top) ||
            !TryReadClipLength(parts[1], out double right) ||
            !TryReadClipLength(parts[2], out double bottom) ||
            !TryReadClipLength(parts[3], out double left) ||
            right <= left ||
            bottom <= top)
        {
            return null;
        }

        return new ClipRectangle(string.Empty, frameX + left, frameY + top, right - left, bottom - top);
    }

    private static bool TryReadClipLength(string value, out double points)
    {
        string trimmed = value.Trim();
        if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase))
        {
            points = 0;
            return false;
        }

        if (OdfLength.TryParse(trimmed, out OdfLength length))
        {
            points = length.ToPoints();
            return true;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out points);
    }

    private static bool IsSvgCompatibleEnhancedPath(string pathData)
    {
        foreach (char c in pathData)
        {
            if (!(char.IsWhiteSpace(c) ||
                char.IsDigit(c) ||
                c is 'M' or 'm' or 'L' or 'l' or 'H' or 'h' or 'V' or 'v' or 'C' or 'c' or 'S' or 's' or 'Q' or 'q' or 'T' or 't' or 'A' or 'a' or 'Z' or 'z' or '.' or ',' or '-' or '+' or 'E' or 'e'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveEnhancedPath(OdfNode geometry, string pathData, out string resolvedPath)
    {
        resolvedPath = pathData;
        if (IsSvgCompatibleEnhancedPath(pathData))
        {
            return true;
        }

        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        string? modifiers = geometry.GetAttribute("modifiers", OdfNamespaces.Draw);
        if (!string.IsNullOrWhiteSpace(modifiers))
        {
            string[] parts = modifiers!.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    return false;
                }

                values["$" + i.ToString(CultureInfo.InvariantCulture)] = value;
            }
        }

        foreach (OdfNode child in geometry.Children)
        {
            if (child.LocalName != "equation" || child.NamespaceUri != OdfNamespaces.Draw)
            {
                continue;
            }

            string? name = child.GetAttribute("name", OdfNamespaces.Draw);
            string? formula = child.GetAttribute("formula", OdfNamespaces.Draw);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(formula))
            {
                continue;
            }

            var parser = new EnhancedPathFormulaParser(formula!, values);
            if (!parser.TryEvaluate(out double value))
            {
                return false;
            }

            values["?" + name!.Trim()] = value;
        }

        var sb = new StringBuilder(pathData.Length);
        for (int i = 0; i < pathData.Length; i++)
        {
            char c = pathData[i];
            if (c is '$' or '?')
            {
                int start = i;
                i++;
                while (i < pathData.Length && (char.IsLetterOrDigit(pathData[i]) || pathData[i] == '_' || pathData[i] == '-'))
                {
                    i++;
                }

                string token = pathData.Substring(start, i - start);
                if (!values.TryGetValue(token, out double value))
                {
                    return false;
                }

                sb.Append(FormatPoints(value));
                i--;
                continue;
            }

            sb.Append(c);
        }

        string substitutedPath = StripEnhancedPathStateCommands(sb.ToString());
        if (!TryTranslateEnhancedPath(substitutedPath, out resolvedPath))
        {
            return false;
        }

        return IsSvgCompatibleEnhancedPath(resolvedPath);
    }

    private static bool TryTranslateEnhancedPath(string pathData, out string svgPath)
    {
        svgPath = string.Empty;
        if (!TryTokenizeEnhancedPath(pathData, out List<EnhancedPathToken> tokens))
        {
            return false;
        }

        var sb = new StringBuilder(pathData.Length + 32);
        int index = 0;
        char command = '\0';
        bool commandIsRelative = false;
        double currentX = 0;
        double currentY = 0;
        double subpathX = 0;
        double subpathY = 0;
        bool hasCurrentPoint = false;
        bool hasLastCubicControlPoint = false;
        double lastCubicControlX = 0;
        double lastCubicControlY = 0;

        while (index < tokens.Count)
        {
            if (tokens[index].IsCommand)
            {
                char rawCommand = tokens[index].Command;
                command = char.ToUpperInvariant(rawCommand);
                commandIsRelative = char.IsLower(rawCommand);
                index++;
            }
            else if (command == '\0')
            {
                return false;
            }

            switch (command)
            {
                case 'M':
                    if (!TryReadNumber(tokens, ref index, out double moveX) ||
                        !TryReadNumber(tokens, ref index, out double moveY))
                    {
                        return false;
                    }

                    currentX = commandIsRelative && hasCurrentPoint ? currentX + moveX : moveX;
                    currentY = commandIsRelative && hasCurrentPoint ? currentY + moveY : moveY;
                    AppendSvgCommand(sb, "M", currentX, currentY);
                    subpathX = currentX;
                    subpathY = currentY;
                    hasCurrentPoint = true;
                    hasLastCubicControlPoint = false;
                    command = 'L';
                    break;
                case 'L':
                    if (!TryReadNumber(tokens, ref index, out double lineX) ||
                        !TryReadNumber(tokens, ref index, out double lineY))
                    {
                        return false;
                    }

                    currentX = commandIsRelative ? currentX + lineX : lineX;
                    currentY = commandIsRelative ? currentY + lineY : lineY;
                    AppendSvgCommand(sb, "L", currentX, currentY);
                    hasCurrentPoint = true;
                    hasLastCubicControlPoint = false;
                    break;
                case 'H':
                    if (!hasCurrentPoint ||
                        !TryReadNumber(tokens, ref index, out double horizontalX))
                    {
                        return false;
                    }

                    currentX = commandIsRelative ? currentX + horizontalX : horizontalX;
                    AppendSvgCommand(sb, "H", currentX);
                    hasLastCubicControlPoint = false;
                    break;
                case 'C':
                    if (!TryReadNumbers(tokens, ref index, 6, out double[] cubic))
                    {
                        return false;
                    }

                    if (commandIsRelative)
                    {
                        OffsetCoordinatePairs(cubic, currentX, currentY);
                    }

                    AppendSvgCommand(sb, "C", cubic);
                    lastCubicControlX = cubic[2];
                    lastCubicControlY = cubic[3];
                    currentX = cubic[4];
                    currentY = cubic[5];
                    hasCurrentPoint = true;
                    hasLastCubicControlPoint = true;
                    break;
                case 'S':
                    if (!hasCurrentPoint ||
                        !TryReadNumbers(tokens, ref index, 4, out double[] smoothCubic))
                    {
                        return false;
                    }

                    if (commandIsRelative)
                    {
                        OffsetCoordinatePairs(smoothCubic, currentX, currentY);
                    }

                    double reflectedControlX = hasLastCubicControlPoint
                        ? (2 * currentX) - lastCubicControlX
                        : currentX;
                    double reflectedControlY = hasLastCubicControlPoint
                        ? (2 * currentY) - lastCubicControlY
                        : currentY;
                    AppendSvgCommand(
                        sb,
                        "C",
                        reflectedControlX,
                        reflectedControlY,
                        smoothCubic[0],
                        smoothCubic[1],
                        smoothCubic[2],
                        smoothCubic[3]);
                    lastCubicControlX = smoothCubic[0];
                    lastCubicControlY = smoothCubic[1];
                    currentX = smoothCubic[2];
                    currentY = smoothCubic[3];
                    hasCurrentPoint = true;
                    hasLastCubicControlPoint = true;
                    break;
                case 'Q':
                    if (!TryReadNumbers(tokens, ref index, 4, out double[] quadratic))
                    {
                        return false;
                    }

                    if (commandIsRelative)
                    {
                        OffsetCoordinatePairs(quadratic, currentX, currentY);
                    }

                    AppendSvgCommand(sb, "Q", quadratic);
                    currentX = quadratic[2];
                    currentY = quadratic[3];
                    hasCurrentPoint = true;
                    hasLastCubicControlPoint = false;
                    break;
                case 'Z':
                    AppendSvgCommand(sb, "Z");
                    currentX = subpathX;
                    currentY = subpathY;
                    hasCurrentPoint = true;
                    hasLastCubicControlPoint = false;
                    command = '\0';
                    break;
                case 'A':
                case 'B':
                case 'V':
                case 'W':
                    if (command == 'V' && CountAvailableNumbers(tokens, index) == 1)
                    {
                        if (!hasCurrentPoint ||
                            !TryReadNumber(tokens, ref index, out double verticalY))
                        {
                            return false;
                        }

                        currentY = commandIsRelative ? currentY + verticalY : verticalY;
                        AppendSvgCommand(sb, "V", currentY);
                        hasLastCubicControlPoint = false;
                        break;
                    }

                    if (!TryReadNumbers(tokens, ref index, 8, out double[] arcBox))
                    {
                        return false;
                    }

                    bool moveToArcStart = command is 'B' or 'V';
                    bool clockwise = command is 'V' or 'W';
                    if (!AppendBoundingBoxArc(sb, arcBox, moveToArcStart, clockwise, ref currentX, ref currentY, ref hasCurrentPoint))
                    {
                        return false;
                    }

                    if (moveToArcStart)
                    {
                        subpathX = currentX;
                        subpathY = currentY;
                    }

                    hasLastCubicControlPoint = false;
                    break;
                case 'G':
                    if (!hasCurrentPoint ||
                        !TryReadNumbers(tokens, ref index, 4, out double[] angleArcTo))
                    {
                        return false;
                    }

                    if (!AppendCurrentPointAngleArc(sb, angleArcTo, currentX, currentY, ref currentX, ref currentY))
                    {
                        return false;
                    }

                    hasLastCubicControlPoint = false;
                    break;
                case 'T':
                case 'U':
                    if (!TryReadNumbers(tokens, ref index, 6, out double[] angleEllipse))
                    {
                        return false;
                    }

                    bool moveToEllipseStart = command == 'U';
                    if (!moveToEllipseStart && !hasCurrentPoint)
                    {
                        return false;
                    }

                    if (!AppendAngleEllipseArc(sb, angleEllipse, moveToEllipseStart, ref currentX, ref currentY))
                    {
                        return false;
                    }

                    hasCurrentPoint = true;
                    if (moveToEllipseStart)
                    {
                        subpathX = currentX;
                        subpathY = currentY;
                    }

                    hasLastCubicControlPoint = false;
                    break;
                case 'X':
                case 'Y':
                    if (!hasCurrentPoint ||
                        !TryReadNumbers(tokens, ref index, 2, out double[] quadrant))
                    {
                        return false;
                    }

                    AppendQuadrantArc(sb, command, currentX, currentY, quadrant[0], quadrant[1]);
                    currentX = quadrant[0];
                    currentY = quadrant[1];
                    hasLastCubicControlPoint = false;
                    break;
                case 'F':
                case 'N':
                    command = '\0';
                    hasLastCubicControlPoint = false;
                    break;
                default:
                    return false;
            }
        }

        svgPath = sb.ToString().Trim();
        return svgPath.Length > 0;
    }

    private static bool TryTokenizeEnhancedPath(string pathData, out List<EnhancedPathToken> tokens)
    {
        tokens = [];
        for (int i = 0; i < pathData.Length;)
        {
            char c = pathData[i];
            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
                continue;
            }

            if (char.IsLetter(c))
            {
                tokens.Add(EnhancedPathToken.ForCommand(c));
                i++;
                continue;
            }

            int start = i;
            if (c is '+' or '-')
            {
                i++;
            }

            bool hasDigits = false;
            while (i < pathData.Length && char.IsDigit(pathData[i]))
            {
                hasDigits = true;
                i++;
            }

            if (i < pathData.Length && pathData[i] == '.')
            {
                i++;
                while (i < pathData.Length && char.IsDigit(pathData[i]))
                {
                    hasDigits = true;
                    i++;
                }
            }

            if (!hasDigits)
            {
                return false;
            }

            if (i < pathData.Length && pathData[i] is 'e' or 'E')
            {
                int exponentStart = i;
                i++;
                if (i < pathData.Length && pathData[i] is '+' or '-')
                {
                    i++;
                }

                bool hasExponentDigits = false;
                while (i < pathData.Length && char.IsDigit(pathData[i]))
                {
                    hasExponentDigits = true;
                    i++;
                }

                if (!hasExponentDigits)
                {
                    i = exponentStart;
                }
            }

            string numberText = pathData.Substring(start, i - start);
            if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                !IsFinite(value))
            {
                return false;
            }

            tokens.Add(EnhancedPathToken.ForNumber(value));
        }

        return tokens.Count > 0;
    }

    private static bool TryReadNumber(IReadOnlyList<EnhancedPathToken> tokens, ref int index, out double value)
    {
        value = 0;
        if (index >= tokens.Count || tokens[index].IsCommand)
        {
            return false;
        }

        value = tokens[index].Number;
        index++;
        return true;
    }

    private static bool TryReadNumbers(IReadOnlyList<EnhancedPathToken> tokens, ref int index, int count, out double[] values)
    {
        values = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (!TryReadNumber(tokens, ref index, out values[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void OffsetCoordinatePairs(double[] values, double offsetX, double offsetY)
    {
        for (int i = 0; i + 1 < values.Length; i += 2)
        {
            values[i] += offsetX;
            values[i + 1] += offsetY;
        }
    }

    private static int CountAvailableNumbers(IReadOnlyList<EnhancedPathToken> tokens, int index)
    {
        int count = 0;
        for (int i = index; i < tokens.Count && !tokens[i].IsCommand; i++)
        {
            count++;
        }

        return count;
    }

    private static bool AppendBoundingBoxArc(
        StringBuilder sb,
        IReadOnlyList<double> values,
        bool moveToStart,
        bool clockwise,
        ref double currentX,
        ref double currentY,
        ref bool hasCurrentPoint)
    {
        double left = values[0];
        double top = values[1];
        double right = values[2];
        double bottom = values[3];
        double radiusX = Math.Abs(right - left) / 2d;
        double radiusY = Math.Abs(bottom - top) / 2d;
        if (radiusX <= 0 || radiusY <= 0)
        {
            return false;
        }

        double centerX = (left + right) / 2d;
        double centerY = (top + bottom) / 2d;
        double startAngle = AngleFromRadialPoint(centerX, centerY, radiusX, radiusY, values[4], values[5]);
        double endAngle = AngleFromRadialPoint(centerX, centerY, radiusX, radiusY, values[6], values[7]);
        (double startX, double startY) = PointOnEllipse(centerX, centerY, radiusX, radiusY, startAngle);
        (double endX, double endY) = PointOnEllipse(centerX, centerY, radiusX, radiusY, endAngle);

        if (moveToStart)
        {
            AppendSvgCommand(sb, "M", startX, startY);
        }
        else
        {
            if (!hasCurrentPoint)
            {
                return false;
            }

            AppendSvgCommand(sb, "L", startX, startY);
        }

        AppendSvgArc(sb, radiusX, radiusY, clockwise, ArcDelta(startAngle, endAngle, clockwise), endX, endY);
        currentX = endX;
        currentY = endY;
        hasCurrentPoint = true;
        return true;
    }

    private static bool AppendCurrentPointAngleArc(
        StringBuilder sb,
        IReadOnlyList<double> values,
        double startX,
        double startY,
        ref double currentX,
        ref double currentY)
    {
        double radiusX = Math.Abs(values[0]);
        double radiusY = Math.Abs(values[1]);
        if (radiusX <= 0 || radiusY <= 0)
        {
            return false;
        }

        double startAngle = values[2];
        double swing = values[3];
        double centerX = startX - (radiusX * Math.Cos(ToRadians(startAngle)));
        double centerY = startY - (radiusY * Math.Sin(ToRadians(startAngle)));
        double endAngle = startAngle + swing;
        (double endX, double endY) = PointOnEllipse(centerX, centerY, radiusX, radiusY, endAngle);
        AppendSvgArc(sb, radiusX, radiusY, swing >= 0, Math.Abs(swing), endX, endY);
        currentX = endX;
        currentY = endY;
        return true;
    }

    private static bool AppendAngleEllipseArc(
        StringBuilder sb,
        IReadOnlyList<double> values,
        bool moveToStart,
        ref double currentX,
        ref double currentY)
    {
        double centerX = values[0];
        double centerY = values[1];
        double radiusX = Math.Abs(values[2]);
        double radiusY = Math.Abs(values[3]);
        if (radiusX <= 0 || radiusY <= 0)
        {
            return false;
        }

        double startAngle = values[4];
        double endAngle = values[5];
        (double startX, double startY) = PointOnEllipse(centerX, centerY, radiusX, radiusY, startAngle);
        (double endX, double endY) = PointOnEllipse(centerX, centerY, radiusX, radiusY, endAngle);
        if (moveToStart)
        {
            AppendSvgCommand(sb, "M", startX, startY);
        }
        else
        {
            AppendSvgCommand(sb, "L", startX, startY);
        }

        double rawDelta = endAngle - startAngle;
        if (IsFullEllipseArc(rawDelta))
        {
            bool clockwise = rawDelta >= 0;
            AppendFullEllipseArc(sb, centerX, centerY, radiusX, radiusY, startAngle, clockwise);
            currentX = startX;
            currentY = startY;
            return true;
        }

        AppendSvgArc(sb, radiusX, radiusY, clockwise: true, ArcDelta(startAngle, endAngle, clockwise: true), endX, endY);
        currentX = endX;
        currentY = endY;
        return true;
    }

    private static bool IsFullEllipseArc(double rawDelta)
        => Math.Abs(rawDelta) >= 360d &&
            Math.Abs(NormalizeDegrees(rawDelta)) < 0.000001d;

    private static void AppendFullEllipseArc(
        StringBuilder sb,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double startAngle,
        bool clockwise)
    {
        double midAngle = startAngle + (clockwise ? 180d : -180d);
        (double midX, double midY) = PointOnEllipse(centerX, centerY, radiusX, radiusY, midAngle);
        (double endX, double endY) = PointOnEllipse(centerX, centerY, radiusX, radiusY, startAngle);
        AppendSvgArc(sb, radiusX, radiusY, clockwise, 180d, midX, midY);
        AppendSvgArc(sb, radiusX, radiusY, clockwise, 180d, endX, endY);
    }

    private static void AppendQuadrantArc(
        StringBuilder sb,
        char command,
        double currentX,
        double currentY,
        double targetX,
        double targetY)
    {
        double radiusX = Math.Max(Math.Abs(targetX - currentX), 0.001d);
        double radiusY = Math.Max(Math.Abs(targetY - currentY), 0.001d);
        bool sweep = command == 'X'
            ? (targetX - currentX) * (targetY - currentY) >= 0
            : (targetX - currentX) * (targetY - currentY) < 0;
        AppendSvgArc(sb, radiusX, radiusY, sweep, 90, targetX, targetY);
    }

    private static void AppendSvgArc(
        StringBuilder sb,
        double radiusX,
        double radiusY,
        bool clockwise,
        double absoluteDelta,
        double endX,
        double endY)
    {
        int largeArcFlag = NormalizeDegrees(Math.Abs(absoluteDelta)) > 180d ? 1 : 0;
        int sweepFlag = clockwise ? 1 : 0;
        AppendSvgCommand(sb, "A", radiusX, radiusY, 0, largeArcFlag, sweepFlag, endX, endY);
    }

    private static double ArcDelta(double startAngle, double endAngle, bool clockwise)
    {
        return clockwise
            ? NormalizeDegrees(endAngle - startAngle)
            : NormalizeDegrees(startAngle - endAngle);
    }

    private static double NormalizeDegrees(double value)
    {
        double result = value % 360d;
        return result < 0 ? result + 360d : result;
    }

    private static double AngleFromRadialPoint(double centerX, double centerY, double radiusX, double radiusY, double x, double y)
        => Math.Atan2((y - centerY) / radiusY, (x - centerX) / radiusX) * 180d / Math.PI;

    private static (double X, double Y) PointOnEllipse(double centerX, double centerY, double radiusX, double radiusY, double degrees)
    {
        double radians = ToRadians(degrees);
        return (centerX + (radiusX * Math.Cos(radians)), centerY + (radiusY * Math.Sin(radians)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static void AppendSvgCommand(StringBuilder sb, string command, params double[] values)
    {
        if (sb.Length > 0)
        {
            sb.Append(' ');
        }

        sb.Append(command);
        foreach (double value in values)
        {
            sb.Append(' ').Append(FormatPoints(value));
        }
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static string StripEnhancedPathStateCommands(string pathData)
    {
        var sb = new StringBuilder(pathData.Length);
        for (int i = 0; i < pathData.Length; i++)
        {
            char c = pathData[i];
            if ((c is 'N' or 'F') &&
                IsPathCommandBoundary(pathData, i - 1) &&
                IsPathCommandBoundary(pathData, i + 1))
            {
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    private static bool IsPathCommandBoundary(string text, int index)
    {
        if (index < 0 || index >= text.Length)
        {
            return true;
        }

        char c = text[index];
        return char.IsWhiteSpace(c) || c == ',';
    }

    private sealed class EnhancedPathFormulaParser
    {
        private const int MaxFormulaLength = 1024;
        private const int MaxDepth = 32;
        private readonly string _formula;
        private readonly IReadOnlyDictionary<string, double> _values;
        private int _position;

        public EnhancedPathFormulaParser(string formula, IReadOnlyDictionary<string, double> values)
        {
            _formula = formula;
            _values = values;
        }

        public bool TryEvaluate(out double value)
        {
            value = 0;
            if (_formula.Length > MaxFormulaLength)
            {
                return false;
            }

            _position = 0;
            if (!TryParseExpression(0, out value))
            {
                return false;
            }

            SkipWhitespace();
            return _position == _formula.Length && IsFinite(value);
        }

        private bool TryParseExpression(int depth, out double value)
        {
            value = 0;
            if (depth > MaxDepth || !TryParseTerm(depth + 1, out value))
            {
                return false;
            }

            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+'))
                {
                    if (!TryParseTerm(depth + 1, out double right))
                    {
                        return false;
                    }

                    value += right;
                }
                else if (TryConsume('-'))
                {
                    if (!TryParseTerm(depth + 1, out double right))
                    {
                        return false;
                    }

                    value -= right;
                }
                else
                {
                    return IsFinite(value);
                }
            }
        }

        private bool TryParseTerm(int depth, out double value)
        {
            value = 0;
            if (depth > MaxDepth || !TryParseFactor(depth + 1, out value))
            {
                return false;
            }

            while (true)
            {
                SkipWhitespace();
                if (TryConsume('*'))
                {
                    if (!TryParseFactor(depth + 1, out double right))
                    {
                        return false;
                    }

                    value *= right;
                }
                else if (TryConsume('/'))
                {
                    if (!TryParseFactor(depth + 1, out double right) || Math.Abs(right) < double.Epsilon)
                    {
                        return false;
                    }

                    value /= right;
                }
                else
                {
                    return IsFinite(value);
                }
            }
        }

        private bool TryParseFactor(int depth, out double value)
        {
            value = 0;
            if (depth > MaxDepth)
            {
                return false;
            }

            SkipWhitespace();
            if (TryConsume('+'))
            {
                return TryParseFactor(depth + 1, out value);
            }

            if (TryConsume('-'))
            {
                if (!TryParseFactor(depth + 1, out value))
                {
                    return false;
                }

                value = -value;
                return IsFinite(value);
            }

            if (TryConsume('('))
            {
                if (!TryParseExpression(depth + 1, out value))
                {
                    return false;
                }

                SkipWhitespace();
                return TryConsume(')');
            }

            if (TryPeek('$') || TryPeek('?'))
            {
                return TryReadReference(out value);
            }

            if (TryPeekLetter())
            {
                return TryReadIdentifier(depth + 1, out value);
            }

            return TryReadNumber(out value);
        }

        private bool TryReadIdentifier(int depth, out double value)
        {
            value = 0;
            int start = _position;
            while (_position < _formula.Length && char.IsLetter(_formula[_position]))
            {
                _position++;
            }

            string name = _formula.Substring(start, _position - start);
            if (string.Equals(name, "pi", StringComparison.OrdinalIgnoreCase))
            {
                value = Math.PI;
                return true;
            }

            SkipWhitespace();
            if (!TryConsume('('))
            {
                return false;
            }

            var args = new List<double>();
            SkipWhitespace();
            if (!TryPeek(')'))
            {
                do
                {
                    if (!TryParseExpression(depth + 1, out double arg))
                    {
                        return false;
                    }

                    args.Add(arg);
                    SkipWhitespace();
                }
                while (TryConsume(','));
            }

            if (!TryConsume(')'))
            {
                return false;
            }

            return TryEvaluateFunction(name, args, out value);
        }

        private static bool TryEvaluateFunction(string name, IReadOnlyList<double> args, out double value)
        {
            value = 0;
            switch (name.ToLowerInvariant())
            {
                case "abs" when args.Count == 1:
                    value = Math.Abs(args[0]);
                    return true;
                case "sqrt" when args.Count == 1 && args[0] >= 0:
                    value = Math.Sqrt(args[0]);
                    return true;
                case "floor" when args.Count == 1:
                    value = Math.Floor(args[0]);
                    return true;
                case "ceil" when args.Count == 1:
                    value = Math.Ceiling(args[0]);
                    return true;
                case "round" when args.Count == 1:
                    value = Math.Round(args[0], MidpointRounding.AwayFromZero);
                    return true;
                case "pow" when args.Count == 2:
                    value = Math.Pow(args[0], args[1]);
                    return IsFinite(value);
                case "mod" when args.Count == 2 && Math.Abs(args[1]) >= double.Epsilon:
                    value = args[0] % args[1];
                    return IsFinite(value);
                case "sin" when args.Count == 1:
                    value = Math.Sin(args[0]);
                    return true;
                case "cos" when args.Count == 1:
                    value = Math.Cos(args[0]);
                    return true;
                case "tan" when args.Count == 1:
                    value = Math.Tan(args[0]);
                    return IsFinite(value);
                case "asin" when args.Count == 1 && args[0] >= -1 && args[0] <= 1:
                    value = Math.Asin(args[0]);
                    return true;
                case "acos" when args.Count == 1 && args[0] >= -1 && args[0] <= 1:
                    value = Math.Acos(args[0]);
                    return true;
                case "atan" when args.Count == 1:
                    value = Math.Atan(args[0]);
                    return true;
                case "atan2" when args.Count == 2:
                    value = Math.Atan2(args[0], args[1]);
                    return true;
                case "min" when args.Count >= 1:
                    value = args[0];
                    for (int i = 1; i < args.Count; i++)
                    {
                        value = Math.Min(value, args[i]);
                    }

                    return true;
                case "max" when args.Count >= 1:
                    value = args[0];
                    for (int i = 1; i < args.Count; i++)
                    {
                        value = Math.Max(value, args[i]);
                    }

                    return true;
                case "if" when args.Count == 3:
                    value = Math.Abs(args[0]) > double.Epsilon ? args[1] : args[2];
                    return true;
                default:
                    return false;
            }
        }

        private bool TryReadReference(out double value)
        {
            value = 0;
            int start = _position;
            _position++;
            while (_position < _formula.Length && (char.IsLetterOrDigit(_formula[_position]) || _formula[_position] == '_' || _formula[_position] == '-'))
            {
                _position++;
            }

            return _values.TryGetValue(_formula.Substring(start, _position - start), out value);
        }

        private bool TryReadNumber(out double value)
        {
            value = 0;
            int start = _position;
            while (_position < _formula.Length && (char.IsDigit(_formula[_position]) || _formula[_position] is '.' or 'E' or 'e' or '+' or '-'))
            {
                if ((_formula[_position] is '+' or '-') && _position > start && _formula[_position - 1] is not 'E' and not 'e')
                {
                    break;
                }

                _position++;
            }

            return _position > start &&
                double.TryParse(_formula.Substring(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                IsFinite(value);
        }

        private void SkipWhitespace()
        {
            while (_position < _formula.Length && char.IsWhiteSpace(_formula[_position]))
            {
                _position++;
            }
        }

        private bool TryConsume(char value)
        {
            SkipWhitespace();
            if (!TryPeek(value))
            {
                return false;
            }

            _position++;
            return true;
        }

        private bool TryPeek(char value) => _position < _formula.Length && _formula[_position] == value;

        private bool TryPeekLetter() => _position < _formula.Length && char.IsLetter(_formula[_position]);

    }

    private readonly struct EnhancedPathToken
    {
        private EnhancedPathToken(char command, double number, bool isCommand)
        {
            Command = command;
            Number = number;
            IsCommand = isCommand;
        }

        public char Command { get; }

        public double Number { get; }

        public bool IsCommand { get; }

        public static EnhancedPathToken ForCommand(char command) => new(command, 0, isCommand: true);

        public static EnhancedPathToken ForNumber(double number) => new('\0', number, isCommand: false);
    }

    private sealed class CanvasBounds
    {
        public CanvasBounds(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; private set; }

        public double Height { get; private set; }

        public void Include(double x, double y)
        {
            if (x > Width)
            {
                Width = x;
            }

            if (y > Height)
            {
                Height = y;
            }
        }
    }

    private sealed class SvgExportContext
    {
        private readonly Dictionary<string, GradientDefinition> _availableGradients = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GradientDefinition> _usedGradients = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MarkerDefinition> _availableMarkers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MarkerDefinition> _usedMarkers = new(StringComparer.Ordinal);
        private int _clipId;

        public SvgExportContext(DrawingDocument document, OdfSvgExportOptions options)
        {
            Document = document;
            Options = options;
            LoadGradients(document.ContentDom);
            LoadGradients(document.StylesDom);
            LoadMarkers(document.ContentDom);
            LoadMarkers(document.StylesDom);
        }

        public DrawingDocument Document { get; }

        public OdfSvgExportOptions Options { get; }

        public void DiscoverGradientReferences(OdfNode node)
        {
            if (node.NodeType == OdfNodeType.Element)
            {
                _ = TryUseGradient(node);
                _ = TryUseMarker(node, "marker-start");
                _ = TryUseMarker(node, "marker-end");
            }

            foreach (OdfNode child in node.Children)
            {
                DiscoverGradientReferences(child);
            }
        }

        public bool TryGetGradientFill(OdfNode node, out string? fill)
        {
            GradientDefinition? gradient = TryUseGradient(node);
            fill = gradient is null ? null : "url(#" + gradient.SvgId + ")";
            return gradient is not null;
        }

        public string CreateClipId()
        {
            _clipId++;
            return "odf-clip-" + _clipId.ToString(CultureInfo.InvariantCulture);
        }

        public bool TryGetMarkerUrl(OdfNode node, string attributeName, out string? value)
        {
            MarkerDefinition? marker = TryUseMarker(node, attributeName);
            value = marker is null ? null : "url(#" + marker.SvgId + ")";
            return marker is not null;
        }

        public void WriteDefinitions(StringBuilder sb)
        {
            if (_usedGradients.Count == 0 && _usedMarkers.Count == 0)
            {
                return;
            }

            sb.Append("<defs>");
            foreach (MarkerDefinition marker in _usedMarkers.Values)
            {
                sb.Append("<marker");
                AppendAttribute(sb, "id", marker.SvgId);
                AppendAttribute(sb, "viewBox", marker.ViewBox.Raw);
                AppendAttribute(sb, "markerWidth", FormatPoints(marker.ViewBox.Width));
                AppendAttribute(sb, "markerHeight", FormatPoints(marker.ViewBox.Height));
                AppendAttribute(sb, "refX", FormatPoints(marker.ViewBox.MaxX));
                AppendAttribute(sb, "refY", FormatPoints(marker.ViewBox.CenterY));
                AppendAttribute(sb, "orient", "auto");
                AppendAttribute(sb, "markerUnits", "strokeWidth");
                sb.Append("><path");
                AppendAttribute(sb, "d", marker.PathData);
                AppendAttribute(sb, "fill", "context-stroke");
                AppendAttribute(sb, "stroke", "none");
                sb.Append(" /></marker>");
            }

            foreach (GradientDefinition gradient in _usedGradients.Values)
            {
                if (gradient.IsRadial)
                {
                    sb.Append("<radialGradient");
                    AppendAttribute(sb, "id", gradient.SvgId);
                    AppendAttribute(sb, "cx", gradient.CenterX);
                    AppendAttribute(sb, "cy", gradient.CenterY);
                    AppendAttribute(sb, "fx", gradient.CenterX);
                    AppendAttribute(sb, "fy", gradient.CenterY);
                    AppendAttribute(sb, "r", "50%");
                    sb.Append('>');
                    WriteGradientStops(sb, gradient);
                    sb.Append("</radialGradient>");
                    continue;
                }

                GradientVector vector = GradientVector.FromAngle(gradient.AngleDegrees);
                sb.Append("<linearGradient");
                AppendAttribute(sb, "id", gradient.SvgId);
                AppendAttribute(sb, "x1", vector.X1);
                AppendAttribute(sb, "y1", vector.Y1);
                AppendAttribute(sb, "x2", vector.X2);
                AppendAttribute(sb, "y2", vector.Y2);
                sb.Append('>');
                WriteGradientStops(sb, gradient);
                sb.Append("</linearGradient>");
            }
            sb.Append("</defs>");
        }

        private static void WriteGradientStops(StringBuilder sb, GradientDefinition gradient)
        {
            sb.Append("<stop");
            AppendAttribute(sb, "offset", "0%");
            AppendAttribute(sb, "stop-color", gradient.StartColor);
            sb.Append(" /><stop");
            AppendAttribute(sb, "offset", "100%");
            AppendAttribute(sb, "stop-color", gradient.EndColor);
            sb.Append(" />");
        }

        private GradientDefinition? TryUseGradient(OdfNode node)
        {
            string? fillMode = node.GetAttribute("fill", OdfNamespaces.Draw);
            string? gradientName = node.GetAttribute("fill-gradient-name", OdfNamespaces.Draw);
            if (!string.Equals(fillMode, "gradient", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(gradientName) ||
                !_availableGradients.TryGetValue(gradientName!, out GradientDefinition? gradient))
            {
                return null;
            }

            _usedGradients[gradient.Name] = gradient;
            return gradient;
        }

        private MarkerDefinition? TryUseMarker(OdfNode node, string attributeName)
        {
            string? markerName = node.GetAttribute(attributeName, OdfNamespaces.Draw);
            if (string.IsNullOrWhiteSpace(markerName))
            {
                string? styleName = node.GetAttribute("style-name", OdfNamespaces.Draw);
                markerName = string.IsNullOrWhiteSpace(styleName)
                    ? null
                    : Document.StyleEngine.GetStyleProperty(styleName!, attributeName, OdfNamespaces.Draw, "graphic");
            }

            if (string.IsNullOrWhiteSpace(markerName) ||
                !_availableMarkers.TryGetValue(markerName!, out MarkerDefinition? marker))
            {
                return null;
            }

            _usedMarkers[marker.Name] = marker;
            return marker;
        }

        private void LoadGradients(OdfNode? root)
        {
            if (root is null)
            {
                return;
            }

            foreach (OdfNode node in Enumerate(root))
            {
                if (node.NodeType != OdfNodeType.Element ||
                    node.NamespaceUri != OdfNamespaces.Draw ||
                    node.LocalName != "gradient")
                {
                    continue;
                }

                string? name = node.GetAttribute("name", OdfNamespaces.Draw);
                string? startColor = node.GetAttribute("start-color", OdfNamespaces.Draw);
                string? endColor = node.GetAttribute("end-color", OdfNamespaces.Draw);
                string? style = node.GetAttribute("style", OdfNamespaces.Draw);
                string centerX = NormalizePercent(node.GetAttribute("cx", OdfNamespaces.Draw) ?? node.GetAttribute("cx", OdfNamespaces.Svg), "50%");
                string centerY = NormalizePercent(node.GetAttribute("cy", OdfNamespaces.Draw) ?? node.GetAttribute("cy", OdfNamespaces.Svg), "50%");
                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(startColor) ||
                    string.IsNullOrWhiteSpace(endColor) ||
                    _availableGradients.ContainsKey(name!))
                {
                    continue;
                }

                _availableGradients.Add(
                    name!,
                    new GradientDefinition(
                        name!,
                        CreateSvgId(name!),
                        startColor!,
                        endColor!,
                        style,
                        ParseGradientAngle(node.GetAttribute("angle", OdfNamespaces.Draw)),
                        centerX,
                        centerY));
            }
        }

        private void LoadMarkers(OdfNode? root)
        {
            if (root is null)
            {
                return;
            }

            foreach (OdfNode node in Enumerate(root))
            {
                if (node.NodeType != OdfNodeType.Element ||
                    node.NamespaceUri != OdfNamespaces.Draw ||
                    node.LocalName != "marker")
                {
                    continue;
                }

                string? name = node.GetAttribute("name", OdfNamespaces.Draw);
                string? pathData = node.GetAttribute("d", OdfNamespaces.Svg);
                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(pathData) ||
                    !IsSvgCompatibleEnhancedPath(pathData!) ||
                    _availableMarkers.ContainsKey(name!))
                {
                    continue;
                }

                _availableMarkers.Add(
                    name!,
                    new MarkerDefinition(
                        name!,
                        CreateSvgId("odf-marker-", name!),
                        pathData!,
                        MarkerViewBox.Parse(node.GetAttribute("viewBox", OdfNamespaces.Svg))));
            }
        }

        private static double ParseGradientAngle(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            string angle = value!.Trim();
            if (angle.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
            {
                angle = angle.Substring(0, angle.Length - 3);
            }

            if (!double.TryParse(angle, NumberStyles.Float, CultureInfo.InvariantCulture, out double degrees))
            {
                return 0;
            }

            return Math.Abs(degrees) > 360 ? degrees / 10 : degrees;
        }

        private static string NormalizePercent(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string percent = value!.Trim();
            if (percent.EndsWith("%", StringComparison.Ordinal))
            {
                return percent;
            }

            return double.TryParse(percent, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? FormatPercent(number)
                : fallback;
        }

        private static IEnumerable<OdfNode> Enumerate(OdfNode root)
        {
            yield return root;
            foreach (OdfNode child in root.Children)
            {
                foreach (OdfNode descendant in Enumerate(child))
                {
                    yield return descendant;
                }
            }
        }

        private static string CreateSvgId(string name) => CreateSvgId("odf-gradient-", name);

        private static string CreateSvgId(string prefix, string name)
        {
            var sb = new StringBuilder(prefix);
            foreach (char c in name)
            {
                sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
            }

            return sb.ToString();
        }
    }

    private sealed class ClipRectangle
    {
        public ClipRectangle(string id, double x, double y, double width, double height)
        {
            Id = id;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string Id { get; }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public ClipRectangle WithId(string id) => new(id, X, Y, Width, Height);
    }

    private sealed class ClipDefinition
    {
        public ClipDefinition(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

    private readonly struct ViewBox
    {
        public ViewBox(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }
    }

    private sealed class GradientDefinition
    {
        public GradientDefinition(
            string name,
            string svgId,
            string startColor,
            string endColor,
            string? style,
            double angleDegrees,
            string centerX,
            string centerY)
        {
            Name = name;
            SvgId = svgId;
            StartColor = startColor;
            EndColor = endColor;
            Style = string.IsNullOrWhiteSpace(style) ? "linear" : style!.Trim();
            AngleDegrees = angleDegrees;
            CenterX = centerX;
            CenterY = centerY;
        }

        public string Name { get; }

        public string SvgId { get; }

        public string StartColor { get; }

        public string EndColor { get; }

        public string Style { get; }

        public double AngleDegrees { get; }

        public string CenterX { get; }

        public string CenterY { get; }

        public bool IsRadial =>
            string.Equals(Style, "radial", StringComparison.Ordinal) ||
            string.Equals(Style, "ellipsoid", StringComparison.Ordinal) ||
            string.Equals(Style, "square", StringComparison.Ordinal) ||
            string.Equals(Style, "rectangular", StringComparison.Ordinal);
    }

    private sealed class MarkerDefinition
    {
        public MarkerDefinition(string name, string svgId, string pathData, MarkerViewBox viewBox)
        {
            Name = name;
            SvgId = svgId;
            PathData = pathData;
            ViewBox = viewBox;
        }

        public string Name { get; }

        public string SvgId { get; }

        public string PathData { get; }

        public MarkerViewBox ViewBox { get; }
    }

    private sealed class MarkerViewBox
    {
        private MarkerViewBox(string raw, double x, double y, double width, double height)
        {
            Raw = raw;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string Raw { get; }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public double MaxX => X + Width;

        public double CenterY => Y + (Height / 2);

        public static MarkerViewBox Parse(string? value)
        {
            const string fallback = "0 0 10 10";
            if (string.IsNullOrWhiteSpace(value))
            {
                return new MarkerViewBox(fallback, 0, 0, 10, 10);
            }

            string[] parts = value!.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double width) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double height) &&
                width > 0 &&
                height > 0)
            {
                return new MarkerViewBox(value!, x, y, width, height);
            }

            return new MarkerViewBox(fallback, 0, 0, 10, 10);
        }
    }

    private sealed class GradientVector
    {
        private GradientVector(string x1, string y1, string x2, string y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public string X1 { get; }

        public string Y1 { get; }

        public string X2 { get; }

        public string Y2 { get; }

        public static GradientVector FromAngle(double degrees)
        {
            double radians = degrees * Math.PI / 180;
            double dx = Math.Cos(radians) * 50;
            double dy = Math.Sin(radians) * 50;
            return new GradientVector(
                FormatPercent(50 - dx),
                FormatPercent(50 - dy),
                FormatPercent(50 + dx),
                FormatPercent(50 + dy));
        }
    }

    private sealed class TextParagraph
    {
        public TextParagraph(IReadOnlyList<TextRun> runs)
        {
            Runs = runs;
        }

        public IReadOnlyList<TextRun> Runs { get; }
    }

    private sealed class TextRun
    {
        public TextRun(string text, TextStyle style)
        {
            Text = text;
            Style = style;
        }

        public string Text { get; }

        public TextStyle Style { get; }
    }

    private sealed class TextStyle
    {
        public static readonly TextStyle Empty = new(null, null, null, null);

        public TextStyle(string? fontWeight, string? fontStyle, string? fontSize, string? colorHex)
        {
            FontWeight = string.IsNullOrWhiteSpace(fontWeight) ? null : fontWeight;
            FontStyle = string.IsNullOrWhiteSpace(fontStyle) ? null : fontStyle;
            FontSize = string.IsNullOrWhiteSpace(fontSize) ? null : fontSize;
            ColorHex = colorHex;
        }

        public string? FontWeight { get; }

        public string? FontStyle { get; }

        public string? FontSize { get; }

        public string? ColorHex { get; }

        public TextStyle Merge(TextStyle overrideStyle)
        {
            return new TextStyle(
                overrideStyle.FontWeight ?? FontWeight,
                overrideStyle.FontStyle ?? FontStyle,
                overrideStyle.FontSize ?? FontSize,
                overrideStyle.ColorHex ?? ColorHex);
        }
    }
}

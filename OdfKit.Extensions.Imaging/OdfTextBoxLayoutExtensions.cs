using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Extensions.Imaging;

/// <summary>
/// Provides shared automatic text-box layout for ODT, ODP, and ODG documents.
/// 提供 ODT、ODP 與 ODG 文件共用的文字框自動版面配置。
/// </summary>
public static class OdfTextBoxLayoutExtensions
{
    /// <summary>
    /// Automatically lays out an ODP or ODG text box.
    /// 自動配置 ODP 或 ODG 文字框。
    /// </summary>
    /// <param name="textBox">The target text box. / 目標文字框。</param>
    /// <param name="options">The bounded layout options. / 具資源上限的版面選項。</param>
    /// <returns>The measured or reader-delegated bounds. / 量測或交由閱讀器處理的邊界。</returns>
    public static OdfTextMeasureResult AutoFit(
        this OdfTextBox textBox,
        OdfAutoFitOptions options) =>
        AutoFit(textBox, options, CancellationToken.None);

    /// <summary>
    /// Automatically lays out an ODP or ODG text box.
    /// 自動配置 ODP 或 ODG 文字框。
    /// </summary>
    /// <param name="textBox">The target text box. / 目標文字框。</param>
    /// <param name="options">The bounded layout options. / 具資源上限的版面選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The measured or reader-delegated bounds. / 量測或交由閱讀器處理的邊界。</returns>
    public static OdfTextMeasureResult AutoFit(
        this OdfTextBox textBox,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(
            textBox,
            nameof(textBox));
        return AutoFitFrame(
            textBox.Node,
            textBox.Document,
            textBox.Text,
            options,
            cancellationToken);
    }

    /// <summary>
    /// Automatically lays out an ODT floating text box.
    /// 自動配置 ODT 浮動文字框。
    /// </summary>
    /// <param name="textBox">The target floating text box. / 目標浮動文字框。</param>
    /// <param name="options">The bounded layout options. / 具資源上限的版面選項。</param>
    /// <returns>The measured or reader-delegated bounds. / 量測或交由閱讀器處理的邊界。</returns>
    public static OdfTextMeasureResult AutoFit(
        this OdfFloatingTextBox textBox,
        OdfAutoFitOptions options) =>
        AutoFit(textBox, options, CancellationToken.None);

    /// <summary>
    /// Automatically lays out an ODT floating text box.
    /// 自動配置 ODT 浮動文字框。
    /// </summary>
    /// <param name="textBox">The target floating text box. / 目標浮動文字框。</param>
    /// <param name="options">The bounded layout options. / 具資源上限的版面選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The measured or reader-delegated bounds. / 量測或交由閱讀器處理的邊界。</returns>
    public static OdfTextMeasureResult AutoFit(
        this OdfFloatingTextBox textBox,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(
            textBox,
            nameof(textBox));
        OdfNode frame = textBox.TextBoxNode.Parent ??
            throw new InvalidOperationException();
        return AutoFitFrame(
            frame,
            textBox.Document,
            ExtractParagraphText(textBox.TextBoxNode),
            options,
            cancellationToken);
    }

    private static OdfTextMeasureResult AutoFitFrame(
        OdfNode frame,
        OdfDocument document,
        string text,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(
            options,
            nameof(options));
        if (options.MaximumTextElementsPerBlock < 1 ||
            text.Length > options.MaximumTextElementsPerBlock ||
            text.Length > options.MaximumTextElements)
        {
            throw new InvalidOperationException();
        }
        if (options.Mode == OdfAutoFitMode.Precise &&
            options.TextMeasurer is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (options.Mode == OdfAutoFitMode.Reader)
        {
            ApplyReaderLayout(frame, document, options);
            return ReadCurrentBounds(frame, text);
        }

        string graphicStyle = frame.GetAttribute(
            "style-name",
            OdfNamespaces.Draw) ?? string.Empty;
        OdfNode? paragraph = FindFirstDescendant(
            frame,
            "p",
            OdfNamespaces.Text);
        string paragraphStyle = paragraph?.GetAttribute(
            "style-name",
            OdfNamespaces.Text) ?? string.Empty;
        string fontFamily = GetProperty(
            document,
            paragraphStyle,
            "paragraph",
            graphicStyle,
            "graphic",
            "font-name",
            OdfNamespaces.Style) ?? options.DefaultFontFamily;
        string? rawFontSize = GetProperty(
            document,
            paragraphStyle,
            "paragraph",
            graphicStyle,
            "graphic",
            "font-size",
            OdfNamespaces.Fo);
        double fontSize = ParsePoints(
            rawFontSize,
            options.DefaultFontSizePoints);
        bool bold = GetProperty(
            document,
            paragraphStyle,
            "paragraph",
            graphicStyle,
            "graphic",
            "font-weight",
            OdfNamespaces.Fo) == "bold";
        bool italic = GetProperty(
            document,
            paragraphStyle,
            "paragraph",
            graphicStyle,
            "graphic",
            "font-style",
            OdfNamespaces.Fo) == "italic";
        OdfWritingMode writingMode = OdfWritingModeExtensions.FromOdfToken(
            GetProperty(
                document,
                paragraphStyle,
                "paragraph",
                graphicStyle,
                "graphic",
                "writing-mode",
                OdfNamespaces.Style));

        double horizontalPadding =
            options.HorizontalPadding.ToCentimeters();
        double verticalPadding =
            options.VerticalPadding.ToCentimeters();
        double? currentWidth = ParseCentimeters(
            frame.GetAttribute("width", OdfNamespaces.Svg));
        double? availableWidth = !options.ResizeTextBoxWidth &&
            currentWidth is double width
                ? Math.Max(width - horizontalPadding, 0.01)
                : null;

        var request = new OdfTextMeasureRequest
        {
            Text = text,
            FontFamily = fontFamily,
            FontSizePoints = fontSize,
            IsBold = bold,
            IsItalic = italic,
            WritingMode = writingMode,
            AvailableWidthCentimeters = availableWidth,
            Wrap = availableWidth is not null,
            MaximumTextElements = options.MaximumTextElementsPerBlock
        };
        IOdfTextLayoutMeasurer measurer =
            options.Mode == OdfAutoFitMode.Precise
                ? options.TextMeasurer!
                : OdfFastTextLayoutMeasurer.Instance;
        OdfTextMeasureResult measured = measurer.Measure(
            request,
            cancellationToken);
        double measuredWidth = Clamp(
            measured.WidthCentimeters + horizontalPadding,
            options.MinimumColumnWidth.ToCentimeters(),
            options.MaximumColumnWidth.ToCentimeters());
        double measuredHeight = Clamp(
            measured.HeightCentimeters + verticalPadding,
            options.MinimumRowHeight.ToCentimeters(),
            options.MaximumRowHeight.ToCentimeters());

        if (options.ResizeTextBoxWidth)
        {
            frame.SetAttribute(
                "width",
                OdfNamespaces.Svg,
                OdfLength.FromCentimeters(measuredWidth).ToString(),
                "svg");
        }
        if (options.ResizeTextBoxHeight)
        {
            frame.SetAttribute(
                "height",
                OdfNamespaces.Svg,
                OdfLength.FromCentimeters(measuredHeight).ToString(),
                "svg");
        }
        DisableReaderLayout(frame, document, options);
        return new OdfTextMeasureResult(
            options.ResizeTextBoxWidth
                ? measuredWidth
                : currentWidth ?? measuredWidth,
            measuredHeight,
            measured.LineCount,
            measured.IsExact);
    }

    private static void ApplyReaderLayout(
        OdfNode frame,
        OdfDocument document,
        OdfAutoFitOptions options)
    {
        using IDisposable update = document.BeginUpdate();
        if (options.ResizeTextBoxWidth)
        {
            document.StyleEngine.SetLocalStyleProperty(
                frame,
                "graphic",
                "graphic-properties",
                "auto-grow-width",
                OdfNamespaces.Draw,
                "true",
                "draw");
        }
        if (options.ResizeTextBoxHeight)
        {
            document.StyleEngine.SetLocalStyleProperty(
                frame,
                "graphic",
                "graphic-properties",
                "auto-grow-height",
                OdfNamespaces.Draw,
                "true",
                "draw");
        }
    }

    private static void DisableReaderLayout(
        OdfNode frame,
        OdfDocument document,
        OdfAutoFitOptions options)
    {
        using IDisposable update = document.BeginUpdate();
        if (options.ResizeTextBoxWidth)
        {
            document.StyleEngine.SetLocalStyleProperty(
                frame,
                "graphic",
                "graphic-properties",
                "auto-grow-width",
                OdfNamespaces.Draw,
                "false",
                "draw");
        }
        if (options.ResizeTextBoxHeight)
        {
            document.StyleEngine.SetLocalStyleProperty(
                frame,
                "graphic",
                "graphic-properties",
                "auto-grow-height",
                OdfNamespaces.Draw,
                "false",
                "draw");
        }
    }

    private static OdfTextMeasureResult ReadCurrentBounds(
        OdfNode frame,
        string text)
    {
        double width = ParseCentimeters(
            frame.GetAttribute("width", OdfNamespaces.Svg)) ?? 0;
        double height = ParseCentimeters(
            frame.GetAttribute("height", OdfNamespaces.Svg)) ?? 0;
        int lines = 1;
        foreach (char value in text)
        {
            if (value == '\n')
                lines++;
        }
        return new OdfTextMeasureResult(width, height, lines, false);
    }

    private static string ExtractParagraphText(OdfNode root)
    {
        var paragraphs = new List<string>();
        CollectParagraphs(root, paragraphs);
        return string.Join(Environment.NewLine, paragraphs);
    }

    private static void CollectParagraphs(
        OdfNode node,
        List<string> paragraphs)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "p" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                paragraphs.Add(child.TextContent);
            }
            else
            {
                CollectParagraphs(child, paragraphs);
            }
        }
    }

    private static OdfNode? FindFirstDescendant(
        OdfNode node,
        string localName,
        string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
            OdfNode? nested = FindFirstDescendant(
                child,
                localName,
                namespaceUri);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private static string? GetProperty(
        OdfDocument document,
        string primaryStyle,
        string primaryFamily,
        string fallbackStyle,
        string fallbackFamily,
        string property,
        string namespaceUri) =>
        document.StyleEngine.GetStyleProperty(
            primaryStyle,
            property,
            namespaceUri,
            primaryFamily) ??
        document.StyleEngine.GetStyleProperty(
            fallbackStyle,
            property,
            namespaceUri,
            fallbackFamily);

    private static double ParsePoints(string? value, double fallback)
    {
        if (OdfLength.TryParse(value, out OdfLength length) &&
            length.Unit is not OdfUnit.Percentage and not OdfUnit.Em)
        {
            double points = length.ToPoints();
            if (IsFinite(points) && points > 0)
                return Math.Min(points, 1_000);
        }
        return fallback;
    }

    private static double? ParseCentimeters(string? value)
    {
        if (!OdfLength.TryParse(value, out OdfLength length) ||
            length.Unit is OdfUnit.Percentage or OdfUnit.Em)
        {
            return null;
        }
        double centimeters = length.ToCentimeters();
        return IsFinite(centimeters) && centimeters >= 0
            ? centimeters
            : null;
    }

    private static double Clamp(
        double value,
        double minimum,
        double maximum)
    {
        if (!IsFinite(value) ||
            !IsFinite(minimum) ||
            !IsFinite(maximum) ||
            minimum <= 0 ||
            maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        return Math.Min(Math.Max(value, minimum), maximum);
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}

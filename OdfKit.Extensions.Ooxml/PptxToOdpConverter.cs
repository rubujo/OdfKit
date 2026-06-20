using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using A = DocumentFormat.OpenXml.Drawing;
using OdfPresentationDocument = OdfKit.Presentation.PresentationDocument;
using OdfShapeType = OdfKit.Presentation.OdfShapeType;
using P = DocumentFormat.OpenXml.Presentation;
using PackagingPresentationDocument = DocumentFormat.OpenXml.Packaging.PresentationDocument;

namespace OdfKit.Conversion;

/// <summary>
/// 將 PPTX 轉換為 <see cref="OdfPresentationDocument"/> (ODP) 的 managed 淨室轉換器。
/// </summary>
public static class PptxToOdpConverter
{
    private const double EmusPerPoint = 12700d;
    private static readonly OdfLength DefaultX = OdfLength.FromCentimeters(1);
    private static readonly OdfLength DefaultY = OdfLength.FromCentimeters(1);
    private static readonly OdfLength DefaultWidth = OdfLength.FromCentimeters(4);
    private static readonly OdfLength DefaultHeight = OdfLength.FromCentimeters(2);

    /// <summary>
    /// 從 PPTX 資料流建立 ODP 簡報文件。
    /// </summary>
    /// <param name="pptxStream">來源 PPTX 資料流。</param>
    /// <returns>轉換後的 ODP 簡報文件。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pptxStream"/> 為 null 時引發。</exception>
    public static OdfPresentationDocument Convert(Stream pptxStream)
    {
        if (pptxStream is null)
            throw new ArgumentNullException(nameof(pptxStream));

        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        var odp = OdfPresentationDocument.Create();
        PresentationPart? presentationPart = pptx.PresentationPart;
        if (presentationPart is null)
        {
            odp.AddSlide("Slide 1");
            return odp;
        }

        ApplySlideSize(presentationPart, odp);

        int slideIndex = 1;
        foreach (SlidePart slidePart in GetOrderedSlides(presentationPart))
        {
            OdfKit.Presentation.OdfSlide slide = odp.AddSlide(ReadSlideName(slidePart, slideIndex));
            ApplySlideLayout(slidePart, odp, slideIndex - 1);
            slideIndex++;
            ConvertSlide(slidePart, slide);
        }

        if (slideIndex == 1)
        {
            odp.AddSlide("Slide 1");
        }

        return odp;
    }

    private static void ApplySlideSize(PresentationPart presentationPart, OdfPresentationDocument odp)
    {
        P.SlideSize? slideSize = presentationPart.Presentation?.SlideSize;
        int? width = slideSize?.Cx?.Value;
        int? height = slideSize?.Cy?.Value;
        if (width is null or <= 0 || height is null or <= 0)
        {
            return;
        }

        odp.SetSlideSize(
            OdfLength.FromPoints(width.Value / EmusPerPoint),
            OdfLength.FromPoints(height.Value / EmusPerPoint));
    }

    private static string ReadSlideName(SlidePart slidePart, int slideIndex)
    {
        string? name = slidePart.Slide?.CommonSlideData?.Name?.Value;
        return string.IsNullOrWhiteSpace(name)
            ? "Slide " + slideIndex.ToString(CultureInfo.InvariantCulture)
            : name!;
    }

    private static IEnumerable<SlidePart> GetOrderedSlides(PresentationPart presentationPart)
    {
        P.SlideIdList? slideIds = presentationPart.Presentation?.SlideIdList;
        if (slideIds is null)
        {
            return presentationPart.SlideParts;
        }

        return slideIds.Elements<P.SlideId>()
            .Select(slideId => slideId.RelationshipId?.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => presentationPart.GetPartById(id!))
            .OfType<SlidePart>();
    }

    private static void ApplySlideLayout(SlidePart slidePart, OdfPresentationDocument document, int slideIndex)
    {
        OdfKit.Presentation.OdfPresentationLayout? layout = ReadSlideLayout(slidePart);
        if (layout.HasValue)
        {
            document.SetLayout(slideIndex, layout.Value);
        }
    }

    private static OdfKit.Presentation.OdfPresentationLayout? ReadSlideLayout(SlidePart slidePart)
    {
        P.SlideLayoutValues? layoutType = slidePart.SlideLayoutPart?.SlideLayout?.Type?.Value;
        if (!layoutType.HasValue)
        {
            return null;
        }

        if (layoutType.Value.Equals(P.SlideLayoutValues.Blank))
        {
            return OdfKit.Presentation.OdfPresentationLayout.Blank;
        }

        if (layoutType.Value.Equals(P.SlideLayoutValues.TitleOnly))
        {
            return OdfKit.Presentation.OdfPresentationLayout.TitleOnly;
        }

        if (layoutType.Value.Equals(P.SlideLayoutValues.Title))
        {
            return OdfKit.Presentation.OdfPresentationLayout.TitleAndSubtitle;
        }

        return layoutType.Value.Equals(P.SlideLayoutValues.Text) ||
            layoutType.Value.Equals(P.SlideLayoutValues.TwoColumnText) ||
            layoutType.Value.Equals(P.SlideLayoutValues.TextAndObject) ||
            layoutType.Value.Equals(P.SlideLayoutValues.ObjectAndText) ||
            layoutType.Value.Equals(P.SlideLayoutValues.VerticalTitleAndText)
            ? OdfKit.Presentation.OdfPresentationLayout.TitleAndBody
            : null;
    }

    private static void ConvertSlide(SlidePart slidePart, OdfKit.Presentation.OdfSlide slide)
    {
        ThemeColorMap themeColors = ThemeColorMap.FromSlide(slidePart);
        string? backgroundColor = ReadSlideBackgroundColor(slidePart, themeColors);
        if (!string.IsNullOrWhiteSpace(backgroundColor))
        {
            slide.BackgroundColor = "#" + backgroundColor;
        }

        ApplySlideTransition(slidePart, slide);

        P.ShapeTree? shapeTree = slidePart.Slide?.CommonSlideData?.ShapeTree;
        if (shapeTree is null)
        {
            return;
        }

        var animationTargets = new Dictionary<uint, string>();
        foreach (P.Shape shape in shapeTree.Elements<P.Shape>())
        {
            ConvertShape(slidePart, shape, slide, animationTargets, themeColors);
        }

        foreach (P.Picture picture in shapeTree.Elements<P.Picture>())
        {
            ConvertPicture(picture, slidePart, slide, animationTargets);
        }

        foreach (P.GraphicFrame graphicFrame in shapeTree.Elements<P.GraphicFrame>())
        {
            ConvertGraphicFrame(graphicFrame, slide, animationTargets, themeColors);
        }

        ApplySlideAnimations(slidePart, slide, animationTargets);

        IReadOnlyList<string> speakerNotes = GetSpeakerNotes(slidePart);
        if (speakerNotes.Count > 0 && speakerNotes.Any(note => !string.IsNullOrWhiteSpace(note)))
        {
            slide.SetSpeakerNotes(speakerNotes);
        }
    }

    private static void ApplySlideTransition(SlidePart slidePart, OdfKit.Presentation.OdfSlide slide)
    {
        P.Transition? transition = slidePart.Slide?.Transition;
        if (transition is null)
        {
            return;
        }

        OdfKit.Presentation.OdfTransitionType? transitionType = ReadTransitionType(transition);
        if (!transitionType.HasValue)
        {
            return;
        }

        slide.SetTransition(
            transitionType.Value,
            ReadTransitionDuration(transition.Duration?.Value),
            ReadTransitionSpeed(transition.Speed?.Value));
    }

    private static OdfKit.Presentation.OdfTransitionType? ReadTransitionType(P.Transition transition)
    {
        OpenXmlElement? child = transition.ChildElements.FirstOrDefault();
        return child switch
        {
            P.PushTransition _ => OdfKit.Presentation.OdfTransitionType.Push,
            P.WipeTransition _ => OdfKit.Presentation.OdfTransitionType.Wipe,
            P.ZoomTransition _ => OdfKit.Presentation.OdfTransitionType.Zoom,
            P.SplitTransition _ => OdfKit.Presentation.OdfTransitionType.Split,
            P.FadeTransition _ => OdfKit.Presentation.OdfTransitionType.Fade,
            _ => null,
        };
    }

    private static OdfLength ReadTransitionDuration(string? millisecondsValue)
    {
        if (double.TryParse(millisecondsValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds))
        {
            double points = Math.Max(0d, milliseconds / 1000d) * 72d;
            return OdfLength.FromPoints(points);
        }

        return OdfLength.FromPoints(72);
    }

    private static OdfKit.Presentation.OdfTransitionSpeed ReadTransitionSpeed(P.TransitionSpeedValues? value)
    {
        if (value?.Equals(P.TransitionSpeedValues.Slow) == true)
        {
            return OdfKit.Presentation.OdfTransitionSpeed.Slow;
        }

        if (value?.Equals(P.TransitionSpeedValues.Fast) == true)
        {
            return OdfKit.Presentation.OdfTransitionSpeed.Fast;
        }

        return OdfKit.Presentation.OdfTransitionSpeed.Medium;
    }

    private static string? ReadSlideBackgroundColor(SlidePart slidePart, ThemeColorMap themeColors)
    {
        P.Background?[] backgrounds =
        [
            slidePart.Slide?.CommonSlideData?.Background,
            slidePart.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Background,
            slidePart.SlideLayoutPart?.SlideMasterPart?.SlideMaster?.CommonSlideData?.Background,
        ];

        foreach (P.Background? background in backgrounds)
        {
            string? color = ReadBackgroundColor(background, themeColors);
            if (!string.IsNullOrWhiteSpace(color))
            {
                return color;
            }
        }

        return null;
    }

    private static string? ReadBackgroundColor(P.Background? background, ThemeColorMap themeColors)
    {
        string? directColor = ReadSolidFillColor(
            background?.BackgroundProperties?.GetFirstChild<A.SolidFill>(),
            themeColors);
        if (!string.IsNullOrWhiteSpace(directColor))
        {
            return directColor;
        }

        P.BackgroundStyleReference? backgroundReference = background?.BackgroundStyleReference;
        return ReadStyleReferenceColor(backgroundReference, themeColors) ??
            themeColors.ResolveBackgroundFillColor(backgroundReference?.Index?.Value);
    }

    private static void ConvertShape(
        SlidePart slidePart,
        P.Shape shape,
        OdfKit.Presentation.OdfSlide slide,
        Dictionary<uint, string> animationTargets,
        ThemeColorMap themeColors)
    {
        Bounds bounds = GetBounds(shape.ShapeProperties);
        IReadOnlyList<TextParagraph> paragraphs = GetTextParagraphs(shape, themeColors);
        IReadOnlyList<TextRun> runs = FlattenTextRuns(paragraphs);
        uint? sourceShapeId = ReadShapeId(shape.NonVisualShapeProperties?.NonVisualDrawingProperties);
        OdfKit.Presentation.OdfPlaceholderType? placeholderType = ReadPlaceholderType(shape);

        P.PlaceholderShape? ph = GetPlaceholderShape(shape);
        P.Shape? matchingPlaceholder = ph is not null ? FindMatchingPlaceholder(slidePart, ph) : null;

        if (placeholderType.HasValue)
        {
            OdfKit.Presentation.OdfPlaceholder placeholder = slide.AddPlaceholder(placeholderType.Value, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (runs.Count > 0)
            {
                AppendTextParagraphs(slide, placeholder.Node, paragraphs);
            }

            ApplyShapeStyle(shape.ShapeProperties, shape.ShapeStyle, placeholder, themeColors, matchingPlaceholder);
            RegisterAnimationTarget(sourceShapeId, placeholder, animationTargets);
            return;
        }

        A.ShapeTypeValues? preset = shape.ShapeProperties?
            .GetFirstChild<A.PresetGeometry>()?
            .Preset?
            .Value;
        if (runs.Count > 0)
        {
            OdfKit.Presentation.OdfShape addedShape;
            if (preset.HasValue && !IsTextBoxShape(shape) && preset.Value.Equals(A.ShapeTypeValues.Rectangle))
            {
                addedShape = slide.AddShape(OdfShapeType.Rectangle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                AppendTextParagraphs(slide, addedShape.Node, paragraphs);
            }
            else if (preset.HasValue && !IsTextBoxShape(shape) && preset.Value.Equals(A.ShapeTypeValues.Ellipse))
            {
                addedShape = slide.AddShape(OdfShapeType.Ellipse, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                AppendTextParagraphs(slide, addedShape.Node, paragraphs);
            }
            else
            {
                string text = string.Concat(runs.Select(run => run.Text));
                if (runs.Count == 1 && runs[0].Style.IsEmpty && !HasParagraphAlignment(paragraphs))
                {
                    addedShape = slide.AddTextBox(bounds.X, bounds.Y, bounds.Width, bounds.Height, text);
                }
                else
                {
                    addedShape = AddStyledTextBox(slide, bounds, paragraphs);
                }
            }

            ApplyShapeStyle(shape.ShapeProperties, shape.ShapeStyle, addedShape, themeColors, matchingPlaceholder);
            RegisterAnimationTarget(sourceShapeId, addedShape, animationTargets);
            return;
        }
        if (preset.HasValue && preset.Value.Equals(A.ShapeTypeValues.Rectangle))
        {
            OdfKit.Presentation.OdfShape addedShape = slide.AddShape(OdfShapeType.Rectangle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            ApplyShapeStyle(shape.ShapeProperties, shape.ShapeStyle, addedShape, themeColors, matchingPlaceholder);
            RegisterAnimationTarget(sourceShapeId, addedShape, animationTargets);
        }
        else if (preset.HasValue && preset.Value.Equals(A.ShapeTypeValues.Ellipse))
        {
            OdfKit.Presentation.OdfShape addedShape = slide.AddShape(OdfShapeType.Ellipse, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            ApplyShapeStyle(shape.ShapeProperties, shape.ShapeStyle, addedShape, themeColors, matchingPlaceholder);
            RegisterAnimationTarget(sourceShapeId, addedShape, animationTargets);
        }
        else if (preset.HasValue && preset.Value.Equals(A.ShapeTypeValues.Line))
        {
            OdfKit.Presentation.OdfShape addedShape = slide.AddLine(
                bounds.HorizontalFlip ? AddLengths(bounds.X, bounds.Width) : bounds.X,
                bounds.VerticalFlip ? AddLengths(bounds.Y, bounds.Height) : bounds.Y,
                bounds.HorizontalFlip ? bounds.X : AddLengths(bounds.X, bounds.Width),
                bounds.VerticalFlip ? bounds.Y : AddLengths(bounds.Y, bounds.Height));
            ApplyShapeStyle(shape.ShapeProperties, shape.ShapeStyle, addedShape, themeColors, matchingPlaceholder);
            RegisterAnimationTarget(sourceShapeId, addedShape, animationTargets);
        }
    }

    private static bool IsTextBoxShape(P.Shape shape)
    {
        string? name = shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value;
        return string.Equals(name, "Text Box", StringComparison.OrdinalIgnoreCase);
    }

    private static OdfKit.Presentation.OdfPlaceholderType? ReadPlaceholderType(P.Shape shape)
    {
        P.PlaceholderValues? value = shape.NonVisualShapeProperties?
            .ApplicationNonVisualDrawingProperties?
            .GetFirstChild<P.PlaceholderShape>()?
            .Type?
            .Value;
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value.Equals(P.PlaceholderValues.Title) || value.Value.Equals(P.PlaceholderValues.CenteredTitle))
        {
            return OdfKit.Presentation.OdfPlaceholderType.Title;
        }

        if (value.Value.Equals(P.PlaceholderValues.SubTitle))
        {
            return OdfKit.Presentation.OdfPlaceholderType.Subtitle;
        }

        if (value.Value.Equals(P.PlaceholderValues.Body))
        {
            return OdfKit.Presentation.OdfPlaceholderType.Outline;
        }

        if (value.Value.Equals(P.PlaceholderValues.Chart))
        {
            return OdfKit.Presentation.OdfPlaceholderType.Chart;
        }

        if (value.Value.Equals(P.PlaceholderValues.Table))
        {
            return OdfKit.Presentation.OdfPlaceholderType.Table;
        }

        if (value.Value.Equals(P.PlaceholderValues.SlideNumber))
        {
            return OdfKit.Presentation.OdfPlaceholderType.PageNumber;
        }

        if (value.Value.Equals(P.PlaceholderValues.Header))
        {
            return OdfKit.Presentation.OdfPlaceholderType.Header;
        }

        if (value.Value.Equals(P.PlaceholderValues.Footer))
        {
            return OdfKit.Presentation.OdfPlaceholderType.Footer;
        }

        if (value.Value.Equals(P.PlaceholderValues.DateAndTime))
        {
            return OdfKit.Presentation.OdfPlaceholderType.DateTime;
        }

        return OdfKit.Presentation.OdfPlaceholderType.Object;
    }

    private static void ApplyShapeStyle(
        P.ShapeProperties? properties,
        P.ShapeStyle? style,
        OdfKit.Presentation.OdfShape shape,
        ThemeColorMap themeColors,
        P.Shape? matchingPlaceholder = null)
    {
        A.SolidFill? fill = properties?.GetFirstChild<A.SolidFill>() ??
            matchingPlaceholder?.ShapeProperties?.GetFirstChild<A.SolidFill>();

        string? fillColor = ReadSolidFillColor(fill, themeColors) ??
            ReadStyleReferenceColor(style?.FillReference ?? matchingPlaceholder?.ShapeStyle?.FillReference, themeColors) ??
            themeColors.ResolveFillStyleColor((style?.FillReference ?? matchingPlaceholder?.ShapeStyle?.FillReference)?.Index?.Value);
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            shape.FillColor = "#" + fillColor;
        }

        A.Outline? outline = properties?.GetFirstChild<A.Outline>() ??
            matchingPlaceholder?.ShapeProperties?.GetFirstChild<A.Outline>();

        string? strokeColor = ReadSolidFillColor(outline?.GetFirstChild<A.SolidFill>(), themeColors) ??
            ReadStyleReferenceColor(style?.LineReference ?? matchingPlaceholder?.ShapeStyle?.LineReference, themeColors) ??
            themeColors.ResolveLineStyleColor((style?.LineReference ?? matchingPlaceholder?.ShapeStyle?.LineReference)?.Index?.Value);
        if (!string.IsNullOrWhiteSpace(strokeColor))
        {
            shape.StrokeColor = "#" + strokeColor;
        }

        if (outline?.Width?.Value is int width && width > 0)
        {
            shape.StrokeWidth = (width / EmusPerPoint).ToString("0.##", CultureInfo.InvariantCulture) + "pt";
        }
        else if (themeColors.ResolveLineWidth(style?.LineReference?.Index?.Value ?? matchingPlaceholder?.ShapeStyle?.LineReference?.Index?.Value) is string themeLineWidth)
        {
            shape.StrokeWidth = themeLineWidth;
        }

        string? strokeStyle = ReadStrokeStyle(outline?.GetFirstChild<A.PresetDash>()?.Val?.Value);
        if (!string.IsNullOrWhiteSpace(strokeStyle))
        {
            shape.StrokeStyle = strokeStyle;
        }

        A.EffectReference? effectRef = style?.EffectReference ?? matchingPlaceholder?.ShapeStyle?.EffectReference;
        if (effectRef is not null)
        {
            string? shadowColor = ReadStyleReferenceColor(effectRef, themeColors) ??
                themeColors.ResolveEffectStyleColor(effectRef.Index?.Value);
            if (!string.IsNullOrWhiteSpace(shadowColor))
            {
                shape.Document.StyleEngine.SetLocalStyleProperty(
                    shape.Node,
                    "graphic",
                    "graphic-properties",
                    "shadow",
                    OdfNamespaces.Draw,
                    "visible",
                    "draw");
                shape.Document.StyleEngine.SetLocalStyleProperty(
                    shape.Node,
                    "graphic",
                    "graphic-properties",
                    "shadow-color",
                    OdfNamespaces.Draw,
                    "#" + shadowColor,
                    "draw");
            }
        }
    }

    private static string? ReadStyleReferenceColor(OpenXmlCompositeElement? reference, ThemeColorMap themeColors)
    {
        if (reference is null)
        {
            return null;
        }

        string? rgb = reference.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(rgb))
        {
            return NormalizeColor(rgb);
        }

        A.SchemeColorValues? scheme = reference.GetFirstChild<A.SchemeColor>()?.Val?.Value;
        return scheme.HasValue ? themeColors.Resolve(scheme.Value) : null;
    }

    private static void ConvertGraphicFrame(
        P.GraphicFrame graphicFrame,
        OdfKit.Presentation.OdfSlide slide,
        Dictionary<uint, string> animationTargets,
        ThemeColorMap themeColors)
    {
        A.Table? table = graphicFrame.Graphic?
            .GraphicData?
            .GetFirstChild<A.Table>();
        if (table is null)
        {
            return;
        }

        A.TableRow[] rows = table.Elements<A.TableRow>().ToArray();
        int rowCount = Math.Max(rows.Length, 1);
        int columnCount = Math.Max(rows.Select(row => row.Elements<A.TableCell>().Count()).DefaultIfEmpty(1).Max(), 1);
        Bounds bounds = GetBounds(graphicFrame.Transform);
        OdfKit.Presentation.OdfShape shape = slide.AddShape(OdfShapeType.Rectangle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        RegisterAnimationTarget(
            ReadShapeId(graphicFrame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties),
            shape,
            animationTargets);
        OdfKit.Presentation.OdfEmbeddedTable embeddedTable = shape.AddEmbeddedTable(rowCount, columnCount);
        string? tableStyleId = table.TableProperties?.GetFirstChild<A.TableStyleId>()?.Text;
        if (!string.IsNullOrWhiteSpace(tableStyleId))
        {
            embeddedTable.SetTemplateName(tableStyleId);
        }

        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            A.TableCell[] cells = rows[rowIndex].Elements<A.TableCell>().ToArray();
            for (int columnIndex = 0; columnIndex < cells.Length; columnIndex++)
            {
                A.TableCell cell = cells[columnIndex];
                if (cell.HorizontalMerge?.Value == true || cell.VerticalMerge?.Value == true)
                {
                    continue;
                }

                embeddedTable
                    .SetCellText(rowIndex, columnIndex, ReadCellText(cell))
                    .SetCellSpan(
                        rowIndex,
                        columnIndex,
                        ReadPositiveSpan(cell.RowSpan?.Value),
                        ReadPositiveSpan(cell.GridSpan?.Value));
                ApplyTableCellTextStyle(embeddedTable, rowIndex, columnIndex, GetCellTextStyle(cell, themeColors));
                string? backgroundColor = ReadCellBackgroundColor(cell, themeColors);
                if (!string.IsNullOrWhiteSpace(backgroundColor))
                {
                    embeddedTable.SetCellBackgroundColor(rowIndex, columnIndex, "#" + backgroundColor);
                }

                string? border = ReadCellBorder(cell, themeColors);
                if (!string.IsNullOrWhiteSpace(border))
                {
                    embeddedTable.SetCellBorder(rowIndex, columnIndex, border);
                }
            }
        }
    }

    private static int ReadPositiveSpan(int? value)
        => value is > 1 ? value.Value : 1;

    private static string ReadCellText(A.TableCell cell)
    {
        A.TextBody? textBody = cell.TextBody;
        if (textBody is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            textBody.Elements<A.Paragraph>()
                .Select(paragraph => string.Concat(paragraph.Descendants<A.Text>().Select(text => text.Text)))
                .Where(text => text.Length > 0));
    }

    private static TextStyle GetCellTextStyle(A.TableCell cell, ThemeColorMap themeColors)
    {
        A.RunProperties? runProperties = cell.TextBody?
            .Descendants<A.Run>()
            .Select(run => run.RunProperties)
            .FirstOrDefault(properties => properties is not null);
        return GetTextStyle(runProperties, themeColors);
    }

    private static void ApplyTableCellTextStyle(
        OdfKit.Presentation.OdfEmbeddedTable table,
        int row,
        int column,
        TextStyle style)
    {
        if (style.IsEmpty)
        {
            return;
        }

        table.SetCellTextStyle(
            row,
            column,
            bold: style.Bold,
            italic: style.Italic,
            underline: style.Underline,
            strikethrough: style.Strikethrough,
            textPosition: style.TextPosition == TextPosition.Super ? "super" : style.TextPosition == TextPosition.Sub ? "sub" : null,
            fontSize: style.FontSizeHundredthsOfPoint is > 0
                ? (style.FontSizeHundredthsOfPoint.Value / 100d).ToString("0.##", CultureInfo.InvariantCulture) + "pt"
                : null,
            color: string.IsNullOrWhiteSpace(style.ColorHex) ? null : "#" + style.ColorHex);
    }

    private static string? ReadCellBackgroundColor(A.TableCell cell, ThemeColorMap themeColors)
    {
        return ReadSolidFillColor(cell.TableCellProperties?.GetFirstChild<A.SolidFill>(), themeColors);
    }

    private static string? ReadCellBorder(A.TableCell cell, ThemeColorMap themeColors)
    {
        A.TableCellProperties? properties = cell.TableCellProperties;
        A.LinePropertiesType? line = properties?.TopBorderLineProperties;
        line ??= properties?.LeftBorderLineProperties;
        line ??= properties?.RightBorderLineProperties;
        line ??= properties?.BottomBorderLineProperties;
        if (line is null)
        {
            return null;
        }

        string? color = ReadSolidFillColor(line.GetFirstChild<A.SolidFill>(), themeColors) ?? "000000";
        double widthPoints = Math.Max((line.Width?.Value ?? 9525) / EmusPerPoint, 0.25d);
        A.PresetLineDashValues? dash = line.GetFirstChild<A.PresetDash>()?.Val?.Value;
        string style = dash?.Equals(A.PresetLineDashValues.Dash) == true
            ? "dashed"
            : dash?.Equals(A.PresetLineDashValues.Dot) == true ? "dotted" : "solid";
        return widthPoints.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "pt " + style + " #" + color;
    }

    private static void ConvertPicture(
        P.Picture picture,
        SlidePart slidePart,
        OdfKit.Presentation.OdfSlide slide,
        Dictionary<uint, string> animationTargets)
    {
        string? relationshipId = picture.BlipFill?.Blip?.Embed?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return;
        }

        if (slidePart.GetPartById(relationshipId!) is not ImagePart imagePart)
        {
            return;
        }

        using Stream imageStream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
        using var buffer = new MemoryStream();
        imageStream.CopyTo(buffer);

        Bounds bounds = GetBounds(picture.ShapeProperties);
        OdfKit.Presentation.OdfPicture addedShape = slide.AddPicture(buffer.ToArray(), bounds.X, bounds.Y, bounds.Width, bounds.Height, ReadPictureAltText(picture));
        addedShape.CropClip = ReadPictureCropClip(picture, bounds);
        RegisterAnimationTarget(
            ReadShapeId(picture.NonVisualPictureProperties?.NonVisualDrawingProperties),
            addedShape,
            animationTargets);
    }

    private static string? ReadPictureCropClip(P.Picture picture, Bounds bounds)
    {
        A.SourceRectangle? sourceRectangle = picture.BlipFill?.GetFirstChild<A.SourceRectangle>();
        if (sourceRectangle is null)
        {
            return null;
        }

        double width = bounds.Width.ToPoints();
        double height = bounds.Height.ToPoints();
        if (width <= 0d || height <= 0d)
        {
            return null;
        }

        double left = width * ReadCropRatio(sourceRectangle.Left?.Value);
        double top = height * ReadCropRatio(sourceRectangle.Top?.Value);
        double right = width * (1d - ReadCropRatio(sourceRectangle.Right?.Value));
        double bottom = height * (1d - ReadCropRatio(sourceRectangle.Bottom?.Value));
        if (right <= left || bottom <= top)
        {
            return null;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "rect({0}, {1}, {2}, {3})",
            OdfLength.FromPoints(top),
            OdfLength.FromPoints(right),
            OdfLength.FromPoints(bottom),
            OdfLength.FromPoints(left));
    }

    private static double ReadCropRatio(int? value)
    {
        int clamped = Math.Min(Math.Max(value ?? 0, 0), 100000);
        return clamped / 100000d;
    }

    private static bool HasParagraphAlignment(IReadOnlyList<TextParagraph> paragraphs)
        => paragraphs.Any(paragraph => !string.IsNullOrWhiteSpace(paragraph.Alignment));

    private static string? ReadPictureAltText(P.Picture picture)
        => picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Description?.Value;

    private static void ApplySlideAnimations(
        SlidePart slidePart,
        OdfKit.Presentation.OdfSlide slide,
        IReadOnlyDictionary<uint, string> animationTargets)
    {
        P.Timing? timing = slidePart.Slide?.Timing;
        if (timing is null || animationTargets.Count == 0)
        {
            return;
        }

        var animationNodes = timing.Descendants<OpenXmlCompositeElement>()
            .Where(e => e is P.AnimateEffect || e is P.Animate || e is P.SetBehavior || e is P.AnimateColor || e is P.AnimateMotion || e is P.AnimateRotation || e is P.AnimateScale);

        foreach (OpenXmlCompositeElement effect in animationNodes)
        {
            P.CommonBehavior? behavior = GetCommonBehavior(effect);
            P.ShapeTarget? shapeTarget = behavior?.TargetElement?.ShapeTarget;
            if (shapeTarget is null)
            {
                continue;
            }

            if (!uint.TryParse(shapeTarget.ShapeId?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint sourceShapeId) ||
                !animationTargets.TryGetValue(sourceShapeId, out string? targetId))
            {
                continue;
            }

            OdfKit.Presentation.OdfAnimationEffect animationEffect = ReadAnimationEffect(effect);
            TimeSpan duration = ReadAnimationDuration(effect);
            TimeSpan delay = ReadAnimationDelay(effect);
            OdfKit.Presentation.OdfAnimationKind kind = ReadAnimationKind(effect);
            OdfKit.Presentation.OdfAnimationTrigger trigger = ReadAnimationTrigger(effect);
            switch (kind)
            {
                case OdfKit.Presentation.OdfAnimationKind.Exit:
                    slide.AddExitEffect(targetId, animationEffect, trigger, delay, duration);
                    break;
                case OdfKit.Presentation.OdfAnimationKind.Emphasis:
                    slide.AddEmphasisEffect(targetId, animationEffect, duration);
                    break;
                default:
                    slide.AddEntranceEffect(targetId, animationEffect, trigger, delay, duration);
                    break;
            }
        }
    }

    private static int? ReadPresetId(OpenXmlElement element)
    {
        return element.Ancestors<P.ParallelTimeNode>()
            .Select(ancestor => ancestor.CommonTimeNode?.PresetId?.Value)
            .FirstOrDefault(value => value.HasValue);
    }

    private static P.CommonBehavior? GetCommonBehavior(OpenXmlElement element)
    {
        return element switch
        {
            P.AnimateEffect e => e.CommonBehavior,
            P.Animate e => e.CommonBehavior,
            P.SetBehavior e => e.CommonBehavior,
            P.AnimateColor e => e.CommonBehavior,
            P.AnimateMotion e => e.CommonBehavior,
            P.AnimateRotation e => e.CommonBehavior,
            P.AnimateScale e => e.CommonBehavior,
            _ => null,
        };
    }

    private static OdfKit.Presentation.OdfAnimationEffect ReadAnimationEffect(OpenXmlElement element)
    {
        int? presetId = ReadPresetId(element);
        if (presetId.HasValue)
        {
            switch (presetId.Value)
            {
                case 1:
                    return OdfKit.Presentation.OdfAnimationEffect.Appear;
                case 2:
                    return OdfKit.Presentation.OdfAnimationEffect.FlyIn;
                case 9:
                    return OdfKit.Presentation.OdfAnimationEffect.Fade;
                case 10:
                    return OdfKit.Presentation.OdfAnimationEffect.Zoom;
            }
        }

        if (element is P.AnimateEffect effect)
        {
            string filter = effect.Filter?.Value ?? string.Empty;
            if (filter.Contains("fade", StringComparison.OrdinalIgnoreCase))
            {
                return OdfKit.Presentation.OdfAnimationEffect.Fade;
            }

            if (filter.Contains("zoom", StringComparison.OrdinalIgnoreCase))
            {
                return OdfKit.Presentation.OdfAnimationEffect.Zoom;
            }

            if (filter.Contains("fly", StringComparison.OrdinalIgnoreCase))
            {
                return OdfKit.Presentation.OdfAnimationEffect.FlyIn;
            }
        }
        else if (element is P.SetBehavior)
        {
            return OdfKit.Presentation.OdfAnimationEffect.Appear;
        }
        else if (element is P.Animate animate)
        {
            if (animate.CommonBehavior is not null)
            {
                string? attr = animate.CommonBehavior.GetAttribute("attributeName", "").Value;
                if (attr is not null && attr.Contains("opacity", StringComparison.OrdinalIgnoreCase))
                {
                    return OdfKit.Presentation.OdfAnimationEffect.Fade;
                }
            }
        }

        return OdfKit.Presentation.OdfAnimationEffect.Appear;
    }

    private static OdfKit.Presentation.OdfAnimationKind ReadAnimationKind(OpenXmlElement element)
    {
        P.TimeNodePresetClassValues? presetClass = element
            .Ancestors<P.ParallelTimeNode>()
            .Select(ancestor => ancestor.CommonTimeNode?.PresetClass?.Value)
            .FirstOrDefault(value => value.HasValue);

        if (presetClass?.Equals(P.TimeNodePresetClassValues.Exit) == true)
        {
            return OdfKit.Presentation.OdfAnimationKind.Exit;
        }

        if (presetClass?.Equals(P.TimeNodePresetClassValues.Emphasis) == true)
        {
            return OdfKit.Presentation.OdfAnimationKind.Emphasis;
        }

        if (element is P.AnimateEffect effect)
        {
            P.AnimateEffectTransitionValues? transition = effect.Transition?.Value;
            if (transition?.Equals(P.AnimateEffectTransitionValues.Out) == true)
            {
                return OdfKit.Presentation.OdfAnimationKind.Exit;
            }

            if (transition?.Equals(P.AnimateEffectTransitionValues.None) == true)
            {
                return OdfKit.Presentation.OdfAnimationKind.Emphasis;
            }
        }

        return OdfKit.Presentation.OdfAnimationKind.Entrance;
    }

    private static OdfKit.Presentation.OdfAnimationTrigger ReadAnimationTrigger(OpenXmlElement element)
    {
        P.TimeNodeValues? nodeType = element
            .Ancestors<P.ParallelTimeNode>()
            .Select(ancestor => ancestor.CommonTimeNode?.NodeType?.Value)
            .FirstOrDefault(value => value.HasValue);

        if (nodeType?.Equals(P.TimeNodeValues.AfterEffect) == true)
        {
            return OdfKit.Presentation.OdfAnimationTrigger.AfterPrevious;
        }

        if (nodeType?.Equals(P.TimeNodeValues.WithEffect) == true)
        {
            return OdfKit.Presentation.OdfAnimationTrigger.WithPrevious;
        }

        return OdfKit.Presentation.OdfAnimationTrigger.OnClick;
    }

    private static TimeSpan ReadAnimationDuration(OpenXmlElement element)
    {
        P.CommonBehavior? behavior = GetCommonBehavior(element);
        string? duration = behavior?.CommonTimeNode?.Duration?.Value;
        if (string.IsNullOrWhiteSpace(duration))
        {
            duration = element
                .Ancestors<P.ParallelTimeNode>()
                .Select(ancestor => ancestor.CommonTimeNode?.Duration?.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        return double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds)
            ? TimeSpan.FromMilliseconds(Math.Max(milliseconds, 0d))
            : default;
    }

    private static TimeSpan ReadAnimationDelay(OpenXmlElement element)
    {
        P.CommonBehavior? behavior = GetCommonBehavior(element);
        string? start = ReadStartDelay(behavior?.CommonTimeNode);
        if (string.IsNullOrWhiteSpace(start))
        {
            start = element
                .Ancestors<P.ParallelTimeNode>()
                .Select(ancestor => ReadStartDelay(ancestor.CommonTimeNode))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        return double.TryParse(start, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds)
            ? TimeSpan.FromMilliseconds(Math.Max(milliseconds, 0d))
            : default;
    }

    private static string? ReadStartDelay(P.CommonTimeNode? timeNode)
        => timeNode?
            .StartConditionList?
            .Elements<P.Condition>()
            .Select(condition => condition.Delay?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static P.PlaceholderShape? GetPlaceholderShape(P.Shape shape)
    {
        return shape.NonVisualShapeProperties?
            .ApplicationNonVisualDrawingProperties?
            .GetFirstChild<P.PlaceholderShape>();
    }

    private static P.Shape? FindMatchingPlaceholder(SlidePart slidePart, P.PlaceholderShape? ph)
    {
        if (ph is null)
        {
            return null;
        }

        P.Shape? layoutShape = FindPlaceholderInPart(slidePart.SlideLayoutPart, ph);
        if (layoutShape is not null)
        {
            return layoutShape;
        }

        return FindPlaceholderInPart(slidePart.SlideLayoutPart?.SlideMasterPart, ph);
    }

    private static P.Shape? FindPlaceholderInPart(OpenXmlPart? part, P.PlaceholderShape ph)
    {
        if (part is null)
        {
            return null;
        }

        P.ShapeTree? shapeTree = null;
        if (part is SlideLayoutPart layoutPart)
        {
            shapeTree = layoutPart.SlideLayout?.CommonSlideData?.ShapeTree;
        }
        else if (part is SlideMasterPart masterPart)
        {
            shapeTree = masterPart.SlideMaster?.CommonSlideData?.ShapeTree;
        }

        if (shapeTree is null)
        {
            return null;
        }

        return shapeTree.Descendants<P.Shape>().FirstOrDefault(s =>
        {
            P.PlaceholderShape? sPh = GetPlaceholderShape(s);
            if (sPh is null)
            {
                return false;
            }

            if (ph.Type is not null && sPh.Type is not null && ph.Type.Value == sPh.Type.Value)
            {
                return true;
            }

            if (ph.Index is not null && sPh.Index is not null && ph.Index.Value == sPh.Index.Value)
            {
                return true;
            }

            return false;
        });
    }

    private static string? ReadStrokeStyle(A.PresetLineDashValues? dash)
    {
        if (!dash.HasValue || dash.Value.Equals(A.PresetLineDashValues.Solid))
        {
            return null;
        }

        return dash.Value.Equals(A.PresetLineDashValues.Dot)
            ? "dot"
            : "dash";
    }

    private static uint? ReadShapeId(P.NonVisualDrawingProperties? properties)
        => properties?.Id?.Value;

    private static void RegisterAnimationTarget(
        uint? sourceShapeId,
        OdfKit.Presentation.OdfShape addedShape,
        Dictionary<uint, string> animationTargets)
    {
        if (sourceShapeId.HasValue && !string.IsNullOrWhiteSpace(addedShape.Id))
        {
            animationTargets[sourceShapeId.Value] = addedShape.Id;
        }
    }

    private static string GetText(P.Shape shape)
    {
        P.TextBody? textBody = shape.TextBody;
        if (textBody is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            textBody.Elements<A.Paragraph>()
                .Select(paragraph => string.Concat(paragraph.Descendants<A.Text>().Select(text => text.Text)))
                .Where(text => text.Length > 0));
    }

    private static IReadOnlyList<TextParagraph> GetTextParagraphs(P.Shape shape, ThemeColorMap themeColors)
    {
        P.TextBody? textBody = shape.TextBody;
        if (textBody is null)
        {
            return [];
        }

        var paragraphs = new List<TextParagraph>();
        foreach (A.Paragraph paragraph in textBody.Elements<A.Paragraph>())
        {
            var runs = new List<TextRun>();
            foreach (A.Run run in paragraph.Elements<A.Run>())
            {
                string text = string.Concat(run.Elements<A.Text>().Select(value => value.Text));
                if (text.Length > 0)
                {
                    runs.Add(new TextRun(text, GetTextStyle(run.RunProperties, themeColors)));
                }
            }

            paragraphs.Add(new TextParagraph(runs, ReadTextAlignment(paragraph.ParagraphProperties?.Alignment?.Value)));
        }

        return paragraphs;
    }

    private static IReadOnlyList<TextRun> FlattenTextRuns(IReadOnlyList<TextParagraph> paragraphs)
    {
        var runs = new List<TextRun>();
        foreach (TextParagraph paragraph in paragraphs)
        {
            if (runs.Count > 0)
            {
                runs.Add(new TextRun(Environment.NewLine, TextStyle.Empty));
            }

            runs.AddRange(paragraph.Runs);
        }

        return runs;
    }

    private static string? ReadTextAlignment(A.TextAlignmentTypeValues? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value.Equals(A.TextAlignmentTypeValues.Center))
        {
            return "center";
        }

        if (value.Value.Equals(A.TextAlignmentTypeValues.Right))
        {
            return "end";
        }

        if (value.Value.Equals(A.TextAlignmentTypeValues.Justified))
        {
            return "justify";
        }

        return value.Value.Equals(A.TextAlignmentTypeValues.Left) ? "start" : null;
    }

    private static TextStyle GetTextStyle(P.Shape shape)
    {
        A.RunProperties? runProperties = shape.TextBody?
            .Descendants<A.Run>()
            .Select(run => run.RunProperties)
            .FirstOrDefault(properties => properties is not null);
        return GetTextStyle(runProperties, ThemeColorMap.Empty);
    }

    private static TextStyle GetTextStyle(A.RunProperties? runProperties, ThemeColorMap themeColors)
    {
        if (runProperties is null)
        {
            return TextStyle.Empty;
        }

        string? color = ReadSolidFillColor(runProperties.GetFirstChild<A.SolidFill>(), themeColors);
        string? fontFamily = ReadRunFontFamily(runProperties, themeColors);
        return new TextStyle(
            runProperties.Bold?.Value == true,
            runProperties.Italic?.Value == true,
            runProperties.FontSize?.Value,
            fontFamily,
            color,
            IsUnderlineEnabled(runProperties.Underline?.Value),
            IsStrikethroughEnabled(runProperties.Strike?.Value),
            GetTextPosition(runProperties.Baseline?.Value));
    }

    private static OdfKit.Presentation.OdfShape AddStyledTextBox(
        OdfKit.Presentation.OdfSlide slide,
        Bounds bounds,
        IReadOnlyList<TextParagraph> paragraphs)
    {
        var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, bounds.X.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, bounds.Y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, bounds.Width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, bounds.Height.ToString(), "svg");

        var textBox = new OdfNode(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
        AppendTextParagraphs(slide, textBox, paragraphs);
        frame.AppendChild(textBox);
        slide.Node.AppendChild(frame);
        return new OdfKit.Presentation.OdfTextBox(frame, slide);
    }

    private static void AppendTextParagraphs(
        OdfKit.Presentation.OdfSlide slide,
        OdfNode parent,
        IReadOnlyList<TextParagraph> paragraphs)
    {
        if (paragraphs.Count == 0)
        {
            parent.AppendChild(new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text"));
            return;
        }

        foreach (TextParagraph textParagraph in paragraphs)
        {
            var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            if (!string.IsNullOrWhiteSpace(textParagraph.Alignment))
            {
                slide.Document.StyleEngine.SetLocalStyleProperty(
                    paragraph,
                    "paragraph",
                    "paragraph-properties",
                    "text-align",
                    OdfNamespaces.Fo,
                    textParagraph.Alignment!,
                    "fo");
            }

            foreach (TextRun run in textParagraph.Runs)
            {
                var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text") { TextContent = run.Text };
                paragraph.AppendChild(span);
                ApplyTextStyle(slide, span, run.Style);
            }

            parent.AppendChild(paragraph);
        }
    }

    private static void ApplyTextStyle(OdfKit.Presentation.OdfSlide slide, OdfNode span, TextStyle style)
    {
        if (style.Bold)
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-weight", OdfNamespaces.Fo, "bold", "fo");
        }

        if (style.Italic)
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-style", OdfNamespaces.Fo, "italic", "fo");
        }

        if (style.FontSizeHundredthsOfPoint is > 0)
        {
            string fontSize = (style.FontSizeHundredthsOfPoint.Value / 100d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "pt";
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-size", OdfNamespaces.Fo, fontSize, "fo");
        }

        if (!string.IsNullOrWhiteSpace(style.FontFamily))
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-family", OdfNamespaces.Fo, style.FontFamily!, "fo");
        }

        if (!string.IsNullOrWhiteSpace(style.ColorHex))
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "color", OdfNamespaces.Fo, "#" + style.ColorHex, "fo");
        }

        if (style.Underline)
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, "solid", "style");
        }

        if (style.Strikethrough)
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "text-line-through-style", OdfNamespaces.Style, "solid", "style");
        }

        if (style.TextPosition == TextPosition.Super)
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "text-position", OdfNamespaces.Style, "super", "style");
        }
        else if (style.TextPosition == TextPosition.Sub)
        {
            slide.Document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "text-position", OdfNamespaces.Style, "sub", "style");
        }
    }

    private static IReadOnlyList<string> GetSpeakerNotes(SlidePart slidePart)
    {
        NotesSlidePart? notesPart = slidePart.NotesSlidePart;
        if (notesPart?.NotesSlide is null)
        {
            return [];
        }

        return notesPart.NotesSlide.Descendants<A.Paragraph>()
            .Select(paragraph => string.Concat(paragraph.Descendants<A.Text>().Select(text => text.Text)))
            .Where(text => text.Length > 0)
            .ToArray();
    }

    private static Bounds GetBounds(OpenXmlCompositeElement? properties)
    {
        A.Transform2D? transform = properties?.GetFirstChild<A.Transform2D>();
        if (transform is null)
        {
            return new Bounds(DefaultX, DefaultY, DefaultWidth, DefaultHeight);
        }

        return new Bounds(
            ToOdfLength(transform.Offset?.X?.Value),
            ToOdfLength(transform.Offset?.Y?.Value),
            ToOdfLength(transform.Extents?.Cx?.Value, DefaultWidth),
            ToOdfLength(transform.Extents?.Cy?.Value, DefaultHeight),
            transform.HorizontalFlip?.Value == true,
            transform.VerticalFlip?.Value == true);
    }

    private static Bounds GetBounds(P.Transform? transform)
    {
        if (transform is null)
        {
            return new Bounds(DefaultX, DefaultY, DefaultWidth, DefaultHeight);
        }

        return new Bounds(
            ToOdfLength(transform.Offset?.X?.Value),
            ToOdfLength(transform.Offset?.Y?.Value),
            ToOdfLength(transform.Extents?.Cx?.Value, DefaultWidth),
            ToOdfLength(transform.Extents?.Cy?.Value, DefaultHeight),
            transform.HorizontalFlip?.Value == true,
            transform.VerticalFlip?.Value == true);
    }

    private static OdfLength AddLengths(OdfLength left, OdfLength right)
        => OdfLength.FromPoints(left.ToPoints() + right.ToPoints());

    private static OdfLength ToOdfLength(long? emus)
    {
        return emus is null
            ? OdfLength.FromPoints(0)
            : OdfLength.FromPoints(emus.Value / EmusPerPoint);
    }

    private static OdfLength ToOdfLength(long? emus, OdfLength fallback)
    {
        return emus is null or <= 0
            ? fallback
            : OdfLength.FromPoints(emus.Value / EmusPerPoint);
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

    private static string? ReadSolidFillColor(A.SolidFill? fill, ThemeColorMap themeColors)
    {
        if (fill is null)
        {
            return null;
        }

        string? color = fill.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(color))
        {
            return NormalizeColor(color);
        }

        A.SchemeColor? schemeColor = fill.GetFirstChild<A.SchemeColor>();
        A.SchemeColorValues? scheme = schemeColor?.Val?.Value;
        return scheme.HasValue ? themeColors.Resolve(scheme.Value) : null;
    }

    private static string? ReadRunFontFamily(A.RunProperties runProperties, ThemeColorMap themeColors)
    {
        string? latin = ResolveTypeface(runProperties.GetFirstChild<A.LatinFont>()?.Typeface?.Value, themeColors);
        if (!string.IsNullOrWhiteSpace(latin))
        {
            return latin;
        }

        string? eastAsian = ResolveTypeface(runProperties.GetFirstChild<A.EastAsianFont>()?.Typeface?.Value, themeColors);
        if (!string.IsNullOrWhiteSpace(eastAsian))
        {
            return eastAsian;
        }

        return ResolveTypeface(runProperties.GetFirstChild<A.ComplexScriptFont>()?.Typeface?.Value, themeColors);
    }

    private static string? ResolveTypeface(string? typeface, ThemeColorMap themeColors)
    {
        if (string.IsNullOrWhiteSpace(typeface))
        {
            return null;
        }

        string value = typeface!.Trim();
        string? resolved = themeColors.ResolveFont(value);
        return string.IsNullOrWhiteSpace(resolved) ? value : resolved;
    }

    private static bool IsUnderlineEnabled(A.TextUnderlineValues? value)
    {
        return value is not null && value.Value != A.TextUnderlineValues.None;
    }

    private static bool IsStrikethroughEnabled(A.TextStrikeValues? value)
    {
        return value is not null && value.Value != A.TextStrikeValues.NoStrike;
    }

    private static TextPosition GetTextPosition(int? baseline)
    {
        return baseline switch
        {
            > 0 => TextPosition.Super,
            < 0 => TextPosition.Sub,
            _ => TextPosition.Normal,
        };
    }

    private sealed class Bounds
    {
        public Bounds(OdfLength x, OdfLength y, OdfLength width, OdfLength height, bool horizontalFlip = false, bool verticalFlip = false)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            HorizontalFlip = horizontalFlip;
            VerticalFlip = verticalFlip;
        }

        public OdfLength X { get; }

        public OdfLength Y { get; }

        public OdfLength Width { get; }

        public OdfLength Height { get; }

        public bool HorizontalFlip { get; }

        public bool VerticalFlip { get; }
    }

    private sealed class ThemeColorMap
    {
        public static readonly ThemeColorMap Empty = new(
            new Dictionary<A.SchemeColorValues, string>(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<uint, string>(),
            new Dictionary<uint, string>(),
            new Dictionary<uint, string>(),
            new Dictionary<uint, string>(),
            new Dictionary<uint, string>());

        private readonly IReadOnlyDictionary<A.SchemeColorValues, string> _colors;
        private readonly IReadOnlyDictionary<string, string> _fonts;
        private readonly IReadOnlyDictionary<uint, string> _lineWidths;
        private readonly IReadOnlyDictionary<uint, string> _fillStyleColors;
        private readonly IReadOnlyDictionary<uint, string> _lineStyleColors;
        private readonly IReadOnlyDictionary<uint, string> _effectStyleColors;
        private readonly IReadOnlyDictionary<uint, string> _backgroundFillColors;

        private ThemeColorMap(
            IReadOnlyDictionary<A.SchemeColorValues, string> colors,
            IReadOnlyDictionary<string, string> fonts,
            IReadOnlyDictionary<uint, string> lineWidths,
            IReadOnlyDictionary<uint, string> fillStyleColors,
            IReadOnlyDictionary<uint, string> lineStyleColors,
            IReadOnlyDictionary<uint, string> effectStyleColors,
            IReadOnlyDictionary<uint, string> backgroundFillColors)
        {
            _colors = colors;
            _fonts = fonts;
            _lineWidths = lineWidths;
            _fillStyleColors = fillStyleColors;
            _lineStyleColors = lineStyleColors;
            _effectStyleColors = effectStyleColors;
            _backgroundFillColors = backgroundFillColors;
        }

        public static ThemeColorMap FromSlide(SlidePart slidePart)
        {
            SlideMasterPart? masterPart = slidePart.SlideLayoutPart?.SlideMasterPart ??
                slidePart.SlideLayoutPart?.GetParentParts().OfType<SlideMasterPart>().FirstOrDefault();

            ThemePart? themePart = masterPart?.ThemePart ??
                slidePart.GetParentParts().OfType<PresentationPart>().FirstOrDefault()?.ThemePart;

            A.ColorScheme? colorScheme = themePart?
                .Theme?
                .ThemeElements?
                .ColorScheme;

            if (colorScheme is null)
            {
                A.FontScheme? fontSchemeOnly = themePart?
                    .Theme?
                    .ThemeElements?
                    .FontScheme;
                var themeFontsOnly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                AddThemeFonts(themeFontsOnly, fontSchemeOnly);
                return themeFontsOnly.Count == 0
                    ? Empty
                    : new ThemeColorMap(
                        new Dictionary<A.SchemeColorValues, string>(),
                        themeFontsOnly,
                        new Dictionary<uint, string>(),
                        new Dictionary<uint, string>(),
                        new Dictionary<uint, string>(),
                        new Dictionary<uint, string>(),
                        new Dictionary<uint, string>());
            }

            var colors = new Dictionary<A.SchemeColorValues, string>();
            Add(colors, A.SchemeColorValues.Dark1, ReadThemeColor(colorScheme.Dark1Color));
            Add(colors, A.SchemeColorValues.Light1, ReadThemeColor(colorScheme.Light1Color));
            Add(colors, A.SchemeColorValues.Dark2, ReadThemeColor(colorScheme.Dark2Color));
            Add(colors, A.SchemeColorValues.Light2, ReadThemeColor(colorScheme.Light2Color));
            Add(colors, A.SchemeColorValues.Accent1, ReadThemeColor(colorScheme.Accent1Color));
            Add(colors, A.SchemeColorValues.Accent2, ReadThemeColor(colorScheme.Accent2Color));
            Add(colors, A.SchemeColorValues.Accent3, ReadThemeColor(colorScheme.Accent3Color));
            Add(colors, A.SchemeColorValues.Accent4, ReadThemeColor(colorScheme.Accent4Color));
            Add(colors, A.SchemeColorValues.Accent5, ReadThemeColor(colorScheme.Accent5Color));
            Add(colors, A.SchemeColorValues.Accent6, ReadThemeColor(colorScheme.Accent6Color));
            Add(colors, A.SchemeColorValues.Hyperlink, ReadThemeColor(colorScheme.Hyperlink));
            Add(colors, A.SchemeColorValues.FollowedHyperlink, ReadThemeColor(colorScheme.FollowedHyperlinkColor));

            A.FormatScheme? formatScheme = themePart?.Theme?.ThemeElements?.FormatScheme;
            var fonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddThemeFonts(fonts, themePart?.Theme?.ThemeElements?.FontScheme);
            var lineWidths = new Dictionary<uint, string>();
            AddThemeLineWidths(lineWidths, formatScheme?.LineStyleList);
            var fillStyleColors = new Dictionary<uint, string>();
            AddThemeFillStyleColors(fillStyleColors, formatScheme?.FillStyleList, colors);
            var lineStyleColors = new Dictionary<uint, string>();
            AddThemeLineStyleColors(lineStyleColors, formatScheme?.LineStyleList, colors);
            var effectStyleColors = new Dictionary<uint, string>();
            AddThemeEffectStyleColors(effectStyleColors, formatScheme?.EffectStyleList, colors);
            var backgroundFillColors = new Dictionary<uint, string>();
            AddThemeBackgroundFillColors(backgroundFillColors, formatScheme?.BackgroundFillStyleList, colors);
            return colors.Count == 0 && fonts.Count == 0 && lineWidths.Count == 0 && fillStyleColors.Count == 0 &&
                lineStyleColors.Count == 0 && effectStyleColors.Count == 0 && backgroundFillColors.Count == 0
                ? Empty
                : new ThemeColorMap(colors, fonts, lineWidths, fillStyleColors, lineStyleColors, effectStyleColors, backgroundFillColors);
        }

        public string? Resolve(A.SchemeColorValues value)
        {
            return _colors.TryGetValue(value, out string? color) ? color : null;
        }

        public string? ResolveFont(string typeface)
        {
            return _fonts.TryGetValue(typeface, out string? font) ? font : null;
        }

        public string? ResolveLineWidth(uint? index)
        {
            if (!index.HasValue || index.Value == 0U)
            {
                return null;
            }

            return _lineWidths.TryGetValue(index.Value, out string? width) ? width : null;
        }

        public string? ResolveFillStyleColor(uint? index)
        {
            if (!index.HasValue || index.Value == 0U)
            {
                return null;
            }

            return _fillStyleColors.TryGetValue(index.Value, out string? color) ? color : null;
        }

        public string? ResolveLineStyleColor(uint? index)
        {
            if (!index.HasValue || index.Value == 0U)
            {
                return null;
            }

            return _lineStyleColors.TryGetValue(index.Value, out string? color) ? color : null;
        }

        public string? ResolveEffectStyleColor(uint? index)
        {
            if (!index.HasValue || index.Value == 0U)
            {
                return null;
            }

            return _effectStyleColors.TryGetValue(index.Value, out string? color) ? color : null;
        }

        public string? ResolveBackgroundFillColor(uint? index)
        {
            if (!index.HasValue || index.Value == 0U)
            {
                return null;
            }

            uint normalizedIndex = index.Value >= 1001U ? index.Value - 1000U : index.Value;
            return _backgroundFillColors.TryGetValue(normalizedIndex, out string? color) ? color : null;
        }

        private static void Add(Dictionary<A.SchemeColorValues, string> colors, A.SchemeColorValues key, string? color)
        {
            if (!string.IsNullOrWhiteSpace(color))
            {
                colors[key] = color!;
            }
        }

        private static string? ReadThemeColor(OpenXmlElement? colorElement)
        {
            string? rgb = colorElement?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value;
            if (!string.IsNullOrWhiteSpace(rgb))
            {
                return NormalizeColor(rgb);
            }

            A.SystemColor? systemColor = colorElement?.GetFirstChild<A.SystemColor>();
            return NormalizeColor(systemColor?.LastColor?.Value);
        }

        private static void AddThemeLineWidths(Dictionary<uint, string> lineWidths, A.LineStyleList? lineStyleList)
        {
            if (lineStyleList is null)
            {
                return;
            }

            uint index = 1U;
            foreach (A.Outline outline in lineStyleList.Elements<A.Outline>())
            {
                if (outline.Width?.Value is int width && width > 0)
                {
                    lineWidths[index] = (width / EmusPerPoint).ToString("0.##", CultureInfo.InvariantCulture) + "pt";
                }

                index++;
            }
        }

        private static void AddThemeFillStyleColors(
            Dictionary<uint, string> fillStyleColors,
            A.FillStyleList? fillStyleList,
            IReadOnlyDictionary<A.SchemeColorValues, string> colors)
        {
            if (fillStyleList is null)
            {
                return;
            }

            uint index = 1U;
            foreach (OpenXmlElement fill in fillStyleList.ChildElements)
            {
                string? color = ReadThemeFillColor(fill, colors);
                if (!string.IsNullOrWhiteSpace(color))
                {
                    fillStyleColors[index] = color!;
                }

                index++;
            }
        }

        private static void AddThemeLineStyleColors(
            Dictionary<uint, string> lineStyleColors,
            A.LineStyleList? lineStyleList,
            IReadOnlyDictionary<A.SchemeColorValues, string> colors)
        {
            if (lineStyleList is null)
            {
                return;
            }

            uint index = 1U;
            foreach (A.Outline outline in lineStyleList.Elements<A.Outline>())
            {
                string? color = ReadThemeFillColor(outline, colors);
                if (!string.IsNullOrWhiteSpace(color))
                {
                    lineStyleColors[index] = color!;
                }

                index++;
            }
        }

        private static void AddThemeEffectStyleColors(
            Dictionary<uint, string> effectStyleColors,
            A.EffectStyleList? effectStyleList,
            IReadOnlyDictionary<A.SchemeColorValues, string> colors)
        {
            if (effectStyleList is null)
            {
                return;
            }

            uint index = 1U;
            foreach (A.EffectStyle effectStyle in effectStyleList.Elements<A.EffectStyle>())
            {
                string? color = ReadThemeFillColor(effectStyle, colors);
                if (string.IsNullOrWhiteSpace(color))
                {
                    var effectList = effectStyle.GetFirstChild<A.EffectList>();
                    color = effectList?.Descendants<A.RgbColorModelHex>().FirstOrDefault()?.Val?.Value;
                    if (string.IsNullOrWhiteSpace(color))
                    {
                        A.SchemeColorValues? scheme = effectList?.Descendants<A.SchemeColor>().FirstOrDefault()?.Val?.Value;
                        if (scheme.HasValue && colors.TryGetValue(scheme.Value, out string? schemeColor))
                        {
                            color = schemeColor;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(color))
                {
                    effectStyleColors[index] = NormalizeColor(color)!;
                }

                index++;
            }
        }

        private static void AddThemeBackgroundFillColors(
            Dictionary<uint, string> backgroundFillColors,
            A.BackgroundFillStyleList? backgroundFillStyleList,
            IReadOnlyDictionary<A.SchemeColorValues, string> colors)
        {
            if (backgroundFillStyleList is null)
            {
                return;
            }

            uint index = 1U;
            foreach (OpenXmlElement fill in backgroundFillStyleList.ChildElements)
            {
                string? color = ReadThemeFillColor(fill, colors);
                if (!string.IsNullOrWhiteSpace(color))
                {
                    backgroundFillColors[index] = color!;
                }

                index++;
            }
        }

        private static string? ReadThemeFillColor(
            OpenXmlElement fill,
            IReadOnlyDictionary<A.SchemeColorValues, string> colors)
        {
            string? rgb = fill.Descendants<A.RgbColorModelHex>().FirstOrDefault()?.Val?.Value;
            if (!string.IsNullOrWhiteSpace(rgb))
            {
                return NormalizeColor(rgb);
            }

            A.SchemeColorValues? scheme = fill.Descendants<A.SchemeColor>().FirstOrDefault()?.Val?.Value;
            return scheme.HasValue && colors.TryGetValue(scheme.Value, out string? color) ? color : null;
        }

        private static void AddThemeFonts(Dictionary<string, string> fonts, A.FontScheme? fontScheme)
        {
            AddFont(fonts, "+mj-lt", fontScheme?.MajorFont?.LatinFont?.Typeface?.Value);
            AddFont(fonts, "+mn-lt", fontScheme?.MinorFont?.LatinFont?.Typeface?.Value);
            AddFont(fonts, "+mj-ea", fontScheme?.MajorFont?.EastAsianFont?.Typeface?.Value);
            AddFont(fonts, "+mn-ea", fontScheme?.MinorFont?.EastAsianFont?.Typeface?.Value);
            AddFont(fonts, "+mj-cs", fontScheme?.MajorFont?.ComplexScriptFont?.Typeface?.Value);
            AddFont(fonts, "+mn-cs", fontScheme?.MinorFont?.ComplexScriptFont?.Typeface?.Value);
        }

        private static void AddFont(Dictionary<string, string> fonts, string key, string? typeface)
        {
            if (!string.IsNullOrWhiteSpace(typeface))
            {
                fonts[key] = typeface!;
            }
        }
    }

    private sealed class TextStyle
    {
        public static readonly TextStyle Empty = new(false, false, null, null, null, false, false, TextPosition.Normal);

        public TextStyle(
            bool bold,
            bool italic,
            int? fontSizeHundredthsOfPoint,
            string? fontFamily,
            string? colorHex,
            bool underline,
            bool strikethrough,
            TextPosition textPosition)
        {
            Bold = bold;
            Italic = italic;
            FontSizeHundredthsOfPoint = fontSizeHundredthsOfPoint;
            FontFamily = fontFamily;
            ColorHex = colorHex;
            Underline = underline;
            Strikethrough = strikethrough;
            TextPosition = textPosition;
        }

        public bool Bold { get; }

        public bool Italic { get; }

        public int? FontSizeHundredthsOfPoint { get; }

        public string? FontFamily { get; }

        public string? ColorHex { get; }

        public bool Underline { get; }

        public bool Strikethrough { get; }

        public TextPosition TextPosition { get; }

        public bool IsEmpty =>
            !Bold &&
            !Italic &&
            FontSizeHundredthsOfPoint is null &&
            string.IsNullOrWhiteSpace(FontFamily) &&
            string.IsNullOrWhiteSpace(ColorHex) &&
            !Underline &&
            !Strikethrough &&
            TextPosition == TextPosition.Normal;
    }

    private enum TextPosition
    {
        Normal,
        Super,
        Sub,
    }

    private sealed class TextParagraph
    {
        public TextParagraph(IReadOnlyList<TextRun> runs, string? alignment)
        {
            Runs = runs;
            Alignment = alignment;
        }

        public IReadOnlyList<TextRun> Runs { get; }

        public string? Alignment { get; }
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
}

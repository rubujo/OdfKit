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
using P = DocumentFormat.OpenXml.Presentation;

namespace OdfKit.Conversion;

/// <summary>
/// Applies odp to pptx converter.
/// 將 <see cref="OdfPresentationDocument"/> (ODP) 轉換為 PPTX 格式的 managed 淨室轉換器。
/// </summary>
public static class OdpToPptxConverter
{
    private const long EmusPerPoint = 12700L;
    private const int DefaultSlideWidth = 9144000;
    private const int DefaultSlideHeight = 6858000;
    private const string SmilNamespace = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";
    private const string IndefiniteDuration = "indefinite";

    /// <summary>
    /// Applies convert.
    /// 將 ODP 簡報文件轉換並寫入 PPTX 資料流。
    /// </summary>
    /// <param name="odpDocument">The source or target object. / 來源 ODP 簡報文件</param>
    /// <param name="pptxStream">The source or target object. / 要寫入 PPTX 的目標資料流</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 任一必要參數為 null 時引發</exception>
    public static void Convert(OdfPresentationDocument odpDocument, Stream pptxStream)
    {
        if (odpDocument is null)
            throw new ArgumentNullException(nameof(odpDocument));
        if (pptxStream is null)
            throw new ArgumentNullException(nameof(pptxStream));

        using var pptx = PresentationDocument.Create(pptxStream, PresentationDocumentType.Presentation, autoSave: false);
        PresentationPart presentationPart = pptx.AddPresentationPart();
        (int slideWidth, int slideHeight) = GetSlideSizeEmus(odpDocument);
        presentationPart.Presentation = new P.Presentation
        {
            SlideMasterIdList = new P.SlideMasterIdList(),
            SlideIdList = new P.SlideIdList(),
            SlideSize = new P.SlideSize { Cx = slideWidth, Cy = slideHeight },
            NotesSize = new P.NotesSize { Cx = slideWidth, Cy = slideHeight },
        };
        var layoutParts = new Dictionary<P.SlideLayoutValues, SlideLayoutPart>();
        ThemePalette themePalette = CollectThemePalette(odpDocument);

        uint slideId = 256U;
        uint shapeId = 2U; // id=1 保留給 CreateCommonSlideData 內隱含的群組圖形（nvGrpSpPr）
        foreach (OdfNode slideNode in GetSlides(odpDocument))
        {
            P.SlideLayoutValues layoutType = ReadSlideLayoutType(slideNode);
            SlideLayoutPart layoutPart = GetOrCreateLayoutPart(presentationPart, layoutParts, layoutType, themePalette);
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.AddPart(layoutPart);
            slidePart.Slide = CreateSlide(slideNode, odpDocument, slidePart, ref shapeId);
            slidePart.Slide.Save();
            AddSpeakerNotes(slideNode, odpDocument, slidePart, ref shapeId);
            string relId = presentationPart.GetIdOfPart(slidePart);
            presentationPart.Presentation.SlideIdList!.Append(new P.SlideId { Id = slideId++, RelationshipId = relId });
        }

        if (!presentationPart.Presentation.SlideIdList!.HasChildren)
        {
            SlideLayoutPart layoutPart = GetOrCreateLayoutPart(presentationPart, layoutParts, P.SlideLayoutValues.Blank, themePalette);
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.AddPart(layoutPart);
            slidePart.Slide = CreateEmptySlide();
            slidePart.Slide.Save();
            string relId = presentationPart.GetIdOfPart(slidePart);
            presentationPart.Presentation.SlideIdList.Append(new P.SlideId { Id = slideId, RelationshipId = relId });
        }

        presentationPart.Presentation.Save();
    }

    /// <summary>
    /// 取得簡報投影片之寬高尺寸（以 EMUs 為單位）。
    /// </summary>
    private static (int Width, int Height) GetSlideSizeEmus(OdfPresentationDocument document)
    {
        (OdfLength width, OdfLength height) = document.GetSlideSize();
        return (
            ToSlideSizeEmus(width),
            ToSlideSizeEmus(height));
    }

    /// <summary>
    /// 取得或建立特定投影片配置的部分（Slide Layout Part）。
    /// </summary>
    private static SlideLayoutPart GetOrCreateLayoutPart(
        PresentationPart presentationPart,
        Dictionary<P.SlideLayoutValues, SlideLayoutPart> layoutParts,
        P.SlideLayoutValues layoutType,
        ThemePalette themePalette)
    {
        if (!layoutParts.TryGetValue(layoutType, out SlideLayoutPart? layoutPart))
        {
            layoutPart = CreateLayout(presentationPart, layoutType, themePalette);
            layoutParts.Add(layoutType, layoutPart);
        }

        return layoutPart;
    }

    /// <summary>
    /// 讀取 ODP 節點中對應的投影片配置類型。
    /// </summary>
    private static P.SlideLayoutValues ReadSlideLayoutType(OdfNode slideNode)
    {
        return slideNode.GetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation) switch
        {
            "AL1T1" or "layout_TitleOnly" => P.SlideLayoutValues.TitleOnly,
            "AL1T2" or "layout_TitleAndSubtitle" => P.SlideLayoutValues.Title,
            "AL1T3" or "layout_TitleAndBody" => P.SlideLayoutValues.Text,
            _ => P.SlideLayoutValues.Blank,
        };
    }

    /// <summary>
    /// 加入演講者備忘錄至投影片。
    /// </summary>
    private static void AddSpeakerNotes(OdfNode slideNode, OdfPresentationDocument odpDocument, SlidePart slidePart, ref uint shapeId)
    {
        IReadOnlyList<TextRun> notesRuns = GetSpeakerNoteRuns(slideNode, odpDocument);
        if (notesRuns.Count == 0 || notesRuns.All(run => string.IsNullOrWhiteSpace(run.Text)))
        {
            return;
        }

        NotesSlidePart notesPart = slidePart.AddNewPart<NotesSlidePart>();
        P.CommonSlideData commonSlideData = CreateCommonSlideData();
        commonSlideData.ShapeTree!.Append(CreateNotesTextShape(notesRuns, ref shapeId));
        notesPart.NotesSlide = new P.NotesSlide(
            commonSlideData,
            new P.ColorMapOverride(new A.MasterColorMapping()));
        notesPart.NotesSlide.Save();
    }

    /// <summary>
    /// 建立備忘錄文字框圖形。
    /// </summary>
    private static P.Shape CreateNotesTextShape(IReadOnlyList<TextRun> notesRuns, ref uint shapeId)
    {
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId++, Name = "Speaker Notes" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                CreateApplicationNonVisualDrawingProperties(P.PlaceholderValues.Body)),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 685800L, Y = 3886200L },
                    new A.Extents { Cx = 7772400L, Cy = 2514600L }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
            CreateTextBody(ToTextParagraphs(notesRuns)));
    }

    /// <summary>
    /// 取得演講者備忘錄的文字片段清單。
    /// </summary>
    private static IReadOnlyList<TextRun> GetSpeakerNoteRuns(OdfNode slideNode, OdfPresentationDocument document)
    {
        OdfNode? notesNode = FindChild(slideNode, "notes", OdfNamespaces.Presentation);
        if (notesNode is null)
        {
            return [];
        }

        return GetTextRuns(notesNode, document);
    }

    /// <summary>
    /// 從 OdfNode 讀取並轉換文字樣式。
    /// </summary>
    private static TextStyle GetTextStyle(OdfNode textBox, OdfPresentationDocument document)
    {
        OdfNode? styledNode = textBox.NamespaceUri == OdfNamespaces.Text && textBox.LocalName is "span" or "p"
            ? textBox
            : FindDescendant(textBox, "span", OdfNamespaces.Text)
                ?? FindDescendant(textBox, "p", OdfNamespaces.Text);
        if (styledNode is null)
        {
            return TextStyle.Empty;
        }

        string family = styledNode.LocalName == "span" ? "text" : "paragraph";
        string styleName = styledNode.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return TextStyle.Empty;
        }

        string? fontWeight = document.StyleEngine.GetStyleProperty(styleName, "font-weight", OdfNamespaces.Fo, family);
        string? fontStyle = document.StyleEngine.GetStyleProperty(styleName, "font-style", OdfNamespaces.Fo, family);
        string? fontSize = document.StyleEngine.GetStyleProperty(styleName, "font-size", OdfNamespaces.Fo, family);
        string? fontFamily = document.StyleEngine.GetStyleProperty(styleName, "font-family", OdfNamespaces.Fo, family) ??
            document.StyleEngine.GetStyleProperty(styleName, "font-family", OdfNamespaces.Svg, family) ??
            document.StyleEngine.GetStyleProperty(styleName, "font-name", OdfNamespaces.Style, family);
        string? color = document.StyleEngine.GetStyleProperty(styleName, "color", OdfNamespaces.Fo, family);
        string? underline = document.StyleEngine.GetStyleProperty(styleName, "text-underline-style", OdfNamespaces.Style, family);
        string? strikethrough = document.StyleEngine.GetStyleProperty(styleName, "text-line-through-style", OdfNamespaces.Style, family);
        string? textPosition = document.StyleEngine.GetStyleProperty(styleName, "text-position", OdfNamespaces.Style, family);
        return new TextStyle(
            string.Equals(fontWeight, "bold", StringComparison.OrdinalIgnoreCase),
            string.Equals(fontStyle, "italic", StringComparison.OrdinalIgnoreCase),
            ToHundredthsOfPoint(fontSize),
            NormalizeFontFamily(fontFamily),
            NormalizeColor(color),
            IsEnabledLineStyle(underline),
            IsEnabledLineStyle(strikethrough),
            NormalizeTextPosition(textPosition));
    }

    /// <summary>
    /// 取得 OdfNode 內所有段落的文字段落清單。
    /// </summary>
    private static IReadOnlyList<TextRun> GetTextRuns(OdfNode textBox, OdfPresentationDocument document)
    {
        IReadOnlyList<TextParagraph> paragraphs = GetTextParagraphs(textBox, document);
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

    /// <summary>
    /// 從 OdfNode 內解析文字段落清單。
    /// </summary>
    private static IReadOnlyList<TextParagraph> GetTextParagraphs(OdfNode textBox, OdfPresentationDocument document)
    {
        List<OdfNode> paragraphNodes = FindDescendants(textBox, "p", OdfNamespaces.Text);
        if (paragraphNodes.Count == 0)
        {
            return [new TextParagraph([new TextRun(textBox.TextContent, TextStyle.Empty)], null)];
        }

        var paragraphs = new List<TextParagraph>(paragraphNodes.Count);
        foreach (OdfNode paragraphNode in paragraphNodes)
        {
            var runs = new List<TextRun>();
            TextStyle paragraphStyle = GetTextStyle(paragraphNode, document);
            foreach (OdfNode child in paragraphNode.Children)
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
                    runs.Add(new TextRun(child.TextContent, GetTextStyle(child, document)));
                }
            }

            if (runs.Count == 0 && !string.IsNullOrEmpty(paragraphNode.TextContent))
            {
                runs.Add(new TextRun(paragraphNode.TextContent, paragraphStyle));
            }

            paragraphs.Add(new TextParagraph(runs, GetTextAlignment(paragraphNode, document)));
        }

        return paragraphs;
    }

    /// <summary>
    /// 取得段落的水平對齊方式。
    /// </summary>
    private static A.TextAlignmentTypeValues? GetTextAlignment(OdfNode paragraph, OdfPresentationDocument document)
    {
        string styleName = paragraph.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return null;
        }

        string? textAlign = document.StyleEngine.GetStyleProperty(styleName, "text-align", OdfNamespaces.Fo, "paragraph");
        return textAlign switch
        {
            "center" => A.TextAlignmentTypeValues.Center,
            "end" or "right" => A.TextAlignmentTypeValues.Right,
            "justify" => A.TextAlignmentTypeValues.Justified,
            "start" or "left" => A.TextAlignmentTypeValues.Left,
            _ => null,
        };
    }

    /// <summary>
    /// 尋找所有指定名稱與命名空間的子孫節點。
    /// </summary>
    private static List<OdfNode> FindDescendants(OdfNode node, string localName, string namespaceUri)
    {
        var descendants = new List<OdfNode>();
        AddDescendants(node, localName, namespaceUri, descendants);
        return descendants;
    }

    /// <summary>
    /// 遞迴收集子孫節點。
    /// </summary>
    private static void AddDescendants(OdfNode node, string localName, string namespaceUri, List<OdfNode> descendants)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                descendants.Add(child);
            }

            AddDescendants(child, localName, namespaceUri, descendants);
        }
    }

    /// <summary>
    /// 尋找第一個指定名稱與命名空間的子孫節點。
    /// </summary>
    private static OdfNode? FindDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }

            OdfNode? found = FindDescendant(child, localName, namespaceUri);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 收集 ODP 簡報內的所有色彩與字型以建立佈景主題調色盤（Theme Palette）。
    /// </summary>
    private static ThemePalette CollectThemePalette(OdfPresentationDocument document)
    {
        var colors = new List<string>();
        var latinFonts = new List<string>();
        var eastAsianFonts = new List<string>();
        var complexScriptFonts = new List<string>();

        CollectThemeColors(document.ContentDom, colors);
        CollectThemeColors(document.StylesDom, colors);
        CollectThemeFonts(document.ContentDom, latinFonts, eastAsianFonts, complexScriptFonts);
        CollectThemeFonts(document.StylesDom, latinFonts, eastAsianFonts, complexScriptFonts);
        return ThemePalette.From(colors, latinFonts, eastAsianFonts, complexScriptFonts);
    }

    /// <summary>
    /// 從指定 DOM 節點收集色彩設定。
    /// </summary>
    private static void CollectThemeColors(OdfNode root, List<string> colors)
    {
        foreach (OdfNode node in Enumerate(root))
        {
            AddThemeColor(colors, node.GetAttribute("fill-color", OdfNamespaces.Draw));
            AddThemeColor(colors, node.GetAttribute("stroke-color", OdfNamespaces.Svg));
            AddThemeColor(colors, node.GetAttribute("color", OdfNamespaces.Fo));
            AddThemeColor(colors, node.GetAttribute("background-color", OdfNamespaces.Fo));
            AddThemeBorderColor(colors, node.GetAttribute("border", OdfNamespaces.Fo));
            AddThemeBorderColor(colors, node.GetAttribute("border-top", OdfNamespaces.Fo));
            AddThemeBorderColor(colors, node.GetAttribute("border-right", OdfNamespaces.Fo));
            AddThemeBorderColor(colors, node.GetAttribute("border-bottom", OdfNamespaces.Fo));
            AddThemeBorderColor(colors, node.GetAttribute("border-left", OdfNamespaces.Fo));
        }
    }

    /// <summary>
    /// 將單一色彩色碼標準化後加入佈景主題色彩集合。
    /// </summary>
    private static void AddThemeColor(List<string> colors, string? value)
    {
        string? color = NormalizeColor(value);
        if (color is null ||
            string.Equals(color, "000000", StringComparison.Ordinal) ||
            string.Equals(color, "FFFFFF", StringComparison.Ordinal) ||
            colors.Contains(color, StringComparer.Ordinal))
        {
            return;
        }

        colors.Add(color);
    }

    /// <summary>
    /// 從指定 DOM 節點收集字型家族設定。
    /// </summary>
    private static void CollectThemeFonts(
        OdfNode root,
        List<string> latinFonts,
        List<string> eastAsianFonts,
        List<string> complexScriptFonts)
    {
        foreach (OdfNode node in Enumerate(root))
        {
            AddThemeFont(latinFonts, node.GetAttribute("font-family", OdfNamespaces.Fo));
            AddThemeFont(latinFonts, node.GetAttribute("font-family", OdfNamespaces.Svg));
            AddThemeFont(latinFonts, node.GetAttribute("font-name", OdfNamespaces.Style));
            AddThemeFont(eastAsianFonts, node.GetAttribute("font-family-asian", OdfNamespaces.Style));
            AddThemeFont(eastAsianFonts, node.GetAttribute("font-name-asian", OdfNamespaces.Style));
            AddThemeFont(complexScriptFonts, node.GetAttribute("font-family-complex", OdfNamespaces.Style));
            AddThemeFont(complexScriptFonts, node.GetAttribute("font-name-complex", OdfNamespaces.Style));
        }
    }

    /// <summary>
    /// 將字型家族名稱加入特定字型集合。
    /// </summary>
    private static void AddThemeFont(List<string> fonts, string? value)
    {
        string? font = NormalizeFontFamily(value);
        if (font is null || fonts.Contains(font, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        fonts.Add(font);
    }

    /// <summary>
    /// 解析邊框設定字串，將包含的色彩加入佈景主題色彩集合。
    /// </summary>
    private static void AddThemeBorderColor(List<string> colors, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ReadOnlySpan<char> text = value.AsSpan();
        for (int i = 0; i <= text.Length - 7; i++)
        {
            if (text[i] != '#' || !IsHexColor(text.Slice(i + 1, 6)))
            {
                continue;
            }

            AddThemeColor(colors, text.Slice(i, 7).ToString());
            return;
        }
    }

    /// <summary>
    /// 檢查特定區段字元是否為十六進位色彩色碼。
    /// </summary>
    private static bool IsHexColor(ReadOnlySpan<char> value)
    {
        foreach (char c in value)
        {
            bool isDigit = c >= '0' && c <= '9';
            bool isUpperHex = c >= 'A' && c <= 'F';
            bool isLowerHex = c >= 'a' && c <= 'f';
            if (!isDigit && !isUpperHex && !isLowerHex)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 標準化字型家族名稱（去除多餘引號）。
    /// </summary>
    private static string? NormalizeFontFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string font = value!.Trim();
        int comma = font.IndexOf(',');
        if (comma >= 0)
        {
            font = font.Substring(0, comma).Trim();
        }

        if (font.Length >= 2 &&
            ((font[0] == '"' && font[font.Length - 1] == '"') ||
            (font[0] == '\'' && font[font.Length - 1] == '\'')))
        {
            font = font.Substring(1, font.Length - 2).Trim();
        }

        return font.Length == 0 ? null : font;
    }

    /// <summary>
    /// 建立包含圖形樹的預設 CommonSlideData。
    /// </summary>
    private static P.CommonSlideData CreateCommonSlideData(string? name = null)
    {
        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));
        var cSld = new P.CommonSlideData(shapeTree);
        if (name != null)
        {
            cSld.Name = name;
        }
        return cSld;
    }

    /// <summary>
    /// 建立包含背景樣式參考的幻燈片版面配置與母片（Slide Master 與 Layout）。
    /// </summary>
    private static SlideLayoutPart CreateLayout(
        PresentationPart presentationPart,
        P.SlideLayoutValues layoutType,
        ThemePalette themePalette)
    {
        SlideMasterPart masterPart = presentationPart.AddNewPart<SlideMasterPart>();
        ThemePart themePart = masterPart.AddNewPart<ThemePart>();
        themePart.Theme = CreateDefaultTheme(themePalette);
        themePart.Theme.Save();

        SlideLayoutPart layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
        layoutPart.AddPart(masterPart);
        P.CommonSlideData layoutCommonSlideData = CreateCommonSlideData();
        layoutCommonSlideData.Background = new P.Background(
            new P.BackgroundStyleReference(new A.SchemeColor { Val = A.SchemeColorValues.Background2 }) { Index = 1U });
        AppendStandardLayoutPlaceholders(layoutCommonSlideData.ShapeTree!, layoutType);

        layoutPart.SlideLayout = new P.SlideLayout(
            layoutCommonSlideData,
            new P.ColorMapOverride(new A.MasterColorMapping()))
        {
            Type = layoutType,
            Preserve = true,
        };
        layoutPart.SlideLayout.Save();

        string layoutRelationshipId = masterPart.GetIdOfPart(layoutPart);
        P.CommonSlideData masterCommonSlideData = CreateCommonSlideData();
        masterCommonSlideData.Background = new P.Background(
            new P.BackgroundStyleReference(new A.SchemeColor { Val = A.SchemeColorValues.Background1 }) { Index = 1U });

        masterPart.SlideMaster = new P.SlideMaster(
            masterCommonSlideData,
            new P.ColorMap
            {
                Background1 = A.ColorSchemeIndexValues.Light1,
                Text1 = A.ColorSchemeIndexValues.Dark1,
                Background2 = A.ColorSchemeIndexValues.Light2,
                Text2 = A.ColorSchemeIndexValues.Dark2,
                Accent1 = A.ColorSchemeIndexValues.Accent1,
                Accent2 = A.ColorSchemeIndexValues.Accent2,
                Accent3 = A.ColorSchemeIndexValues.Accent3,
                Accent4 = A.ColorSchemeIndexValues.Accent4,
                Accent5 = A.ColorSchemeIndexValues.Accent5,
                Accent6 = A.ColorSchemeIndexValues.Accent6,
                Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink,
            },
            new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = layoutRelationshipId }));
        masterPart.SlideMaster.Save();

        string masterRelationshipId = presentationPart.GetIdOfPart(masterPart);
        P.Presentation presentation = presentationPart.Presentation!;
        presentation.SlideMasterIdList!.Append(new P.SlideMasterId { Id = 2147483648U, RelationshipId = masterRelationshipId });
        return layoutPart;
    }

    /// <summary>
    /// 依標準版面配置在 SlideLayoutPart 內建立對應的預留位置圖形。
    /// </summary>
    private static void AppendStandardLayoutPlaceholders(P.ShapeTree shapeTree, P.SlideLayoutValues layoutType)
    {
        uint placeholderId = 2U;
        if (layoutType == P.SlideLayoutValues.TitleOnly)
        {
            shapeTree.Append(CreateLayoutPlaceholderShape(
                ref placeholderId,
                P.PlaceholderValues.Title,
                "Title Placeholder",
                685800L,
                457200L,
                7772400L,
                914400L));

            return;
        }

        if (layoutType == P.SlideLayoutValues.Title)
        {
            shapeTree.Append(CreateLayoutPlaceholderShape(
                ref placeholderId,
                P.PlaceholderValues.Title,
                "Title Placeholder",
                685800L,
                457200L,
                7772400L,
                914400L));
            shapeTree.Append(CreateLayoutPlaceholderShape(
                ref placeholderId,
                P.PlaceholderValues.SubTitle,
                "Subtitle Placeholder",
                1371600L,
                1828800L,
                6400800L,
                1371600L));

            return;
        }

        if (layoutType == P.SlideLayoutValues.Text)
        {
            shapeTree.Append(CreateLayoutPlaceholderShape(
                ref placeholderId,
                P.PlaceholderValues.Title,
                "Title Placeholder",
                685800L,
                457200L,
                7772400L,
                914400L));
            shapeTree.Append(CreateLayoutPlaceholderShape(
                ref placeholderId,
                P.PlaceholderValues.Body,
                "Body Placeholder",
                914400L,
                1600200L,
                7315200L,
                4572000L));
        }
    }

    /// <summary>
    /// 建立 SlideLayoutPart 使用的預留位置圖形。
    /// </summary>
    private static P.Shape CreateLayoutPlaceholderShape(
        ref uint placeholderId,
        P.PlaceholderValues placeholderType,
        string name,
        long x,
        long y,
        long width,
        long height)
    {
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = placeholderId++, Name = name },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                CreateApplicationNonVisualDrawingProperties(placeholderType)),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
            CreateDefaultShapeStyle(),
            new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()));
    }

    /// <summary>
    /// 建立包含更完整矩陣（Style Matrix）格式設定的預設佈景主題。
    /// </summary>
    private static A.Theme CreateDefaultTheme(ThemePalette palette)
    {
        return new A.Theme(
            new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = palette.Dark1 }),
                    new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = palette.Light1 }),
                    new A.Dark2Color(new A.RgbColorModelHex { Val = palette.Dark2 }),
                    new A.Light2Color(new A.RgbColorModelHex { Val = palette.Light2 }),
                    new A.Accent1Color(new A.RgbColorModelHex { Val = palette.Accent1 }),
                    new A.Accent2Color(new A.RgbColorModelHex { Val = palette.Accent2 }),
                    new A.Accent3Color(new A.RgbColorModelHex { Val = palette.Accent3 }),
                    new A.Accent4Color(new A.RgbColorModelHex { Val = palette.Accent4 }),
                    new A.Accent5Color(new A.RgbColorModelHex { Val = palette.Accent5 }),
                    new A.Accent6Color(new A.RgbColorModelHex { Val = palette.Accent6 }),
                    new A.Hyperlink(new A.RgbColorModelHex { Val = palette.Hyperlink }),
                    new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = palette.FollowedHyperlink }))
                { Name = "OdfKit" },
                new A.FontScheme(
                    new A.MajorFont(
                        new A.LatinFont { Typeface = palette.MajorLatinFont },
                        new A.EastAsianFont { Typeface = palette.MajorEastAsianFont },
                        new A.ComplexScriptFont { Typeface = palette.MajorComplexScriptFont }),
                    new A.MinorFont(
                        new A.LatinFont { Typeface = palette.MinorLatinFont },
                        new A.EastAsianFont { Typeface = palette.MinorEastAsianFont },
                        new A.ComplexScriptFont { Typeface = palette.MinorComplexScriptFont }))
                { Name = "OdfKit" },
                new A.FormatScheme(
                    new A.FillStyleList(
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                        CreateGradientFill(),
                        CreateGradientFill()),
                    new A.LineStyleList(
                        CreateThemeOutline(9525),
                        CreateThemeOutline(25400),
                        CreateThemeOutline(38100)),
                    new A.EffectStyleList(
                        CreateThemeEffectStyle(A.SchemeColorValues.Dark2, 38100L, 5400000, 72000),
                        CreateThemeEffectStyle(A.SchemeColorValues.Dark2, 50800L, 5400000, 55000),
                        CreateThemeEffectStyle(A.SchemeColorValues.Accent1, 63500L, 5400000, 45000)),
                    new A.BackgroundFillStyleList(
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                        CreateGradientFill(),
                        CreateGradientFill()))
                { Name = "OdfKit" }))
        { Name = "OdfKit" };
    }

    /// <summary>
    /// 建立佈景主題填滿樣式使用的預設漸層填滿（Gradient Fill）。
    /// </summary>
    private static A.GradientFill CreateGradientFill()
    {
        return new A.GradientFill(
            new A.GradientStopList(
                new A.GradientStop(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Position = 0 },
                new A.GradientStop(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Position = 100000 }),
            new A.LinearGradientFill { Angle = 5400000, Scaled = true });
    }

    /// <summary>
    /// 建立佈景主題線條樣式使用的輪廓線（Outline）。
    /// </summary>
    private static A.Outline CreateThemeOutline(int width)
    {
        return new A.Outline(
            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
            new A.PresetDash { Val = A.PresetLineDashValues.Solid },
            new A.Round())
        {
            Width = width,
            CapType = A.LineCapValues.Flat,
            CompoundLineType = A.CompoundLineValues.Single,
            Alignment = A.PenAlignmentValues.Center,
        };
    }

    /// <summary>
    /// 建立佈景主題效果矩陣使用的陰影效果。
    /// </summary>
    private static A.EffectStyle CreateThemeEffectStyle(
        A.SchemeColorValues color,
        long distance,
        int direction,
        int alpha)
    {
        return new A.EffectStyle(
            new A.EffectList(
                new A.OuterShadow(
                    new A.SchemeColor(new A.Alpha { Val = alpha }) { Val = color })
                {
                    Distance = distance,
                    Direction = direction,
                    Alignment = A.RectangleAlignmentValues.BottomLeft,
                }));
    }

    /// <summary>
    /// 建立空的投影片執行個體。
    /// </summary>
    private static P.Slide CreateEmptySlide()
    {
        return new P.Slide(CreateCommonSlideData());
    }

    /// <summary>
    /// 建立投影片執行個體並依據 ODP 設定渲染其下之圖形與動畫時間軸。
    /// </summary>
    private static P.Slide CreateSlide(OdfNode slideNode, OdfPresentationDocument odpDocument, SlidePart slidePart, ref uint shapeId)
    {
        P.CommonSlideData commonSlideData = CreateCommonSlideData(slideNode.GetAttribute("name", OdfNamespaces.Draw));
        var animationTargets = new Dictionary<string, uint>(StringComparer.Ordinal);
        var animationNodes = new Dictionary<string, OdfNode>(StringComparer.Ordinal);

        string? backgroundColor = GetSlideBackgroundColor(slideNode, odpDocument);
        if (!string.IsNullOrWhiteSpace(backgroundColor))
        {
            commonSlideData.Background = new P.Background(
                new P.BackgroundProperties(
                    new A.SolidFill(new A.RgbColorModelHex { Val = backgroundColor })));
        }

        P.ShapeTree shapeTree = commonSlideData.ShapeTree!;

        foreach (OdfNode child in slideNode.Children)
        {
            WriteDrawingNode(child, shapeTree, odpDocument, slidePart, ref shapeId, animationTargets, animationNodes);
        }

        P.Slide slide = new(commonSlideData);
        P.Transition? transition = CreateTransition(slideNode, odpDocument);
        if (transition is not null)
        {
            slide.Append(transition);
        }

        P.Timing? timing = CreateAnimationTiming(slideNode, odpDocument, animationTargets, animationNodes);
        if (timing is not null)
        {
            slide.Append(timing);
        }

        return slide;
    }

    /// <summary>
    /// 建立包含群組步驟（Parallel/Sequence）與段落動畫支援的動畫時間軸（Timing）。
    /// </summary>
    private static P.Timing? CreateAnimationTiming(
        OdfNode slideNode,
        OdfPresentationDocument odpDocument,
        IReadOnlyDictionary<string, uint> animationTargets,
        IReadOnlyDictionary<string, OdfNode> animationNodes)
    {
        IReadOnlyList<OdfKit.Presentation.OdfAnimationInfo> animations =
            new OdfKit.Presentation.OdfSlide(slideNode, odpDocument)
                .GetAnimations()
                .Where(animation => animationTargets.ContainsKey(animation.TargetElementId))
                .ToArray();
        if (animations.Count == 0)
        {
            return null;
        }

        uint timeNodeId = 1;
        var mainSequenceChildren = new P.ChildTimeNodeList();

        P.ChildTimeNodeList? currentGroupChildren = null;
        P.TimeNodeValues currentGroupNodeType = P.TimeNodeValues.ClickEffect;

        // 真機 PowerPoint 對「點擊觸發」步驟的外層延遲固定為 indefinite（等待下一次點擊），
        // 但對「接續上一個效果」步驟的外層延遲是相對最近一次點擊起算的實際累計毫秒數，且不會
        // 在外層與效果節點之間插入 delay=0 的中間層——這是修正 MainSequence 計數低估問題的關鍵。
        int cumulativeMsSinceClick = 0;
        int pendingGroupDelayMs = 0;
        int pendingGroupDurationMs = 0;

        foreach (OdfKit.Presentation.OdfAnimationInfo animation in animations)
        {
            uint targetShapeId = animationTargets[animation.TargetElementId];
            int pCount = 1;
            if (animationNodes.TryGetValue(animation.TargetElementId, out OdfNode? targetNode))
            {
                pCount = Math.Max(GetParagraphCount(targetNode), 1);
            }

            (int Start, int End)? paragraphRange = GetParagraphRange(animation, pCount);

            // 判斷是否需要建立新的動畫群組。OnClick 或 AfterPrevious 動畫，或是第一個動畫時，開啟新群組。
            bool startNewGroup = currentGroupChildren is null ||
                                 animation.Trigger == OdfKit.Presentation.OdfAnimationTrigger.OnClick ||
                                 animation.Trigger == OdfKit.Presentation.OdfAnimationTrigger.AfterPrevious;

            if (startNewGroup)
            {
                if (currentGroupChildren is not null)
                {
                    bool closingGroupIsAfterPrevious = currentGroupNodeType == P.TimeNodeValues.AfterEffect;
                    string outerDelay = closingGroupIsAfterPrevious
                        ? cumulativeMsSinceClick.ToString(CultureInfo.InvariantCulture)
                        : "indefinite";
                    mainSequenceChildren.Append(WrapAnimationStep(currentGroupChildren, outerDelay, closingGroupIsAfterPrevious, ref timeNodeId));

                    int groupOwnEndMs = pendingGroupDelayMs + pendingGroupDurationMs;
                    cumulativeMsSinceClick = closingGroupIsAfterPrevious
                        ? cumulativeMsSinceClick + groupOwnEndMs
                        : groupOwnEndMs;
                }

                currentGroupChildren = new P.ChildTimeNodeList();
                currentGroupNodeType = ReadAnimationNodeType(animation.Trigger);
                pendingGroupDelayMs = ParseMillisecondsOrZero(FormatAnimationDelay(animation));
                pendingGroupDurationMs = int.Parse(FormatAnimationDuration(animation), CultureInfo.InvariantCulture);
            }

            // 逐段落動畫展開，或者單一圖形動畫
            if (paragraphRange.HasValue)
            {
                currentGroupChildren!.Append(CreateAnimationEffectNode(
                    animation,
                    targetShapeId,
                    ReadAnimationNodeType(animation.Trigger),
                    ref timeNodeId,
                    paragraphRange.Value.Start,
                    paragraphRange.Value.End));
            }
            else if (pCount > 1)
            {
                for (int i = 0; i < pCount; i++)
                {
                    currentGroupChildren!.Append(CreateAnimationEffectNode(
                        animation,
                        targetShapeId,
                        ReadAnimationNodeType(animation.Trigger),
                        ref timeNodeId,
                        i,
                        i));
                }
            }
            else
            {
                currentGroupChildren!.Append(CreateAnimationEffectNode(
                    animation,
                    targetShapeId,
                    ReadAnimationNodeType(animation.Trigger),
                    ref timeNodeId));
            }
        }

        if (currentGroupChildren is not null)
        {
            bool lastGroupIsAfterPrevious = currentGroupNodeType == P.TimeNodeValues.AfterEffect;
            string lastOuterDelay = lastGroupIsAfterPrevious
                ? cumulativeMsSinceClick.ToString(CultureInfo.InvariantCulture)
                : "indefinite";
            mainSequenceChildren.Append(WrapAnimationStep(currentGroupChildren, lastOuterDelay, lastGroupIsAfterPrevious, ref timeNodeId));
        }

        var mainSequence = new P.SequenceTimeNode(
            new P.CommonTimeNode
            {
                Id = timeNodeId++,
                Duration = IndefiniteDuration,
                NodeType = P.TimeNodeValues.MainSequence,
                ChildTimeNodeList = mainSequenceChildren,
            },
            new P.PreviousConditionList(
                new P.Condition { Event = P.TriggerEventValues.OnPrevious, Delay = "0", TargetElement = new P.TargetElement(new P.SlideTarget()) }),
            new P.NextConditionList(
                new P.Condition { Event = P.TriggerEventValues.OnNext, Delay = "0", TargetElement = new P.TargetElement(new P.SlideTarget()) }))
        {
            Concurrent = true,
            PreviousAction = P.PreviousActionValues.SkipTimed,
            NextAction = P.NextActionValues.Seek,
        };

        var rootChildren = new P.ChildTimeNodeList(mainSequence);
        var root = new P.ParallelTimeNode(
            new P.CommonTimeNode
            {
                Id = timeNodeId++,
                Duration = IndefiniteDuration,
                Restart = P.TimeNodeRestartValues.Always,
                NodeType = P.TimeNodeValues.TmingRoot,
                ChildTimeNodeList = rootChildren,
            });

        return new P.Timing(
            new P.TimeNodeList(root),
            CreateAnimationBuildList(animations, animationTargets, animationNodes));
    }

    /// <summary>
    /// 將一組動畫效果節點，依 PowerPoint 實際輸出的巢狀結構包裝為主序列下的一個步驟。
    /// </summary>
    /// <remarks>
    /// 已用真機 PowerPoint COM 親自建立兩步驟動畫（OnClick 進場接 AfterPrevious 退場）取得權威結構比對確認：
    /// 點擊觸發（ClickEffect）步驟為外層 delay="indefinite"、中層 delay="0" 的三層巢狀 par；
    /// 接續上一步驟（AfterEffect）步驟僅有外層（delay 為相對最近一次點擊的實際累計毫秒數）直接包住
    /// 效果節點的兩層巢狀 par，並不存在中層。先前固定套用三層 indefinite 結構是 PowerPoint
    /// MainSequence.Count 低估同一序列第二步驟的根因。
    /// </remarks>
    /// <param name="groupChildren">The value to use. / 此步驟的效果節點清單</param>
    /// <param name="outerDelay">The value to use. / 外層 stCondLst 的延遲值（indefinite 或實際毫秒數字串）</param>
    /// <param name="flattenMiddleLayer">The name or identifier. / 是否省略中層（AfterPrevious／WithPrevious 接續步驟應為 <see langword="true"/>）</param>
    /// <param name="timeNodeId">The name or identifier. / 下一個可用的時間節點識別碼</param>
    private static P.ParallelTimeNode WrapAnimationStep(P.ChildTimeNodeList groupChildren, string outerDelay, bool flattenMiddleLayer, ref uint timeNodeId)
    {
        P.ChildTimeNodeList outerChildren;
        if (flattenMiddleLayer)
        {
            outerChildren = groupChildren;
        }
        else
        {
            P.CommonTimeNode middleTimeNode = new()
            {
                Id = timeNodeId++,
                Fill = P.TimeNodeFillValues.Hold,
                StartConditionList = new P.StartConditionList(new P.Condition { Delay = "0" }),
                ChildTimeNodeList = groupChildren,
            };
            outerChildren = new P.ChildTimeNodeList(new P.ParallelTimeNode(middleTimeNode));
        }

        P.CommonTimeNode outerTimeNode = new()
        {
            Id = timeNodeId++,
            Fill = P.TimeNodeFillValues.Hold,
            StartConditionList = new P.StartConditionList(new P.Condition { Delay = outerDelay }),
            ChildTimeNodeList = outerChildren,
        };
        return new P.ParallelTimeNode(outerTimeNode);
    }

    /// <summary>
    /// 將動畫延遲字串（可能為 <see langword="null"/>）解析為毫秒數，無法解析時視為 0。
    /// </summary>
    private static int ParseMillisecondsOrZero(string? delay) =>
        !string.IsNullOrEmpty(delay) && int.TryParse(delay, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ms) ? ms : 0;

    /// <summary>
    /// 計算指定節點底下的段落數量。
    /// </summary>
    private static int GetParagraphCount(OdfNode node)
    {
        List<OdfNode> paragraphNodes = FindDescendants(node, "p", OdfNamespaces.Text);
        return paragraphNodes.Count;
    }

    /// <summary>
    /// 建立包含段落動畫模式（ByLevel）判斷的動畫建置清單（Build List）。
    /// </summary>
    private static P.BuildList CreateAnimationBuildList(
        IEnumerable<OdfKit.Presentation.OdfAnimationInfo> animations,
        IReadOnlyDictionary<string, uint> animationTargets,
        IReadOnlyDictionary<string, OdfNode> animationNodes)
    {
        var buildList = new P.BuildList();
        var shapeIds = new HashSet<uint>();
        foreach (OdfKit.Presentation.OdfAnimationInfo animation in animations)
        {
            uint targetShapeId = animationTargets[animation.TargetElementId];
            if (!shapeIds.Add(targetShapeId))
            {
                continue;
            }

            bool isByParagraph = GetParagraphRange(animation, int.MaxValue).HasValue;
            if (animationNodes.TryGetValue(animation.TargetElementId, out OdfNode? targetNode))
            {
                if (GetParagraphCount(targetNode) > 1)
                {
                    isByParagraph = true;
                }
            }

            var buildParagraph = new P.BuildParagraph
            {
                ShapeId = targetShapeId.ToString(CultureInfo.InvariantCulture),
                GroupId = 0U,
                BuildLevel = 1,
                Build = isByParagraph ? P.ParagraphBuildValues.Paragraph : P.ParagraphBuildValues.Whole,
            };
            buildList.Append(buildParagraph);
        }

        return buildList;
    }

    private static (int Start, int End)? GetParagraphRange(OdfKit.Presentation.OdfAnimationInfo animation, int paragraphCount)
    {
        if (!animation.ParagraphStartIndex.HasValue)
        {
            return null;
        }

        int start = Math.Max(animation.ParagraphStartIndex.Value, 0);
        int end = Math.Max(animation.ParagraphEndIndex ?? start, start);
        if (paragraphCount != int.MaxValue)
        {
            int maxIndex = Math.Max(paragraphCount - 1, 0);
            start = Math.Min(start, maxIndex);
            end = Math.Min(end, maxIndex);
        }

        return (start, end);
    }

    /// <summary>
    /// 建立動畫效果節點，並支援特定段落 range（逐段落動畫）。
    /// </summary>
    private static P.ParallelTimeNode CreateAnimationEffectNode(
        OdfKit.Presentation.OdfAnimationInfo animation,
        uint targetShapeId,
        P.TimeNodeValues groupNodeType,
        ref uint timeNodeId,
        int? paragraphStartIndex = null,
        int? paragraphEndIndex = null)
    {
        string duration = FormatAnimationDuration(animation);
        string? delay = FormatAnimationDelay(animation);
        string shapeIdText = targetShapeId.ToString(CultureInfo.InvariantCulture);

        P.CommonTimeNode behaviorTimeNode = new()
        {
            Id = timeNodeId++,
            Duration = duration,
        };

        P.ShapeTarget CreateShapeTarget()
        {
            var shapeTarget = new P.ShapeTarget { ShapeId = shapeIdText };
            if (paragraphStartIndex.HasValue)
            {
                var txEl = new OpenXmlUnknownElement("p", "txEl", "http://schemas.openxmlformats.org/presentationml/2006/main");
                var pRg = new OpenXmlUnknownElement("p", "pRg", "http://schemas.openxmlformats.org/presentationml/2006/main");
                pRg.SetAttribute(new OpenXmlAttribute("st", "", paragraphStartIndex.Value.ToString(CultureInfo.InvariantCulture)));
                pRg.SetAttribute(new OpenXmlAttribute("end", "", (paragraphEndIndex ?? paragraphStartIndex.Value).ToString(CultureInfo.InvariantCulture)));
                txEl.Append(pRg);
                shapeTarget.Append(txEl);
            }
            return shapeTarget;
        }

        var animateEffect = new P.AnimateEffect(
            new P.CommonBehavior(
                behaviorTimeNode,
                new P.TargetElement(CreateShapeTarget())))
        {
            Transition = ReadAnimationTransition(animation.Kind),
            Filter = ReadAnimationFilter(animation.Effect),
        };

        var childList = new P.ChildTimeNodeList();
        if (animation.Kind is OdfKit.Presentation.OdfAnimationKind.Entrance or OdfKit.Presentation.OdfAnimationKind.Exit)
        {
            string visibility = animation.Kind == OdfKit.Presentation.OdfAnimationKind.Entrance ? "visible" : "hidden";
            P.CommonTimeNode setTimeNode = new()
            {
                Id = timeNodeId++,
                Duration = "1",
                Fill = P.TimeNodeFillValues.Hold,
                StartConditionList = new P.StartConditionList(new P.Condition { Delay = "0" }),
            };
            var setBehavior = new P.SetBehavior(
                new P.CommonBehavior(
                    setTimeNode,
                    new P.TargetElement(CreateShapeTarget()),
                    new P.AttributeNameList(new P.AttributeName("style.visibility"))),
                new P.ToVariantValue(new P.StringVariantValue { Val = visibility }));
            childList.Append(setBehavior);
        }
        childList.Append(animateEffect);

        P.CommonTimeNode effectTimeNode = new()
        {
            Id = timeNodeId++,
            PresetId = ReadAnimationPresetId(animation),
            PresetClass = ReadAnimationPresetClass(animation.Kind),
            PresetSubtype = 0,
            Fill = P.TimeNodeFillValues.Hold,
            GroupId = 0U,
            NodeType = groupNodeType,
            StartConditionList = new P.StartConditionList(new P.Condition { Delay = delay ?? "0" }),
            ChildTimeNodeList = childList,
        };
        return new P.ParallelTimeNode(effectTimeNode);
    }

    /// <summary>
    /// 設定動畫的開始條件（延遲毫秒數）。
    /// </summary>
    private static void SetAnimationStart(P.CommonTimeNode timeNode, string? delay)
    {
        timeNode.StartConditionList = new P.StartConditionList(
            new P.Condition { Delay = delay ?? "0" });
    }

    /// <summary>
    /// 格式化動畫持續時間字串。
    /// </summary>
    private static string FormatAnimationDuration(OdfKit.Presentation.OdfAnimationInfo animation)
    {
        double seconds = animation.TryGetDurationSeconds(out double parsedSeconds)
            ? Math.Max(parsedSeconds, 0d)
            : 0.5d;
        return Math.Max(0, (int)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 將公分或英吋等寬度單位轉換為 EMUs 單位。
    /// </summary>
    private static int? ToLineWidthEmus(string? value)
    {
        if (!OdfLength.TryParse(value, out OdfLength length))
        {
            return null;
        }

        return Math.Max((int)Math.Round(length.ToPoints() * EmusPerPoint, MidpointRounding.AwayFromZero), 1);
    }

    /// <summary>
    /// 解析 Stroke Dash 樣式。
    /// </summary>
    private static A.PresetLineDashValues? ReadStrokeDash(string? stroke)
    {
        if (string.IsNullOrWhiteSpace(stroke) || string.Equals(stroke, "solid", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (stroke.Contains("dot", StringComparison.OrdinalIgnoreCase))
        {
            return A.PresetLineDashValues.Dot;
        }

        return stroke.Contains("dash", StringComparison.OrdinalIgnoreCase)
            ? A.PresetLineDashValues.Dash
            : null;
    }

    /// <summary>
    /// 格式化動畫延遲時間字串。
    /// </summary>
    private static string? FormatAnimationDelay(OdfKit.Presentation.OdfAnimationInfo animation)
    {
        if (!animation.TryGetDelaySeconds(out double seconds) || seconds <= 0d)
        {
            return null;
        }

        return Math.Max(0, (int)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 讀取動畫進入或退出的轉換模式。
    /// </summary>
    private static P.AnimateEffectTransitionValues ReadAnimationTransition(OdfKit.Presentation.OdfAnimationKind kind)
        => kind switch
        {
            OdfKit.Presentation.OdfAnimationKind.Exit => P.AnimateEffectTransitionValues.Out,
            OdfKit.Presentation.OdfAnimationKind.Emphasis => P.AnimateEffectTransitionValues.None,
            _ => P.AnimateEffectTransitionValues.In,
        };

    /// <summary>
    /// 讀取動畫效果名稱的篩選字串。
    /// </summary>
    private static string ReadAnimationFilter(OdfKit.Presentation.OdfAnimationEffect effect)
        => effect switch
        {
            OdfKit.Presentation.OdfAnimationEffect.Fade => "fade",
            OdfKit.Presentation.OdfAnimationEffect.Zoom => "zoom",
            OdfKit.Presentation.OdfAnimationEffect.FlyIn => "fly",
            _ => "appear",
        };

    /// <summary>
    /// 讀取動畫 Preset 類別。
    /// </summary>
    private static P.TimeNodePresetClassValues ReadAnimationPresetClass(OdfKit.Presentation.OdfAnimationKind kind)
        => kind switch
        {
            OdfKit.Presentation.OdfAnimationKind.Exit => P.TimeNodePresetClassValues.Exit,
            OdfKit.Presentation.OdfAnimationKind.Emphasis => P.TimeNodePresetClassValues.Emphasis,
            _ => P.TimeNodePresetClassValues.Entrance,
        };

    /// <summary>
    /// 讀取動畫 Preset ID。
    /// </summary>
    private static int ReadAnimationPresetId(OdfKit.Presentation.OdfAnimationInfo animation)
        => animation.Effect switch
        {
            OdfKit.Presentation.OdfAnimationEffect.Fade => 9,
            OdfKit.Presentation.OdfAnimationEffect.Zoom => 10,
            OdfKit.Presentation.OdfAnimationEffect.FlyIn => 2,
            _ => 1,
        };

    /// <summary>
    /// 讀取動畫時間軸節點的觸發行為。
    /// </summary>
    private static P.TimeNodeValues ReadAnimationNodeType(OdfKit.Presentation.OdfAnimationTrigger trigger)
        => trigger switch
        {
            OdfKit.Presentation.OdfAnimationTrigger.AfterPrevious => P.TimeNodeValues.AfterEffect,
            OdfKit.Presentation.OdfAnimationTrigger.WithPrevious => P.TimeNodeValues.WithEffect,
            _ => P.TimeNodeValues.ClickEffect,
        };

    /// <summary>
    /// 建立投影片轉場效果（Transition）。
    /// </summary>
    private static P.Transition? CreateTransition(OdfNode slideNode, OdfPresentationDocument document)
    {
        string? smilType = slideNode.GetAttribute("type", SmilNamespace);
        string? styleName = slideNode.GetAttribute("style-name", OdfNamespaces.Draw);
        if (string.IsNullOrEmpty(smilType) && !string.IsNullOrWhiteSpace(styleName))
        {
            smilType = document.StyleEngine.GetStyleProperty(styleName!, "type", SmilNamespace, "drawing-page");
        }

        if (string.IsNullOrWhiteSpace(smilType))
        {
            return null;
        }

        string? durAttr = slideNode.GetAttribute("dur", SmilNamespace);
        if (string.IsNullOrEmpty(durAttr) && !string.IsNullOrWhiteSpace(styleName))
        {
            durAttr = document.StyleEngine.GetStyleProperty(styleName!, "duration", OdfNamespaces.Presentation, "drawing-page");
        }

        string? speedAttr = slideNode.GetAttribute("transition-speed", OdfNamespaces.Presentation);
        if (string.IsNullOrEmpty(speedAttr) && !string.IsNullOrWhiteSpace(styleName))
        {
            speedAttr = document.StyleEngine.GetStyleProperty(styleName!, "transition-speed", OdfNamespaces.Presentation, "drawing-page");
        }

        var transition = new P.Transition
        {
            Duration = FormatTransitionDuration(durAttr),
            Speed = ReadTransitionSpeed(speedAttr),
        };

        transition.Append(smilType switch
        {
            "push" => new P.PushTransition { Direction = P.TransitionSlideDirectionValues.Down },
            "wipe" => new P.WipeTransition { Direction = P.TransitionSlideDirectionValues.Right },
            "zoom" => new P.ZoomTransition { Direction = P.TransitionInOutDirectionValues.In },
            "split" => new P.SplitTransition
            {
                Orientation = P.DirectionValues.Horizontal,
                Direction = P.TransitionInOutDirectionValues.Out,
            },
            _ => new P.FadeTransition { ThroughBlack = false },
        });

        return transition;
    }

    /// <summary>
    /// 讀取簡報轉場的速度。
    /// </summary>
    private static P.TransitionSpeedValues ReadTransitionSpeed(string? value)
        => value switch
        {
            "slow" => P.TransitionSpeedValues.Slow,
            "fast" => P.TransitionSpeedValues.Fast,
            _ => P.TransitionSpeedValues.Medium,
        };

    /// <summary>
    /// 格式化轉場持續時間字串。
    /// </summary>
    private static string? FormatTransitionDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string duration = value!.Trim();

        // 支援 W3C XML Schema Duration 格式，例如 "PT2.50S" 或 "PT1S"
        if (duration.StartsWith("PT", StringComparison.OrdinalIgnoreCase) && duration.EndsWith("S", StringComparison.OrdinalIgnoreCase))
        {
            string numberPart = duration.Substring(2, duration.Length - 3);
            if (double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double secs))
            {
                return Math.Max(0, (int)Math.Round(secs * 1000d, MidpointRounding.AwayFromZero))
                    .ToString(CultureInfo.InvariantCulture);
            }
        }

        if (duration.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(duration.Substring(0, duration.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
        {
            return Math.Max(0, (int)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture);
        }

        return double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSeconds)
            ? Math.Max(0, (int)Math.Round(parsedSeconds * 1000d, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// 取得簡報投影片背景色色碼。
    /// </summary>
    private static string? GetSlideBackgroundColor(OdfNode slideNode, OdfPresentationDocument document)
    {
        string? styleName = slideNode.GetAttribute("style-name", OdfNamespaces.Draw);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return null;
        }

        string? ownFillColor = document.StyleEngine.GetStyleProperty(styleName!, "fill-color", OdfNamespaces.Draw, "drawing-page");
        if (!string.IsNullOrWhiteSpace(ownFillColor))
        {
            return NormalizeColor(ownFillColor);
        }

        string? masterName = document.StyleEngine.GetStyleProperty(styleName!, "master-page-name", OdfNamespaces.Presentation, "drawing-page");
        if (string.IsNullOrWhiteSpace(masterName))
        {
            return null;
        }

        string? masterStyleName = FindMasterPageStyleName(document.StylesDom, masterName!);
        if (string.IsNullOrWhiteSpace(masterStyleName))
        {
            return null;
        }

        string? fillColor = document.StyleEngine.GetStyleProperty(masterStyleName!, "fill-color", OdfNamespaces.Draw, "drawing-page");
        return NormalizeColor(fillColor);
    }

    /// <summary>
    /// 尋找母片頁面的樣式名稱。
    /// </summary>
    private static string? FindMasterPageStyleName(OdfNode root, string masterName)
    {
        foreach (OdfNode node in Enumerate(root))
        {
            if (node.NodeType == OdfNodeType.Element &&
                node.NamespaceUri == OdfNamespaces.Style &&
                node.LocalName == "master-page" &&
                string.Equals(node.GetAttribute("name", OdfNamespaces.Style), masterName, StringComparison.Ordinal))
            {
                return node.GetAttribute("style-name", OdfNamespaces.Style);
            }
        }

        return null;
    }

    /// <summary>
    /// 寫入簡報圖形節點，並登錄其為動畫之目標圖形與節點。
    /// </summary>
    private static void WriteDrawingNode(
        OdfNode node,
        P.ShapeTree shapeTree,
        OdfPresentationDocument odpDocument,
        SlidePart slidePart,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets = null,
        Dictionary<string, OdfNode>? animationNodes = null)
    {
        if (node.NodeType != OdfNodeType.Element || node.NamespaceUri != OdfNamespaces.Draw)
        {
            return;
        }

        OdfNode? table = FindChild(node, "table", OdfNamespaces.Table);
        if (table is not null)
        {
            shapeTree.Append(CreateTableGraphicFrame(node, table, odpDocument, ref shapeId, animationTargets, animationNodes));
            return;
        }

        switch (node.LocalName)
        {
            case "frame":
                WriteFrame(node, shapeTree, odpDocument, slidePart, ref shapeId, animationTargets, animationNodes);
                break;
            case "rect":
            case "custom-shape":
                shapeTree.Append(CreateShape(node, odpDocument, A.ShapeTypeValues.Rectangle, ref shapeId, animationTargets, animationNodes));
                break;
            case "ellipse":
                shapeTree.Append(CreateShape(node, odpDocument, A.ShapeTypeValues.Ellipse, ref shapeId, animationTargets, animationNodes));
                break;
            case "line":
            case "connector":
                shapeTree.Append(CreateLine(node, odpDocument, ref shapeId, animationTargets, animationNodes));
                break;
            case "g":
                foreach (OdfNode child in node.Children)
                {
                    WriteDrawingNode(child, shapeTree, odpDocument, slidePart, ref shapeId, animationTargets, animationNodes);
                }
                break;
        }
    }

    /// <summary>
    /// 寫入 Frame 類別的繪圖元素（表格、文字框、圖片等）。
    /// </summary>
    private static void WriteFrame(
        OdfNode frame,
        P.ShapeTree shapeTree,
        OdfPresentationDocument odpDocument,
        SlidePart slidePart,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes)
    {
        OdfNode? table = FindChild(frame, "table", OdfNamespaces.Table);
        if (table is not null)
        {
            shapeTree.Append(CreateTableGraphicFrame(frame, table, odpDocument, ref shapeId, animationTargets, animationNodes));
            return;
        }

        OdfNode? textBox = FindChild(frame, "text-box", OdfNamespaces.Draw);
        if (textBox is not null)
        {
            shapeTree.Append(CreateTextBox(frame, odpDocument, GetTextParagraphs(textBox, odpDocument), ref shapeId, animationTargets, animationNodes));
            return;
        }

        OdfNode? image = FindChild(frame, "image", OdfNamespaces.Draw);
        if (image is not null)
        {
            shapeTree.Append(CreateImage(frame, image, odpDocument, slidePart, ref shapeId, animationTargets, animationNodes));
            return;
        }

        OdfNode? objectNode = FindChild(frame, "object", OdfNamespaces.Draw);
        if (objectNode is not null)
        {
            shapeTree.Append(CreateObjectPlaceholder(frame, ref shapeId, animationTargets, animationNodes));
        }
    }

    /// <summary>
    /// 建立包含佈景主題矩陣參考的預設圖形樣式（Shape Style）。
    /// </summary>
    private static P.ShapeStyle CreateDefaultShapeStyle()
    {
        return new P.ShapeStyle(
            new A.LineReference(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 }) { Index = 1U },
            new A.FillReference(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 }) { Index = 1U },
            new A.EffectReference(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 }) { Index = 0U },
            new A.FontReference(new A.SchemeColor { Val = A.SchemeColorValues.Dark1 }) { Index = A.FontCollectionIndexValues.Minor });
    }

    /// <summary>
    /// 建立一般圖形（如矩形、橢圓）。
    /// </summary>
    private static P.Shape CreateShape(
        OdfNode node,
        OdfPresentationDocument odpDocument,
        A.ShapeTypeValues shapeType,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes)
    {
        return new P.Shape(
            CreateNonVisualShapeProperties(node, ref shapeId, node.LocalName, animationTargets, animationNodes, ReadPlaceholderType(node)),
            CreateShapeProperties(node, odpDocument, shapeType),
            CreateDefaultShapeStyle(),
            CreateTextBody(GetTextParagraphs(node, odpDocument)));
    }

    /// <summary>
    /// 建立線條圖形。
    /// </summary>
    private static P.Shape CreateLine(
        OdfNode node,
        OdfPresentationDocument odpDocument,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes)
    {
        long x1 = ToEmus(node.GetAttribute("x1", OdfNamespaces.Svg));
        long y1 = ToEmus(node.GetAttribute("y1", OdfNamespaces.Svg));
        long x2 = ToEmus(node.GetAttribute("x2", OdfNamespaces.Svg));
        long y2 = ToEmus(node.GetAttribute("y2", OdfNamespaces.Svg));
        long x = Math.Min(x1, x2);
        long y = Math.Min(y1, y2);
        long cx = Math.Abs(x2 - x1);
        long cy = Math.Abs(y2 - y1);

        var properties = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = x, Y = y },
                new A.Extents { Cx = Math.Max(cx, 1L), Cy = Math.Max(cy, 1L) })
            {
                HorizontalFlip = x2 < x1,
                VerticalFlip = y2 < y1,
            },
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Line });
        AppendShapeStyle(properties, node, odpDocument);

        return new P.Shape(
            CreateNonVisualShapeProperties(node, ref shapeId, "line", animationTargets, animationNodes),
            properties,
            CreateDefaultShapeStyle(),
            new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()));
    }

    /// <summary>
    /// 建立文字框圖形。
    /// </summary>
    private static P.Shape CreateTextBox(
        OdfNode frame,
        OdfPresentationDocument odpDocument,
        IReadOnlyList<TextParagraph> paragraphs,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes)
    {
        return new P.Shape(
            CreateNonVisualShapeProperties(frame, ref shapeId, "Text Box", animationTargets, animationNodes),
            CreateShapeProperties(frame, odpDocument, A.ShapeTypeValues.Rectangle),
            CreateDefaultShapeStyle(),
            CreateTextBody(paragraphs));
    }

    /// <summary>
    /// 建立表格之圖形元件。
    /// </summary>
    private static P.GraphicFrame CreateTableGraphicFrame(
        OdfNode frame,
        OdfNode tableNode,
        OdfPresentationDocument odpDocument,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets = null,
        Dictionary<string, OdfNode>? animationNodes = null)
    {
        TableData table = ReadOdfTable(tableNode, odpDocument);
        long width = Math.Max(ToEmus(frame.GetAttribute("width", OdfNamespaces.Svg)), 1L);
        long height = Math.Max(ToEmus(frame.GetAttribute("height", OdfNamespaces.Svg)), 1L);
        long columnWidth = Math.Max(width / Math.Max(table.ColumnCount, 1), 1L);
        long rowHeight = Math.Max(height / Math.Max(table.Rows.Count, 1), 1L);

        A.TableProperties tableProperties = new() { FirstRow = true };
        string? templateName = tableNode.GetAttribute("template-name", OdfNamespaces.Table);
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            tableProperties.Append(new A.TableStyleId { Text = templateName! });
        }

        var drawingTable = new A.Table(
            tableProperties,
            new A.TableGrid(table.Columns.Select(_ => new A.GridColumn { Width = columnWidth })));

        foreach (IReadOnlyList<TableCellData> row in table.Rows)
        {
            var tableRow = new A.TableRow { Height = rowHeight };
            for (int column = 0; column < table.ColumnCount; column++)
            {
                TableCellData cell = column < row.Count ? row[column] : TableCellData.Empty;
                var tableCell = new A.TableCell(
                    new A.TextBody(
                        new A.BodyProperties(),
                        new A.ListStyle(),
                        new A.Paragraph(
                            new A.Run(CreateRunProperties(cell.TextStyle), new A.Text(cell.Text)),
                            new A.EndParagraphRunProperties { Language = "en-US" })),
                    CreateTableCellProperties(cell.Style));
                if (cell.RowSpan > 1)
                {
                    tableCell.RowSpan = cell.RowSpan;
                }

                if (cell.ColumnSpan > 1)
                {
                    tableCell.GridSpan = cell.ColumnSpan;
                }

                if (cell.HorizontalMerge)
                {
                    tableCell.HorizontalMerge = true;
                }

                if (cell.VerticalMerge)
                {
                    tableCell.VerticalMerge = true;
                }

                tableRow.Append(tableCell);
            }

            drawingTable.Append(tableRow);
        }

        uint currentShapeId = shapeId++;
        RegisterAnimationTarget(frame, currentShapeId, animationTargets, animationNodes);
        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = currentShapeId, Name = "Table" },
                new P.NonVisualGraphicFrameDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(
                new A.Offset
                {
                    X = ToEmus(frame.GetAttribute("x", OdfNamespaces.Svg)),
                    Y = ToEmus(frame.GetAttribute("y", OdfNamespaces.Svg)),
                },
                new A.Extents { Cx = width, Cy = height }),
            new A.Graphic(
                new A.GraphicData(drawingTable)
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/table",
                }));
    }

    /// <summary>
    /// 依儲存格樣式（自訂背景與四向邊框）建立 OpenXML 表格儲存格屬性。
    /// </summary>
    private static A.TableCellProperties CreateTableCellProperties(TableCellStyle style)
    {
        var properties = new A.TableCellProperties();
        if (!string.IsNullOrWhiteSpace(style.BackgroundColorHex))
        {
            properties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = style.BackgroundColorHex }));
        }

        if (style.LeftBorder is not null)
        {
            properties.LeftBorderLineProperties = CreateLeftBorder(style.LeftBorder);
        }
        if (style.RightBorder is not null)
        {
            properties.RightBorderLineProperties = CreateRightBorder(style.RightBorder);
        }
        if (style.TopBorder is not null)
        {
            properties.TopBorderLineProperties = CreateTopBorder(style.TopBorder);
        }
        if (style.BottomBorder is not null)
        {
            properties.BottomBorderLineProperties = CreateBottomBorder(style.BottomBorder);
        }

        return properties;
    }

    private static A.LeftBorderLineProperties CreateLeftBorder(TableCellBorder border)
        => ConfigureLine(new A.LeftBorderLineProperties(), border);

    private static A.RightBorderLineProperties CreateRightBorder(TableCellBorder border)
        => ConfigureLine(new A.RightBorderLineProperties(), border);

    private static A.TopBorderLineProperties CreateTopBorder(TableCellBorder border)
        => ConfigureLine(new A.TopBorderLineProperties(), border);

    private static A.BottomBorderLineProperties CreateBottomBorder(TableCellBorder border)
        => ConfigureLine(new A.BottomBorderLineProperties(), border);

    private static TLine ConfigureLine<TLine>(TLine line, TableCellBorder border)
        where TLine : A.LinePropertiesType
    {
        line.Width = Math.Max((int)Math.Round(border.WidthPoints * EmusPerPoint, MidpointRounding.AwayFromZero), 1);
        line.Append(new A.SolidFill(new A.RgbColorModelHex { Val = border.ColorHex }));
        line.Append(new A.PresetDash { Val = border.Dash });
        return line;
    }

    private static TableData ReadOdfTable(OdfNode tableNode, OdfPresentationDocument odpDocument)
    {
        var rows = new List<List<TableCellData>>();
        int columnCount = 0;
        foreach (OdfNode rowNode in tableNode.Children)
        {
            if (rowNode.NodeType != OdfNodeType.Element ||
                rowNode.LocalName != "table-row" ||
                rowNode.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            var cells = new List<TableCellData>();
            foreach (OdfNode cellNode in rowNode.Children)
            {
                if (cellNode.NodeType == OdfNodeType.Element &&
                    cellNode.LocalName == "table-cell" &&
                    cellNode.NamespaceUri == OdfNamespaces.Table)
                {
                    cells.Add(new TableCellData(
                        ReadCellText(cellNode),
                        ReadPositiveInt(cellNode.GetAttribute("number-rows-spanned", OdfNamespaces.Table)),
                        ReadPositiveInt(cellNode.GetAttribute("number-columns-spanned", OdfNamespaces.Table)),
                        horizontalMerge: false,
                        verticalMerge: false,
                        style: GetTableCellStyle(cellNode, odpDocument),
                        textStyle: GetTextStyle(cellNode, odpDocument)));
                }
                else if (cellNode.NodeType == OdfNodeType.Element &&
                    cellNode.LocalName == "covered-table-cell" &&
                    cellNode.NamespaceUri == OdfNamespaces.Table)
                {
                    cells.Add(new TableCellData(
                        string.Empty,
                        rowSpan: 1,
                        columnSpan: 1,
                        horizontalMerge: false,
                        verticalMerge: false,
                        covered: true));
                }
            }

            columnCount = Math.Max(columnCount, cells.Count);
            rows.Add(cells);
        }

        if (rows.Count == 0)
        {
            rows.Add([TableCellData.Empty]);
            columnCount = 1;
        }

        columnCount = Math.Max(columnCount, 1);
        ApplyOdfSpanMergeFlags(rows);
        return new TableData(rows, columnCount);
    }

    private static void ApplyOdfSpanMergeFlags(IReadOnlyList<List<TableCellData>> rows)
    {
        for (int row = 0; row < rows.Count; row++)
        {
            for (int column = 0; column < rows[row].Count; column++)
            {
                TableCellData cell = rows[row][column];
                if (cell.Covered || (cell.RowSpan <= 1 && cell.ColumnSpan <= 1))
                {
                    continue;
                }

                for (int rowOffset = 0; rowOffset < cell.RowSpan && row + rowOffset < rows.Count; rowOffset++)
                {
                    for (int columnOffset = 0; columnOffset < cell.ColumnSpan && column + columnOffset < rows[row + rowOffset].Count; columnOffset++)
                    {
                        if (rowOffset == 0 && columnOffset == 0)
                        {
                            continue;
                        }

                        rows[row + rowOffset][column + columnOffset] = TableCellData.CoveredCell(
                            horizontalMerge: columnOffset > 0,
                            verticalMerge: rowOffset > 0);
                    }
                }
            }
        }
    }

    private static string ReadCellText(OdfNode cellNode)
    {
        string[] paragraphs = cellNode.Children
            .Where(child => child.NodeType == OdfNodeType.Element && child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
            .Select(child => child.TextContent)
            .Where(text => text.Length > 0)
            .ToArray();
        return paragraphs.Length == 0 ? cellNode.TextContent : string.Join(Environment.NewLine, paragraphs);
    }

    private static TableCellStyle GetTableCellStyle(OdfNode cellNode, OdfPresentationDocument odpDocument)
    {
        string? styleName = cellNode.GetAttribute("style-name", OdfNamespaces.Table);
        if (styleName is not { Length: > 0 })
        {
            return TableCellStyle.Empty;
        }

        string backgroundStyleName = styleName.Trim();
        if (backgroundStyleName.Length == 0)
        {
            return TableCellStyle.Empty;
        }

        string? backgroundColor = odpDocument.StyleEngine.GetStyleProperty(backgroundStyleName, "background-color", OdfNamespaces.Fo, "table-cell");
        string? border = odpDocument.StyleEngine.GetStyleProperty(backgroundStyleName, "border", OdfNamespaces.Fo, "table-cell");
        string? borderLeft = odpDocument.StyleEngine.GetStyleProperty(backgroundStyleName, "border-left", OdfNamespaces.Fo, "table-cell") ?? border;
        string? borderRight = odpDocument.StyleEngine.GetStyleProperty(backgroundStyleName, "border-right", OdfNamespaces.Fo, "table-cell") ?? border;
        string? borderTop = odpDocument.StyleEngine.GetStyleProperty(backgroundStyleName, "border-top", OdfNamespaces.Fo, "table-cell") ?? border;
        string? borderBottom = odpDocument.StyleEngine.GetStyleProperty(backgroundStyleName, "border-bottom", OdfNamespaces.Fo, "table-cell") ?? border;

        return new TableCellStyle(
            NormalizeColor(backgroundColor),
            ParseTableCellBorder(borderLeft),
            ParseTableCellBorder(borderRight),
            ParseTableCellBorder(borderTop),
            ParseTableCellBorder(borderBottom));
    }

    private static TableCellBorder? ParseTableCellBorder(string? value)
    {
        if (value is not { Length: > 0 })
        {
            return null;
        }

        string borderValue = value.Trim();
        if (borderValue.Length == 0 ||
            string.Equals(borderValue, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        OdfBorder border = OdfBorder.Parse(borderValue);
        if (border.Style == OdfBorder.BorderStyle.None)
        {
            return null;
        }

        System.Drawing.Color borderColor = border.Color.IsEmpty ? System.Drawing.Color.Black : border.Color;
        string color = $"{borderColor.R:X2}{borderColor.G:X2}{borderColor.B:X2}";
        A.PresetLineDashValues dash = border.Style switch
        {
            OdfBorder.BorderStyle.Dashed => A.PresetLineDashValues.Dash,
            OdfBorder.BorderStyle.Dotted => A.PresetLineDashValues.Dot,
            _ => A.PresetLineDashValues.Solid,
        };
        return new TableCellBorder(Math.Max(border.Width.ToPoints(), 0.25d), color, dash);
    }

    private static int ReadPositiveInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 1
            ? result
            : 1;
    }

    private static P.TextBody CreateTextBody(string text, TextStyle style)
        => CreateTextBody([new TextRun(text ?? string.Empty, style)]);

    private static P.TextBody CreateTextBody(IReadOnlyList<TextRun> runs)
        => CreateTextBody(ToTextParagraphs(runs));

    private static IReadOnlyList<TextParagraph> ToTextParagraphs(IReadOnlyList<TextRun> runs)
    {
        var paragraphs = new List<TextParagraph>();
        var currentRuns = new List<TextRun>();
        foreach (TextRun run in runs)
        {
            if (run.Text == Environment.NewLine)
            {
                paragraphs.Add(new TextParagraph(currentRuns.ToArray(), null));
                currentRuns.Clear();
                continue;
            }

            currentRuns.Add(run);
        }

        if (paragraphs.Count == 0 || currentRuns.Count > 0)
        {
            paragraphs.Add(new TextParagraph(currentRuns.ToArray(), null));
        }

        return paragraphs;
    }

    private static P.TextBody CreateTextBody(IReadOnlyList<TextParagraph> textParagraphs)
    {
        if (textParagraphs.Count == 0)
        {
            textParagraphs = [new TextParagraph([new TextRun(string.Empty, TextStyle.Empty)], null)];
        }

        var textBody = new P.TextBody(
            new A.BodyProperties { Wrap = A.TextWrappingValues.Square },
            new A.ListStyle());
        foreach (TextParagraph textParagraph in textParagraphs)
        {
            var paragraph = new A.Paragraph();
            if (textParagraph.Alignment.HasValue)
            {
                paragraph.Append(new A.ParagraphProperties { Alignment = textParagraph.Alignment.Value });
            }

            if (textParagraph.Runs.Count == 0)
            {
                AppendRun(paragraph, string.Empty, TextStyle.Empty);
            }
            else
            {
                foreach (TextRun run in textParagraph.Runs)
                {
                    AppendRun(paragraph, run.Text, run.Style);
                }
            }

            paragraph.Append(new A.EndParagraphRunProperties { Language = "en-US" });
            textBody.Append(paragraph);
        }

        return textBody;
    }

    private static void AppendRun(A.Paragraph paragraph, string text, TextStyle style)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Append(new A.Break());
            }

            if (lines[i].Length > 0)
            {
                paragraph.Append(new A.Run(CreateRunProperties(style), new A.Text(lines[i])));
            }
        }
    }

    private static A.RunProperties CreateRunProperties(TextStyle style)
    {
        var properties = new A.RunProperties();
        if (style.Bold)
        {
            properties.Bold = true;
        }

        if (style.Italic)
        {
            properties.Italic = true;
        }

        if (style.FontSizeHundredthsOfPoint.HasValue)
        {
            properties.FontSize = style.FontSizeHundredthsOfPoint.Value;
        }

        if (!string.IsNullOrWhiteSpace(style.ColorHex))
        {
            properties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = style.ColorHex }));
        }

        if (style.Underline)
        {
            properties.Underline = A.TextUnderlineValues.Single;
        }

        if (style.Strikethrough)
        {
            properties.Strike = A.TextStrikeValues.SingleStrike;
        }

        if (style.BaselinePositionPercent.HasValue)
        {
            properties.Baseline = style.BaselinePositionPercent.Value * 1000;
        }

        if (!string.IsNullOrWhiteSpace(style.FontFamily))
        {
            properties.Append(new A.LatinFont { Typeface = style.FontFamily });
        }

        return properties;
    }

    private static P.Picture CreateImage(
        OdfNode frame,
        OdfNode image,
        OdfPresentationDocument odpDocument,
        SlidePart slidePart,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes)
    {
        string? href = image.GetAttribute("href", OdfNamespaces.XLink);
        if (href is not null && href.StartsWith("./", StringComparison.Ordinal))
        {
            href = href.Substring(2);
        }

        ImagePart imagePart = slidePart.AddImagePart(ImagePartType.Png);
        using (Stream stream = odpDocument.Package.GetEntryStream(href ?? string.Empty))
        {
            imagePart.FeedData(stream);
        }

        uint currentShapeId = shapeId++;
        RegisterAnimationTarget(frame, currentShapeId, animationTargets, animationNodes);
        string? altText = FindDescendant(frame, "desc", OdfNamespaces.Svg)?.TextContent;
        A.SourceRectangle? sourceRectangle = CreateCropSourceRectangle(frame, image);

        var blipFill = new P.BlipFill();
        blipFill.Append(new A.Blip { Embed = slidePart.GetIdOfPart(imagePart), CompressionState = A.BlipCompressionValues.Print });
        if (sourceRectangle is not null)
        {
            blipFill.Append(sourceRectangle);
        }

        blipFill.Append(new A.Stretch(new A.FillRectangle()));

        // PresentationML 圖片屬於 spTree 內容模型的直接選項（<p:pic>），
        // 不可包在 <p:graphicFrame><a:graphic><a:graphicData> 內（該容器用於圖表／OLE 物件／表格）。
        return new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = currentShapeId, Name = "Picture", Description = altText },
                new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            blipFill,
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset
                    {
                        X = ToEmus(frame.GetAttribute("x", OdfNamespaces.Svg)),
                        Y = ToEmus(frame.GetAttribute("y", OdfNamespaces.Svg)),
                    },
                    new A.Extents
                    {
                        Cx = Math.Max(ToEmus(frame.GetAttribute("width", OdfNamespaces.Svg)), 1L),
                        Cy = Math.Max(ToEmus(frame.GetAttribute("height", OdfNamespaces.Svg)), 1L),
                    }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
    }

    /// <summary>
    /// 依據 <c>fo:clip</c>（CSS <c>rect(top, right, bottom, left)</c> 語法）計算 PPTX 圖片來源裁切矩形。
    /// </summary>
    private static A.SourceRectangle? CreateCropSourceRectangle(OdfNode frame, OdfNode image)
    {
        string? clip = image.GetAttribute("clip", OdfNamespaces.Fo) ?? frame.GetAttribute("clip", OdfNamespaces.Fo);
        if (string.IsNullOrWhiteSpace(clip))
        {
            return null;
        }

        string trimmed = clip!.Trim();
        if (!trimmed.StartsWith("rect(", StringComparison.OrdinalIgnoreCase) || !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return null;
        }

        string[] parts = trimmed.Substring(5, trimmed.Length - 6).Split(',');
        if (parts.Length != 4 ||
            !OdfLength.TryParse(parts[0].Trim(), out OdfLength top) ||
            !OdfLength.TryParse(parts[1].Trim(), out OdfLength right) ||
            !OdfLength.TryParse(parts[2].Trim(), out OdfLength bottom) ||
            !OdfLength.TryParse(parts[3].Trim(), out OdfLength left))
        {
            return null;
        }

        double widthPoints = OdfLength.TryParse(frame.GetAttribute("width", OdfNamespaces.Svg), out OdfLength width) ? width.ToPoints() : 0d;
        double heightPoints = OdfLength.TryParse(frame.GetAttribute("height", OdfNamespaces.Svg), out OdfLength height) ? height.ToPoints() : 0d;
        if (widthPoints <= 0d || heightPoints <= 0d)
        {
            return null;
        }

        int leftPercent = (int)Math.Round(left.ToPoints() / widthPoints * 100000d, MidpointRounding.AwayFromZero);
        int topPercent = (int)Math.Round(top.ToPoints() / heightPoints * 100000d, MidpointRounding.AwayFromZero);
        int rightPercent = (int)Math.Round((widthPoints - right.ToPoints()) / widthPoints * 100000d, MidpointRounding.AwayFromZero);
        int bottomPercent = (int)Math.Round((heightPoints - bottom.ToPoints()) / heightPoints * 100000d, MidpointRounding.AwayFromZero);

        return new A.SourceRectangle
        {
            Left = leftPercent,
            Top = topPercent,
            Right = rightPercent,
            Bottom = bottomPercent,
        };
    }

    private static P.Shape CreateObjectPlaceholder(
        OdfNode frame,
        ref uint shapeId,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes)
    {
        return new P.Shape(
            CreateNonVisualShapeProperties(frame, ref shapeId, "Object Placeholder", animationTargets, animationNodes, P.PlaceholderValues.Object),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset
                    {
                        X = ToEmus(frame.GetAttribute("x", OdfNamespaces.Svg)),
                        Y = ToEmus(frame.GetAttribute("y", OdfNamespaces.Svg)),
                    },
                    new A.Extents
                    {
                        Cx = Math.Max(ToEmus(frame.GetAttribute("width", OdfNamespaces.Svg)), 1L),
                        Cy = Math.Max(ToEmus(frame.GetAttribute("height", OdfNamespaces.Svg)), 1L),
                    }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
            new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()));
    }

    private static P.PlaceholderValues? ReadPlaceholderType(OdfNode node)
    {
        if (!string.Equals(node.GetAttribute("placeholder", OdfNamespaces.Presentation), "true", StringComparison.Ordinal))
        {
            return null;
        }

        return node.GetAttribute("class", OdfNamespaces.Presentation) switch
        {
            "title" => P.PlaceholderValues.Title,
            "subtitle" => P.PlaceholderValues.SubTitle,
            "outline" or "text" => P.PlaceholderValues.Body,
            "graphic" or "object" => P.PlaceholderValues.Object,
            "chart" => P.PlaceholderValues.Chart,
            "table" => P.PlaceholderValues.Table,
            "page-number" => P.PlaceholderValues.SlideNumber,
            "header" => P.PlaceholderValues.Header,
            "footer" => P.PlaceholderValues.Footer,
            "date-time" => P.PlaceholderValues.DateAndTime,
            "notes" => P.PlaceholderValues.Body,
            _ => P.PlaceholderValues.Body,
        };
    }

    private static void RegisterAnimationTarget(
        OdfNode node,
        uint shapeId,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes = null)
    {
        if (animationTargets is null)
        {
            return;
        }

        string? id = node.GetAttribute("id", OdfNamespaces.Draw);
        if (!string.IsNullOrWhiteSpace(id))
        {
            animationTargets[id!] = shapeId;
            if (animationNodes is not null)
            {
                animationNodes[id!] = node;
            }
        }
    }

    private static P.NonVisualShapeProperties CreateNonVisualShapeProperties(
        OdfNode node,
        ref uint shapeId,
        string name,
        Dictionary<string, uint>? animationTargets,
        Dictionary<string, OdfNode>? animationNodes = null,
        P.PlaceholderValues? placeholderType = null)
    {
        uint currentShapeId = shapeId++;
        RegisterAnimationTarget(node, currentShapeId, animationTargets, animationNodes);
        return new P.NonVisualShapeProperties(
            new P.NonVisualDrawingProperties { Id = currentShapeId, Name = name },
            new P.NonVisualShapeDrawingProperties(),
            CreateApplicationNonVisualDrawingProperties(placeholderType));
    }

    private static P.ApplicationNonVisualDrawingProperties CreateApplicationNonVisualDrawingProperties(P.PlaceholderValues? placeholderType)
    {
        var properties = new P.ApplicationNonVisualDrawingProperties();
        if (placeholderType.HasValue)
        {
            properties.Append(new P.PlaceholderShape { Type = placeholderType.Value });
        }

        return properties;
    }

    private static P.ShapeProperties CreateShapeProperties(OdfNode node, OdfPresentationDocument document, A.ShapeTypeValues shapeType)
    {
        var properties = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset
                {
                    X = ToEmus(node.GetAttribute("x", OdfNamespaces.Svg)),
                    Y = ToEmus(node.GetAttribute("y", OdfNamespaces.Svg)),
                },
                new A.Extents
                {
                    Cx = Math.Max(ToEmus(node.GetAttribute("width", OdfNamespaces.Svg)), 1L),
                    Cy = Math.Max(ToEmus(node.GetAttribute("height", OdfNamespaces.Svg)), 1L),
                }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = shapeType });
        AppendShapeStyle(properties, node, document);
        return properties;
    }

    private static void AppendShapeStyle(P.ShapeProperties properties, OdfNode node, OdfPresentationDocument document)
    {
        string styleName = node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return;
        }

        string? fill = document.StyleEngine.GetStyleProperty(styleName, "fill", OdfNamespaces.Draw, "graphic");
        string? fillColor = NormalizeColor(document.StyleEngine.GetStyleProperty(styleName, "fill-color", OdfNamespaces.Draw, "graphic"));
        if ((string.IsNullOrWhiteSpace(fill) || string.Equals(fill, "solid", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(fillColor))
        {
            properties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = fillColor }));
        }

        string? stroke = document.StyleEngine.GetStyleProperty(styleName, "stroke", OdfNamespaces.Draw, "graphic");
        string? strokeColor = NormalizeColor(document.StyleEngine.GetStyleProperty(styleName, "stroke-color", OdfNamespaces.Svg, "graphic"));
        string? strokeWidth = document.StyleEngine.GetStyleProperty(styleName, "stroke-width", OdfNamespaces.Svg, "graphic");
        A.PresetLineDashValues? strokeDash = ReadStrokeDash(stroke);
        if (!string.Equals(stroke, "none", StringComparison.OrdinalIgnoreCase) &&
            (!string.IsNullOrWhiteSpace(strokeColor) || !string.IsNullOrWhiteSpace(strokeWidth) || strokeDash.HasValue))
        {
            var outline = new A.Outline();
            if (!string.IsNullOrWhiteSpace(strokeColor))
            {
                outline.Append(new A.SolidFill(new A.RgbColorModelHex { Val = strokeColor }));
            }

            int? width = ToLineWidthEmus(strokeWidth);
            if (width.HasValue)
            {
                outline.Width = width.Value;
            }

            if (strokeDash.HasValue)
            {
                outline.Append(new A.PresetDash { Val = strokeDash.Value });
            }

            properties.Append(outline);
        }

        AppendShapeShadow(properties, styleName, document);
    }

    private static void AppendShapeShadow(P.ShapeProperties properties, string styleName, OdfPresentationDocument document)
    {
        string? shadow = document.StyleEngine.GetStyleProperty(styleName, "shadow", OdfNamespaces.Draw, "graphic");
        if (!string.Equals(shadow, "visible", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? shadowColor = NormalizeColor(document.StyleEngine.GetStyleProperty(styleName, "shadow-color", OdfNamespaces.Draw, "graphic"));
        long offsetX = ToEmus(document.StyleEngine.GetStyleProperty(styleName, "shadow-offset-x", OdfNamespaces.Draw, "graphic"));
        long offsetY = ToEmus(document.StyleEngine.GetStyleProperty(styleName, "shadow-offset-y", OdfNamespaces.Draw, "graphic"));
        long distance = (long)Math.Round(Math.Sqrt(((double)offsetX * offsetX) + ((double)offsetY * offsetY)), MidpointRounding.AwayFromZero);
        int direction = ToDrawingMlDirection(offsetX, offsetY);
        var color = new A.RgbColorModelHex { Val = shadowColor ?? "808080" };
        int? alpha = ToDrawingMlAlpha(document.StyleEngine.GetStyleProperty(styleName, "shadow-opacity", OdfNamespaces.Draw, "graphic"));
        if (alpha.HasValue)
        {
            color.Append(new A.Alpha { Val = alpha.Value });
        }

        properties.Append(new A.EffectList(
            new A.OuterShadow(color)
            {
                Distance = Math.Max(distance, 0L),
                Direction = direction,
                Alignment = A.RectangleAlignmentValues.BottomRight,
            }));
    }

    private static int ToDrawingMlDirection(long offsetX, long offsetY)
    {
        if (offsetX == 0L && offsetY == 0L)
        {
            return 0;
        }

        double degrees = Math.Atan2(offsetY, offsetX) * 180d / Math.PI;
        if (degrees < 0d)
        {
            degrees += 360d;
        }

        return (int)Math.Round(degrees * 60000d, MidpointRounding.AwayFromZero);
    }

    private static int? ToDrawingMlAlpha(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value!.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)
            ? Math.Min(Math.Max((int)Math.Round(percent * 1000d, MidpointRounding.AwayFromZero), 0), 100000)
            : null;
    }

    private static IEnumerable<OdfNode> GetSlides(OdfPresentationDocument document)
    {
        foreach (OdfNode node in Enumerate(document.ContentDom))
        {
            if (node.NodeType == OdfNodeType.Element &&
                node.NamespaceUri == OdfNamespaces.Draw &&
                node.LocalName == "page")
            {
                yield return node;
            }
        }
    }

    private static OdfNode? FindChild(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }

    private static IEnumerable<OdfNode> Enumerate(OdfNode node)
    {
        yield return node;
        foreach (OdfNode child in node.Children)
        {
            foreach (OdfNode descendant in Enumerate(child))
            {
                yield return descendant;
            }
        }
    }

    private static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string clean = value!.Trim();
        if (clean.StartsWith("#", StringComparison.Ordinal))
        {
            clean = clean.Substring(1);
        }

        return clean.Length == 6 ? clean.ToUpperInvariant() : null;
    }

    private static int? ToHundredthsOfPoint(string? value)
    {
        if (!OdfLength.TryParse(value, out OdfLength length))
        {
            return null;
        }

        return (int)Math.Round(length.ToPoints() * 100d, MidpointRounding.AwayFromZero);
    }

    private static int ToSlideSizeEmus(OdfLength length)
    {
        return length.Value <= 0d
            ? DefaultSlideWidth
            : (int)Math.Round(length.ToPoints() * EmusPerPoint, MidpointRounding.AwayFromZero);
    }

    private static long ToEmus(string? value)
    {
        if (!OdfLength.TryParse(value, out OdfLength length))
        {
            return 0L;
        }

        return (long)Math.Round(length.ToPoints() * EmusPerPoint, MidpointRounding.AwayFromZero);
    }

    private static bool IsEnabledLineStyle(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static int? NormalizeTextPosition(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value!.Trim();
        int space = trimmed.IndexOf(' ');
        string head = space >= 0 ? trimmed.Substring(0, space) : trimmed;

        // ODF 允許 "super"／"sub" 關鍵字單獨出現（不帶百分比），預設位移百分比為 33%。
        if (string.Equals(head, "super", StringComparison.OrdinalIgnoreCase))
        {
            return 33;
        }

        if (string.Equals(head, "sub", StringComparison.OrdinalIgnoreCase))
        {
            return -33;
        }

        if (head.EndsWith("%", StringComparison.Ordinal) &&
            double.TryParse(head.Substring(0, head.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
        {
            return (int)Math.Round(percent, MidpointRounding.AwayFromZero);
        }

        return null;
    }

    private sealed class ThemePalette
    {
        private ThemePalette(
            string dark1,
            string light1,
            string dark2,
            string light2,
            string accent1,
            string accent2,
            string accent3,
            string accent4,
            string accent5,
            string accent6,
            string hyperlink,
            string followedHyperlink,
            string majorLatinFont,
            string majorEastAsianFont,
            string majorComplexScriptFont,
            string minorLatinFont,
            string minorEastAsianFont,
            string minorComplexScriptFont)
        {
            Dark1 = dark1;
            Light1 = light1;
            Dark2 = dark2;
            Light2 = light2;
            Accent1 = accent1;
            Accent2 = accent2;
            Accent3 = accent3;
            Accent4 = accent4;
            Accent5 = accent5;
            Accent6 = accent6;
            Hyperlink = hyperlink;
            FollowedHyperlink = followedHyperlink;
            MajorLatinFont = majorLatinFont;
            MajorEastAsianFont = majorEastAsianFont;
            MajorComplexScriptFont = majorComplexScriptFont;
            MinorLatinFont = minorLatinFont;
            MinorEastAsianFont = minorEastAsianFont;
            MinorComplexScriptFont = minorComplexScriptFont;
        }

        public string Dark1 { get; }
        public string Light1 { get; }
        public string Dark2 { get; }
        public string Light2 { get; }
        public string Accent1 { get; }
        public string Accent2 { get; }
        public string Accent3 { get; }
        public string Accent4 { get; }
        public string Accent5 { get; }
        public string Accent6 { get; }
        public string Hyperlink { get; }
        public string FollowedHyperlink { get; }
        public string MajorLatinFont { get; }
        public string MajorEastAsianFont { get; }
        public string MajorComplexScriptFont { get; }
        public string MinorLatinFont { get; }
        public string MinorEastAsianFont { get; }
        public string MinorComplexScriptFont { get; }

        public static ThemePalette From(
            IReadOnlyList<string> colors,
            IReadOnlyList<string> latinFonts,
            IReadOnlyList<string> eastAsianFonts,
            IReadOnlyList<string> complexScriptFonts)
        {
            string dark1 = "000000";
            string light1 = "FFFFFF";
            string dark2 = "595959";
            string light2 = "E7E6E6";
            string accent1 = colors.Count > 0 ? colors[0] : "4472C4";
            string accent2 = colors.Count > 1 ? colors[1] : "ED7D31";
            string accent3 = colors.Count > 2 ? colors[2] : "A5A5A5";
            string accent4 = colors.Count > 3 ? colors[3] : "FFC000";
            string accent5 = colors.Count > 4 ? colors[4] : "5B9BD5";
            string accent6 = colors.Count > 5 ? colors[5] : "70AD47";
            string hyperlink = colors.Count > 6 ? colors[6] : "0563C1";
            string followedHyperlink = colors.Count > 7 ? colors[7] : "954F72";

            string majorLatin = latinFonts.Count > 0 ? latinFonts[0] : "Calibri Light";
            string majorAsian = eastAsianFonts.Count > 0 ? eastAsianFonts[0] : "Microsoft JhengHei";
            string majorComplex = complexScriptFonts.Count > 0 ? complexScriptFonts[0] : "Arial";
            string minorLatin = latinFonts.Count > 1 ? latinFonts[1] : (latinFonts.Count > 0 ? latinFonts[0] : "Calibri");
            string minorAsian = eastAsianFonts.Count > 1 ? eastAsianFonts[1] : (eastAsianFonts.Count > 0 ? eastAsianFonts[0] : "Microsoft JhengHei");
            string minorComplex = complexScriptFonts.Count > 1 ? complexScriptFonts[1] : (complexScriptFonts.Count > 0 ? complexScriptFonts[0] : "Arial");

            return new ThemePalette(
                dark1, light1, dark2, light2,
                accent1, accent2, accent3, accent4, accent5, accent6,
                hyperlink, followedHyperlink,
                majorLatin, majorAsian, majorComplex,
                minorLatin, minorAsian, minorComplex);
        }
    }

    private sealed class TableData
    {
        public TableData(IReadOnlyList<List<TableCellData>> rows, int columnCount)
        {
            Rows = rows;
            ColumnCount = columnCount;
            Columns = Enumerable.Range(0, columnCount).ToList().AsReadOnly();
        }

        public IReadOnlyList<List<TableCellData>> Rows { get; }
        public int ColumnCount { get; }
        public IReadOnlyList<int> Columns { get; }
    }

    private sealed class TableCellData
    {
        public static readonly TableCellData Empty = new(string.Empty, 1, 1, false, false, false, TableCellStyle.Empty, TextStyle.Empty);

        public TableCellData(
            string text,
            int rowSpan,
            int columnSpan,
            bool horizontalMerge,
            bool verticalMerge,
            bool covered = false,
            TableCellStyle? style = null,
            TextStyle? textStyle = null)
        {
            Text = text;
            RowSpan = rowSpan;
            ColumnSpan = columnSpan;
            HorizontalMerge = horizontalMerge;
            VerticalMerge = verticalMerge;
            Covered = covered;
            Style = style ?? TableCellStyle.Empty;
            TextStyle = textStyle ?? TextStyle.Empty;
        }

        public static TableCellData CoveredCell(bool horizontalMerge, bool verticalMerge)
            => new(string.Empty, 1, 1, horizontalMerge, verticalMerge, covered: true, TableCellStyle.Empty, TextStyle.Empty);

        public string Text { get; }
        public int RowSpan { get; }
        public int ColumnSpan { get; }
        public bool HorizontalMerge { get; }
        public bool VerticalMerge { get; }
        public bool Covered { get; }
        public TableCellStyle Style { get; }
        public TextStyle TextStyle { get; }
    }

    private sealed class TableCellStyle
    {
        public static readonly TableCellStyle Empty = new(null, null, null, null, null);

        public TableCellStyle(
            string? backgroundColorHex,
            TableCellBorder? leftBorder,
            TableCellBorder? rightBorder,
            TableCellBorder? topBorder,
            TableCellBorder? bottomBorder)
        {
            BackgroundColorHex = backgroundColorHex;
            LeftBorder = leftBorder;
            RightBorder = rightBorder;
            TopBorder = topBorder;
            BottomBorder = bottomBorder;
        }

        public string? BackgroundColorHex { get; }
        public TableCellBorder? LeftBorder { get; }
        public TableCellBorder? RightBorder { get; }
        public TableCellBorder? TopBorder { get; }
        public TableCellBorder? BottomBorder { get; }
    }

    private sealed class TableCellBorder
    {
        public TableCellBorder(double widthPoints, string colorHex, A.PresetLineDashValues dash)
        {
            WidthPoints = widthPoints;
            ColorHex = colorHex;
            Dash = dash;
        }

        public double WidthPoints { get; }
        public string ColorHex { get; }
        public A.PresetLineDashValues Dash { get; }
    }

    private sealed class TextStyle
    {
        public static readonly TextStyle Empty = new(false, false, null, null, null, false, false, null);

        public TextStyle(
            bool bold,
            bool italic,
            int? fontSizeHundredthsOfPoint,
            string? fontFamily,
            string? colorHex,
            bool underline,
            bool strikethrough,
            int? baselinePositionPercent)
        {
            Bold = bold;
            Italic = italic;
            FontSizeHundredthsOfPoint = fontSizeHundredthsOfPoint;
            FontFamily = fontFamily;
            ColorHex = colorHex;
            Underline = underline;
            Strikethrough = strikethrough;
            BaselinePositionPercent = baselinePositionPercent;
        }

        public bool Bold { get; }

        public bool Italic { get; }

        public int? FontSizeHundredthsOfPoint { get; }

        public string? FontFamily { get; }

        public string? ColorHex { get; }

        public bool Underline { get; }

        public bool Strikethrough { get; }

        public int? BaselinePositionPercent { get; }

        public bool IsEmpty =>
            !Bold &&
            !Italic &&
            FontSizeHundredthsOfPoint is null &&
            string.IsNullOrWhiteSpace(FontFamily) &&
            string.IsNullOrWhiteSpace(ColorHex) &&
            !Underline &&
            !Strikethrough &&
            BaselinePositionPercent is null;
    }

    private sealed class TextParagraph
    {
        public TextParagraph(IReadOnlyList<TextRun> runs, A.TextAlignmentTypeValues? alignment)
        {
            Runs = runs;
            Alignment = alignment;
        }

        public IReadOnlyList<TextRun> Runs { get; }

        public A.TextAlignmentTypeValues? Alignment { get; }
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

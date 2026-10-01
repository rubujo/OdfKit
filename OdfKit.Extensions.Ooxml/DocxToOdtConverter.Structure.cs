using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace OdfKit.Conversion;

/// <summary>
/// DOCX → ODT 轉換的清單、分頁符號與註腳／章節附註。
/// </summary>
public static partial class DocxToOdtConverter
{
    private const int MaxListLevels = 9;

    private sealed class NoteCounters
    {
        internal int Footnotes { get; set; }

        internal int Endnotes { get; set; }
    }

    private static readonly ConditionalWeakTable<TextDocument, NoteCounters> s_noteCounters = new();

    /// <summary>
    /// 一次轉換期間的本文狀態：目前的清單、已使用的清單編號、待套用的分頁符號與自動樣式計數。
    /// </summary>
    private sealed class BodyContext
    {
        internal BodyContext(MainDocumentPart mainPart, TextDocument document)
        {
            MainPart = mainPart;
            OwnerPart = mainPart;
            Document = document;
            Numbering = NumberingIndex.TryCreate(mainPart);
        }

        internal MainDocumentPart MainPart { get; }

        // 超連結與圖片的關係屬於段落所在的部件：本文是主文件部件，頁首頁尾則是各自的部件。
        internal OpenXmlPartContainer OwnerPart { get; set; } = null!;

        internal TextDocument Document { get; }

        internal NumberingIndex? Numbering { get; }

        internal ListState? CurrentList { get; set; }

        internal HashSet<int> UsedNumberingIds { get; } = [];

        internal Dictionary<int, string> ListStyleNames { get; } = [];

        internal bool PendingPageBreak { get; set; }

        internal int PageBreakStyleCount { get; set; }
    }

    private sealed class ListState(int numberingId, OdfNode rootList)
    {
        internal int NumberingId { get; } = numberingId;

        internal List<OdfNode> LevelLists { get; } = [rootList];

        internal Dictionary<OdfNode, OdfNode> LastItems { get; } = [];
    }

    private sealed class NumberingIndex
    {
        private readonly Dictionary<int, WP.NumberingInstance> _instances = [];
        private readonly Dictionary<int, WP.AbstractNum> _abstracts = [];

        private NumberingIndex(WP.Numbering numbering)
        {
            foreach (WP.AbstractNum abstractNum in numbering.Elements<WP.AbstractNum>())
            {
                if (abstractNum.AbstractNumberId?.Value is int id)
                {
                    _abstracts[id] = abstractNum;
                }
            }

            foreach (WP.NumberingInstance instance in numbering.Elements<WP.NumberingInstance>())
            {
                if (instance.NumberID?.Value is int id)
                {
                    _instances[id] = instance;
                }
            }
        }

        internal static NumberingIndex? TryCreate(MainDocumentPart mainPart)
        {
            WP.Numbering? numbering = mainPart.NumberingDefinitionsPart?.Numbering;
            return numbering is null ? null : new NumberingIndex(numbering);
        }

        internal WP.Level? FindLevel(int numberingId, int level, out int? startOverride)
        {
            startOverride = null;
            if (!_instances.TryGetValue(numberingId, out WP.NumberingInstance? instance))
            {
                return null;
            }

            WP.Level? overridden = null;
            foreach (WP.LevelOverride levelOverride in instance.Elements<WP.LevelOverride>())
            {
                if (levelOverride.LevelIndex?.Value == level)
                {
                    startOverride = levelOverride.StartOverrideNumberingValue?.Val?.Value;
                    overridden = levelOverride.Level;
                }
            }

            if (overridden is not null)
            {
                return overridden;
            }

            int? abstractId = instance.AbstractNumId?.Val?.Value;
            if (abstractId is not int resolvedAbstractId || !_abstracts.TryGetValue(resolvedAbstractId, out WP.AbstractNum? abstractNum))
            {
                return null;
            }

            return abstractNum.Elements<WP.Level>().FirstOrDefault(item => item.LevelIndex?.Value == level);
        }
    }

    // ---------- 本文迴圈 ----------

    private static void ConvertBodyParagraph(BodyContext context, WP.Paragraph paragraph)
    {
        List<(WP.Paragraph Segment, bool BreakAfter)> segments = SplitAtPageBreaks(paragraph);
        for (int index = 0; index < segments.Count; index++)
        {
            (WP.Paragraph segment, bool breakAfter) = segments[index];
            if (segments.Count > 1 && !SegmentHasContent(segment))
            {
                // 只用來承載分頁符號的空段落不轉成空白段落，只把分頁符號延後套用到下一個區塊。
                if (breakAfter)
                {
                    context.PendingPageBreak = true;
                }

                continue;
            }

            bool breakBefore = context.PendingPageBreak || HasPageBreakBefore(segment);
            context.PendingPageBreak = false;

            ConvertParagraph(context.OwnerPart, segment, context.Document);
            OdfNode produced = context.Document.BodyTextRoot.LastChild!;

            ParagraphListInfo? list = GetListInfo(context, segment);
            if (list is not null)
            {
                PlaceInList(context, produced, list.Value);
            }
            else
            {
                context.CurrentList = null;
            }

            if (breakBefore)
            {
                ApplyBreakBefore(context, produced, "paragraph");
            }

            if (breakAfter)
            {
                context.PendingPageBreak = true;
            }
        }
    }

    private static void ConvertBodyTable(BodyContext context, WP.Table table)
    {
        context.CurrentList = null;
        bool breakBefore = context.PendingPageBreak;
        context.PendingPageBreak = false;

        OdfNode? previous = context.Document.BodyTextRoot.LastChild;
        ConvertTable(context, table);
        OdfNode? produced = context.Document.BodyTextRoot.LastChild;
        if (breakBefore && produced is not null && !ReferenceEquals(produced, previous))
        {
            ApplyBreakBefore(context, produced, "table");
        }
    }

    // ---------- 分頁符號 ----------

    private static bool IsPageBreak(OpenXmlElement element) =>
        element is WP.Break lineBreak && lineBreak.Type?.Value == WP.BreakValues.Page;

    private static bool RunHasPageBreak(WP.Run run) => run.Elements<WP.Break>().Any(IsPageBreak);

    private static bool HasPageBreakBefore(WP.Paragraph paragraph) =>
        paragraph.ParagraphProperties?.PageBreakBefore is { } pageBreakBefore
        && (pageBreakBefore.Val is null || pageBreakBefore.Val.Value);

    private static bool SegmentHasContent(WP.Paragraph segment) =>
        segment.ChildElements.Any(child => child is not WP.ParagraphProperties
            && child is not WP.BookmarkStart
            && child is not WP.BookmarkEnd
            && !(child is WP.Run run && run.ChildElements.All(item => item is WP.RunProperties)));

    /// <summary>
    /// 在分頁符號處切開段落：每個片段是只含該段內容的複本，並標示片段之後是否有分頁符號。
    /// 沒有分頁符號的段落原樣回傳，不複製。
    /// </summary>
    private static List<(WP.Paragraph Segment, bool BreakAfter)> SplitAtPageBreaks(WP.Paragraph paragraph)
    {
        if (!paragraph.Elements<WP.Run>().Any(RunHasPageBreak))
        {
            return [(paragraph, false)];
        }

        var result = new List<(WP.Paragraph, bool)>();
        WP.Paragraph current = CreateSegment(paragraph);
        foreach (OpenXmlElement child in paragraph.ChildElements)
        {
            if (child is WP.ParagraphProperties)
            {
                continue;
            }

            if (child is WP.Run run && RunHasPageBreak(run))
            {
                WP.Run part = CreateRunShell(run);
                foreach (OpenXmlElement runChild in run.ChildElements)
                {
                    if (runChild is WP.RunProperties)
                    {
                        continue;
                    }

                    if (IsPageBreak(runChild))
                    {
                        if (part.ChildElements.Any(item => item is not WP.RunProperties))
                        {
                            current.Append(part);
                        }

                        result.Add((current, true));
                        current = CreateSegment(paragraph);
                        part = CreateRunShell(run);
                    }
                    else
                    {
                        part.Append(runChild.CloneNode(true));
                    }
                }

                if (part.ChildElements.Any(item => item is not WP.RunProperties))
                {
                    current.Append(part);
                }
            }
            else
            {
                current.Append(child.CloneNode(true));
            }
        }

        result.Add((current, false));
        return result;
    }

    private static WP.Paragraph CreateSegment(WP.Paragraph source)
    {
        var segment = new WP.Paragraph();
        if (source.ParagraphProperties is { } properties)
        {
            segment.Append(properties.CloneNode(true));
        }

        return segment;
    }

    private static WP.Run CreateRunShell(WP.Run source)
    {
        var shell = new WP.Run();
        if (source.RunProperties is { } properties)
        {
            shell.Append(properties.CloneNode(true));
        }

        return shell;
    }

    private static void ApplyBreakBefore(BodyContext context, OdfNode block, string kind)
    {
        OdfNode autoStyles = TextDocumentDomHelper.FindOrCreateChild(
            context.Document.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        string styleName = "DocxPageBreak" + (++context.PageBreakStyleCount).ToString(CultureInfo.InvariantCulture);
        var style = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        style.SetAttribute("name", OdfNamespaces.Style, styleName, "style");

        if (kind == "table")
        {
            style.SetAttribute("family", OdfNamespaces.Style, "table", "style");
            var tableProperties = new OdfNode(OdfNodeType.Element, "table-properties", OdfNamespaces.Style, "style");
            tableProperties.SetAttribute("break-before", OdfNamespaces.Fo, "page", "fo");
            style.AppendChild(tableProperties);
            block.SetAttribute("style-name", OdfNamespaces.Table, styleName, "table");
        }
        else
        {
            style.SetAttribute("family", OdfNamespaces.Style, "paragraph", "style");
            string? existing = block.GetAttribute("style-name", OdfNamespaces.Text);
            if (!string.IsNullOrEmpty(existing))
            {
                style.SetAttribute("parent-style-name", OdfNamespaces.Style, existing!, "style");
            }

            var paragraphProperties = new OdfNode(OdfNodeType.Element, "paragraph-properties", OdfNamespaces.Style, "style");
            paragraphProperties.SetAttribute("break-before", OdfNamespaces.Fo, "page", "fo");
            style.AppendChild(paragraphProperties);
            block.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        }

        autoStyles.AppendChild(style);
    }

    // ---------- 清單 ----------

    private readonly record struct ParagraphListInfo(int NumberingId, int Level);

    private static ParagraphListInfo? GetListInfo(BodyContext context, WP.Paragraph paragraph)
    {
        if (GetHeadingLevel(paragraph) > 0)
        {
            return null;
        }

        WP.NumberingProperties? numbering = paragraph.ParagraphProperties?.NumberingProperties
            ?? FindStyleNumbering(context.MainPart, paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value);
        if (numbering?.NumberingId?.Val?.Value is not int numberingId || numberingId <= 0)
        {
            return null;
        }

        int level = Math.Max(0, Math.Min(numbering.NumberingLevelReference?.Val?.Value ?? 0, MaxListLevels - 1));
        return new ParagraphListInfo(numberingId, level);
    }

    private static WP.NumberingProperties? FindStyleNumbering(MainDocumentPart mainPart, string? styleId)
    {
        WP.Styles? styles = mainPart.StyleDefinitionsPart?.Styles;
        for (int depth = 0; styles is not null && !string.IsNullOrEmpty(styleId) && depth < 8; depth++)
        {
            WP.Style? style = styles.Elements<WP.Style>().FirstOrDefault(item => item.StyleId?.Value == styleId);
            if (style is null)
            {
                return null;
            }

            if (style.StyleParagraphProperties?.NumberingProperties is { } numbering)
            {
                return numbering;
            }

            styleId = style.BasedOn?.Val?.Value;
        }

        return null;
    }

    private static void PlaceInList(BodyContext context, OdfNode paragraphNode, ParagraphListInfo info)
    {
        OdfNode body = context.Document.BodyTextRoot;
        body.RemoveChild(paragraphNode);
        context.CurrentList = PlaceInList(context, body, paragraphNode, info, context.CurrentList);
    }

    /// <summary>
    /// 把已建立的段落節點放進 <paramref name="container"/>（本文、儲存格或頁首頁尾區域）內的清單，
    /// 並傳回更新後的清單狀態；<paramref name="state"/> 為容器內目前的清單。
    /// </summary>
    private static ListState PlaceInList(
        BodyContext context,
        OdfNode container,
        OdfNode paragraphNode,
        ParagraphListInfo info,
        ListState? state)
    {
        if (state is null || state.NumberingId != info.NumberingId)
        {
            var rootList = new OdfNode(OdfNodeType.Element, "list", OdfNamespaces.Text, "text");
            rootList.SetAttribute("style-name", OdfNamespaces.Text, EnsureListStyle(context, info.NumberingId), "text");
            if (!context.UsedNumberingIds.Add(info.NumberingId))
            {
                // 被其他內容打斷後回到同一份編號：延續先前的編號，而不是重新開始。
                rootList.SetAttribute("continue-numbering", OdfNamespaces.Text, "true", "text");
            }

            container.AppendChild(rootList);
            state = new ListState(info.NumberingId, rootList);
        }

        while (state.LevelLists.Count - 1 < info.Level)
        {
            OdfNode parentList = state.LevelLists[state.LevelLists.Count - 1];
            if (!state.LastItems.TryGetValue(parentList, out OdfNode? parentItem))
            {
                parentItem = new OdfNode(OdfNodeType.Element, "list-item", OdfNamespaces.Text, "text");
                parentList.AppendChild(parentItem);
                state.LastItems[parentList] = parentItem;
            }

            var nested = new OdfNode(OdfNodeType.Element, "list", OdfNamespaces.Text, "text");
            parentItem.AppendChild(nested);
            state.LevelLists.Add(nested);
        }

        while (state.LevelLists.Count - 1 > info.Level)
        {
            state.LastItems.Remove(state.LevelLists[state.LevelLists.Count - 1]);
            state.LevelLists.RemoveAt(state.LevelLists.Count - 1);
        }

        OdfNode targetList = state.LevelLists[info.Level];
        var item = new OdfNode(OdfNodeType.Element, "list-item", OdfNamespaces.Text, "text");
        item.AppendChild(paragraphNode);
        targetList.AppendChild(item);
        state.LastItems[targetList] = item;
        return state;
    }

    private static bool ParagraphHasContent(WP.Paragraph paragraph) =>
        GetParagraphPlainText(paragraph).Length > 0
        || paragraph.Descendants<WP.FieldChar>().Any()
        || paragraph.Descendants<WP.SimpleField>().Any()
        || paragraph.Descendants<WP.Drawing>().Any()
        || paragraph.Descendants<WP.FootnoteReference>().Any()
        || paragraph.Descendants<WP.EndnoteReference>().Any();

    /// <summary>
    /// 以與本文相同的段落轉換建立儲存格（或頁首頁尾）內的段落：樣式、標題階層、清單、註腳與圖片都保留。
    /// 先照原流程建立段落，再把產生的節點搬進容器。
    /// </summary>
    private static void AppendContainerParagraph(
        BodyContext context,
        OdfNode container,
        WP.Paragraph paragraph,
        ref ListState? containerList)
    {
        OdfNode body = context.Document.BodyTextRoot;
        ConvertParagraph(context.OwnerPart, paragraph, context.Document);
        OdfNode produced = body.LastChild!;
        body.RemoveChild(produced);

        ParagraphListInfo? list = GetListInfo(context, paragraph);
        if (list is not null)
        {
            containerList = PlaceInList(context, container, produced, list.Value, containerList);
        }
        else
        {
            containerList = null;
            container.AppendChild(produced);
        }
    }

    private static string EnsureListStyle(BodyContext context, int numberingId)
    {
        if (context.ListStyleNames.TryGetValue(numberingId, out string? existing))
        {
            return existing;
        }

        string styleName = "DocxList" + numberingId.ToString(CultureInfo.InvariantCulture);
        OdfNode autoStyles = TextDocumentDomHelper.FindOrCreateChild(
            context.Document.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        var listStyle = new OdfNode(OdfNodeType.Element, "list-style", OdfNamespaces.Text, "text");
        listStyle.SetAttribute("name", OdfNamespaces.Style, styleName, "style");

        for (int level = 0; level < MaxListLevels; level++)
        {
            WP.Level? definition = null;
            int? start = null;
            if (context.Numbering is not null)
            {
                definition = context.Numbering.FindLevel(numberingId, level, out start);
            }

            listStyle.AppendChild(CreateListLevelStyle(definition, level, start));
        }

        autoStyles.AppendChild(listStyle);
        context.ListStyleNames[numberingId] = styleName;
        return styleName;
    }

    private static OdfNode CreateListLevelStyle(WP.Level? definition, int level, int? startOverride)
    {
        string format = definition?.NumberingFormat?.Val is { } formatValue ? formatValue.InnerText ?? "decimal" : "decimal";
        string levelText = definition?.LevelText?.Val?.Value ?? string.Empty;
        bool isBullet = definition is null
            ? true
            : string.Equals(format, "bullet", StringComparison.OrdinalIgnoreCase)
              || string.Equals(format, "none", StringComparison.OrdinalIgnoreCase);

        OdfNode levelNode;
        if (isBullet)
        {
            levelNode = new OdfNode(OdfNodeType.Element, "list-level-style-bullet", OdfNamespaces.Text, "text");
            levelNode.SetAttribute("level", OdfNamespaces.Text, (level + 1).ToString(CultureInfo.InvariantCulture), "text");
            levelNode.SetAttribute("bullet-char", OdfNamespaces.Text, MapBulletCharacter(levelText, level), "text");
        }
        else
        {
            levelNode = new OdfNode(OdfNodeType.Element, "list-level-style-number", OdfNamespaces.Text, "text");
            levelNode.SetAttribute("level", OdfNamespaces.Text, (level + 1).ToString(CultureInfo.InvariantCulture), "text");
            levelNode.SetAttribute("num-format", OdfNamespaces.Style, MapNumberFormat(format), "style");
            ParseLevelText(levelText, out string prefix, out string suffix, out int displayLevels);
            if (prefix.Length > 0)
            {
                levelNode.SetAttribute("num-prefix", OdfNamespaces.Style, prefix, "style");
            }

            if (suffix.Length > 0)
            {
                levelNode.SetAttribute("num-suffix", OdfNamespaces.Style, suffix, "style");
            }

            if (displayLevels > 1)
            {
                levelNode.SetAttribute("display-levels", OdfNamespaces.Text, displayLevels.ToString(CultureInfo.InvariantCulture), "text");
            }

            int startValue = startOverride ?? definition?.StartNumberingValue?.Val?.Value ?? 1;
            if (startValue != 1 && startValue >= 0)
            {
                levelNode.SetAttribute("start-value", OdfNamespaces.Text, startValue.ToString(CultureInfo.InvariantCulture), "text");
            }
        }

        AppendListLevelProperties(levelNode, definition, level);
        return levelNode;
    }

    private static void AppendListLevelProperties(OdfNode levelNode, WP.Level? definition, int level)
    {
        TryReadOpenXmlTwips(definition?.PreviousParagraphProperties?.Indentation?.Left, out int leftTwips);
        TryReadOpenXmlTwips(definition?.PreviousParagraphProperties?.Indentation?.Hanging, out int hangingTwips);
        if (leftTwips <= 0 && definition?.PreviousParagraphProperties?.Indentation?.Left is null)
        {
            leftTwips = 360 * (level + 1);
        }

        if (hangingTwips <= 0)
        {
            hangingTwips = 360;
        }

        string followedBy = "listtab";
        if (definition?.LevelSuffix?.Val is { } suffixValue)
        {
            if (suffixValue.Value == WP.LevelSuffixValues.Space)
            {
                followedBy = "space";
            }
            else if (suffixValue.Value == WP.LevelSuffixValues.Nothing)
            {
                followedBy = "nothing";
            }
        }

        var properties = new OdfNode(OdfNodeType.Element, "list-level-properties", OdfNamespaces.Style, "style");
        properties.SetAttribute("list-level-position-and-space-mode", OdfNamespaces.Text, "label-alignment", "text");
        var alignment = new OdfNode(OdfNodeType.Element, "list-level-label-alignment", OdfNamespaces.Style, "style");
        alignment.SetAttribute("label-followed-by", OdfNamespaces.Text, followedBy, "text");
        if (followedBy == "listtab")
        {
            alignment.SetAttribute("list-tab-stop-position", OdfNamespaces.Text, TwipsToCentimeters(leftTwips), "text");
        }

        alignment.SetAttribute("text-indent", OdfNamespaces.Fo, "-" + TwipsToCentimeters(hangingTwips), "fo");
        alignment.SetAttribute("margin-left", OdfNamespaces.Fo, TwipsToCentimeters(leftTwips), "fo");
        properties.AppendChild(alignment);
        levelNode.AppendChild(properties);
    }

    private static string TwipsToCentimeters(int twips) =>
        (twips / 1440.0 * 2.54).ToString("0.###", CultureInfo.InvariantCulture) + "cm";

    private static string MapBulletCharacter(string levelText, int level)
    {
        if (levelText.Length == 0)
        {
            return level % 2 == 0 ? "•" : "◦";
        }

        char first = levelText[0];
        if (first >= '' && first <= '')
        {
            // Symbol／Wingdings 字型的私用區字元沒有對應字型時無法顯示；常見的實心圓與方塊改為標準字元。
            return (first & 0xFF) switch
            {
                0xA7 => "▪",
                0xA8 => "◻",
                0xD8 => "➢",
                0xFC => "✓",
                0xB7 => "•",
                _ => "•",
            };
        }

        return first switch
        {
            'o' => "◦",
            '·' => "•",
            '§' => "▪",
            _ => first.ToString(),
        };
    }

    private static string MapNumberFormat(string format) =>
        format switch
        {
            "decimal" or "decimalZero" or "decimalFullWidth" or "decimalHalfWidth" => "1",
            "lowerLetter" => "a",
            "upperLetter" => "A",
            "lowerRoman" => "i",
            "upperRoman" => "I",
            "chineseCounting" or "chineseCountingThousand" or "taiwaneseCounting" or "taiwaneseCountingThousand"
                or "ideographDigital" => "一, 二, 三, ...",
            "ideographLegalTraditional" or "chineseLegalSimplified" => "壹, 貳, 參, ...",
            _ => "1",
        };

    /// <summary>
    /// 把 Word 的 <c>%1.</c>、<c>(%1)</c>、<c>%1.%2.</c> 這類編號文字拆成前綴、後綴與顯示層數。
    /// </summary>
    private static void ParseLevelText(string levelText, out string prefix, out string suffix, out int displayLevels)
    {
        prefix = string.Empty;
        suffix = string.Empty;
        displayLevels = 1;
        int first = levelText.IndexOf('%');
        if (first < 0)
        {
            suffix = levelText;
            return;
        }

        prefix = levelText.Substring(0, first);
        int last = first;
        int count = 0;
        for (int i = 0; i < levelText.Length - 1; i++)
        {
            if (levelText[i] == '%' && char.IsDigit(levelText[i + 1]))
            {
                count++;
                last = i + 1;
            }
        }

        displayLevels = Math.Max(1, count);
        suffix = last + 1 < levelText.Length ? levelText.Substring(last + 1) : string.Empty;
    }

    // ---------- 註腳與章節附註 ----------

    /// <summary>
    /// 把 <c>w:footnoteReference</c>、<c>w:endnoteReference</c> 轉成 <c>text:note</c>；
    /// 附註內文取自註腳／章節附註部件，多個段落各自成為 <c>text:p</c>。
    /// </summary>
    private static void AppendNotes(OpenXmlPartContainer part, WP.Run run, TextDocument odtDocument, OdfParagraph odtParagraph)
    {
        if (part is not MainDocumentPart mainPart)
        {
            // 註腳與章節附註只存在於主文件部件。
            return;
        }

        foreach (OpenXmlElement child in run.ChildElements)
        {
            if (child is WP.FootnoteReference footnote)
            {
                AppendNote(mainPart, footnote.Id?.Value, endnote: false, odtDocument, odtParagraph);
            }
            else if (child is WP.EndnoteReference endnote)
            {
                AppendNote(mainPart, endnote.Id?.Value, endnote: true, odtDocument, odtParagraph);
            }
        }
    }

    private static void AppendNote(MainDocumentPart mainPart, long? id, bool endnote, TextDocument odtDocument, OdfParagraph odtParagraph)
    {
        if (id is null)
        {
            return;
        }

        IEnumerable<WP.Paragraph>? paragraphs = endnote
            ? mainPart.EndnotesPart?.Endnotes?.Elements<WP.Endnote>().FirstOrDefault(note => note.Id?.Value == id)?.Elements<WP.Paragraph>()
            : mainPart.FootnotesPart?.Footnotes?.Elements<WP.Footnote>().FirstOrDefault(note => note.Id?.Value == id)?.Elements<WP.Paragraph>();
        if (paragraphs is null)
        {
            return;
        }

        List<string> texts = paragraphs.Select(GetParagraphPlainText).ToList();
        if (texts.Count == 0)
        {
            return;
        }

        // 內文第一段開頭是註腳標記與其後的空白，ODF 的 text:note-citation 另外顯示標記。
        texts[0] = texts[0].TrimStart();
        NoteCounters counters = s_noteCounters.GetOrCreateValue(odtDocument);
        string citation = (endnote ? ++counters.Endnotes : ++counters.Footnotes).ToString(CultureInfo.InvariantCulture);
        if (endnote)
        {
            odtParagraph.AddEndnote(citation, texts[0]);
        }
        else
        {
            odtParagraph.AddFootnote(citation, texts[0]);
        }

        if (texts.Count == 1)
        {
            return;
        }

        OdfNode? noteNode = odtParagraph.Node.LastChild;
        OdfNode? body = noteNode?.Children.FirstOrDefault(child => child.LocalName == "note-body");
        if (body is null)
        {
            return;
        }

        foreach (string extra in texts.Skip(1).Where(text => text.Length > 0))
        {
            var paragraphNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            paragraphNode.TextContent = extra;
            body.AppendChild(paragraphNode);
        }
    }

    // ---------- 頁首與頁尾 ----------

    /// <summary>
    /// 轉換頁首與頁尾：內容走與本文相同的段落與表格轉換（樣式、清單、圖片、欄位都保留），
    /// 依 <c>w:titlePg</c> 與 <c>w:evenAndOddHeaders</c> 建立首頁與偶數頁的版本。
    /// </summary>
    private static void ConvertHeaderFooter(BodyContext context, WP.Body body)
    {
        MainDocumentPart mainPart = context.MainPart;
        WP.SectionProperties? section = body.Elements<WP.SectionProperties>().LastOrDefault();
        bool titlePage = section?.GetFirstChild<WP.TitlePage>() is { } titlePageElement
            && (titlePageElement.Val is null || titlePageElement.Val.Value);
        bool evenAndOdd = mainPart.DocumentSettingsPart?.Settings?.GetFirstChild<WP.EvenAndOddHeaders>() is { } evenAndOddElement
            && (evenAndOddElement.Val is null || evenAndOddElement.Val.Value);
        OdfPageSetup setup = context.Document.GetDefaultPageSetup();

        bool anyHeader = false;
        bool anyFooter = false;
        var produced = new HashSet<string>(StringComparer.Ordinal);
        if (section is not null)
        {
            foreach (WP.HeaderReference reference in section.Elements<WP.HeaderReference>())
            {
                HeaderPart? part = FindPart<HeaderPart>(mainPart, reference.Id?.Value);
                OdfPageHeaderFooter? region = SelectRegion(
                    reference.Type?.Value,
                    titlePage,
                    evenAndOdd,
                    setup.Header,
                    setup.HeaderFirst,
                    setup.HeaderLeft,
                    out string regionKey);
                if (part?.Header is not null && region is not null)
                {
                    anyHeader = true;
                    if (FillRegion(context, region, part, part.Header.ChildElements))
                    {
                        produced.Add("header" + regionKey);
                    }
                }
            }

            foreach (WP.FooterReference reference in section.Elements<WP.FooterReference>())
            {
                FooterPart? part = FindPart<FooterPart>(mainPart, reference.Id?.Value);
                OdfPageHeaderFooter? region = SelectRegion(
                    reference.Type?.Value,
                    titlePage,
                    evenAndOdd,
                    setup.Footer,
                    setup.FooterFirst,
                    setup.FooterLeft,
                    out string regionKey);
                if (part?.Footer is not null && region is not null)
                {
                    anyFooter = true;
                    if (FillRegion(context, region, part, part.Footer.ChildElements))
                    {
                        produced.Add("footer" + regionKey);
                    }
                }
            }
        }

        // 沒有章節屬性或沒有參照時，沿用第一個頁首與頁尾部件作為預設版本。
        if (!anyHeader && mainPart.HeaderParts.FirstOrDefault() is { Header: not null } headerPart)
        {
            FillRegion(context, setup.Header, headerPart, headerPart.Header.ChildElements);
        }

        if (!anyFooter && mainPart.FooterParts.FirstOrDefault() is { Footer: not null } footerPart)
        {
            FillRegion(context, setup.Footer, footerPart, footerPart.Footer.ChildElements);
        }

        // ODF 的 style:header-left 與 style:header-first 只能出現在 style:header 之後（頁尾同理）；
        // Word 的預設頁首沒有內容而只有首頁或偶數頁版本時，補一個空的預設區域以維持結構合法。
        if (!produced.Contains("header") && (produced.Contains("headerFirst") || produced.Contains("headerEven")))
        {
            setup.Header.GetOrCreateParagraph();
        }

        if (!produced.Contains("footer") && (produced.Contains("footerFirst") || produced.Contains("footerEven")))
        {
            setup.Footer.GetOrCreateParagraph();
        }
    }

    private static TPart? FindPart<TPart>(MainDocumentPart mainPart, string? relationshipId)
        where TPart : OpenXmlPart
    {
        if (string.IsNullOrEmpty(relationshipId))
        {
            return null;
        }

        foreach (IdPartPair pair in mainPart.Parts)
        {
            if (pair.RelationshipId == relationshipId)
            {
                return pair.OpenXmlPart as TPart;
            }
        }

        return null;
    }

    private static OdfPageHeaderFooter? SelectRegion(
        WP.HeaderFooterValues? type,
        bool titlePage,
        bool evenAndOdd,
        OdfPageHeaderFooter defaultRegion,
        OdfPageHeaderFooter firstRegion,
        OdfPageHeaderFooter evenRegion,
        out string key)
    {
        key = string.Empty;
        if (type is null || type.Value == WP.HeaderFooterValues.Default)
        {
            return defaultRegion;
        }

        if (type.Value == WP.HeaderFooterValues.First)
        {
            key = "First";
            return titlePage ? firstRegion : null;
        }

        key = "Even";
        return evenAndOdd ? evenRegion : null;
    }

    private static bool FillRegion(
        BodyContext context,
        OdfPageHeaderFooter region,
        OpenXmlPartContainer part,
        IEnumerable<OpenXmlElement> content)
    {
        OdfParagraph placeholder = region.GetOrCreateParagraph();
        OdfNode regionNode = placeholder.Node.Parent!;
        regionNode.RemoveChild(placeholder.Node);

        OpenXmlPartContainer previousOwner = context.OwnerPart;
        context.OwnerPart = part;
        try
        {
            ListState? list = null;
            foreach (OpenXmlElement child in content)
            {
                if (child is WP.Paragraph paragraph)
                {
                    if (ParagraphHasContent(paragraph))
                    {
                        AppendContainerParagraph(context, regionNode, paragraph, ref list);
                    }
                }
                else if (child is WP.Table table)
                {
                    list = null;
                    AppendContainerTable(context, regionNode, table);
                }
            }
        }
        finally
        {
            context.OwnerPart = previousOwner;
        }

        // Word 的預設頁首頁尾常只含一個空段落；沒有實際內容就不建立 ODF 的頁首頁尾區域。
        if (regionNode.Children.Count == 0)
        {
            region.Clear();
            return false;
        }

        return true;
    }

    private static void AppendContainerTable(BodyContext context, OdfNode container, WP.Table table)
    {
        OdfNode body = context.Document.BodyTextRoot;
        OdfNode? previous = body.LastChild;
        ConvertTable(context, table);
        OdfNode? produced = body.LastChild;
        if (produced is not null && !ReferenceEquals(produced, previous))
        {
            body.RemoveChild(produced);
            container.AppendChild(produced);
        }
    }

    // ---------- 欄位（頁碼與總頁數） ----------

    private enum FieldKind
    {
        None,
        PageNumber,
        PageCount,
    }

    private sealed class FieldScanner
    {
        internal System.Text.StringBuilder Instruction { get; } = new();

        internal bool InResult { get; set; }

        internal FieldKind Kind { get; set; }
    }

    private static FieldKind ClassifyField(string instruction)
    {
        string trimmed = instruction.Trim();
        int space = trimmed.IndexOf(' ');
        string name = (space < 0 ? trimmed : trimmed.Substring(0, space)).ToUpperInvariant();
        return name switch
        {
            "PAGE" => FieldKind.PageNumber,
            "NUMPAGES" or "SECTIONPAGES" => FieldKind.PageCount,
            _ => FieldKind.None,
        };
    }

    private static void AppendFieldNode(OdfParagraph paragraph, FieldKind kind)
    {
        if (kind == FieldKind.PageNumber)
        {
            var node = new OdfNode(OdfNodeType.Element, "page-number", OdfNamespaces.Text, "text");
            node.SetAttribute("select-page", OdfNamespaces.Text, "current", "text");
            paragraph.Node.AppendChild(node);
        }
        else if (kind == FieldKind.PageCount)
        {
            paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Element, "page-count", OdfNamespaces.Text, "text"));
        }
    }

    private static bool TryAppendSimpleField(WP.SimpleField field, OdfParagraph paragraph)
    {
        FieldKind kind = ClassifyField(field.Instruction?.Value ?? string.Empty);
        if (kind == FieldKind.None)
        {
            return false;
        }

        AppendFieldNode(paragraph, kind);
        return true;
    }

    /// <summary>
    /// 處理由多個執行區段組成的複雜欄位（<c>begin</c>、指令、<c>separate</c>、結果、<c>end</c>）：
    /// 頁碼與總頁數轉成 ODF 欄位並略過 Word 儲存的結果文字（否則會變成固定的「1」），
    /// 其他欄位保留結果文字。傳回該執行區段是否已被欄位處理而不需再轉換。
    /// </summary>
    private static bool TryConsumeFieldRun(ref FieldScanner? scanner, WP.Run run, OdfParagraph paragraph)
    {
        bool consumed = false;
        foreach (OpenXmlElement child in run.ChildElements)
        {
            if (child is WP.FieldChar fieldChar)
            {
                consumed = true;
                var type = fieldChar.FieldCharType?.Value;
                if (type == WP.FieldCharValues.Begin)
                {
                    scanner = new FieldScanner();
                }
                else if (type == WP.FieldCharValues.Separate && scanner is not null)
                {
                    scanner.InResult = true;
                    scanner.Kind = ClassifyField(scanner.Instruction.ToString());
                }
                else if (type == WP.FieldCharValues.End && scanner is not null)
                {
                    FieldKind kind = scanner.InResult ? scanner.Kind : ClassifyField(scanner.Instruction.ToString());
                    AppendFieldNode(paragraph, kind);
                    scanner = null;
                }
            }
            else if (child is WP.FieldCode code)
            {
                consumed = true;
                scanner?.Instruction.Append(code.Text);
            }
        }

        if (!consumed && scanner is not null && (!scanner.InResult || scanner.Kind != FieldKind.None))
        {
            consumed = true;
        }

        return consumed;
    }
}

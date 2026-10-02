using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace OdfKit.Conversion;

/// <summary>
/// Body traversal for DOCX to ODT: block-level content controls, tables of contents, section starts, columns and page-number restarts.
/// DOCX → ODT 轉換的本文走訪：區塊層級內容控制項、目錄、章節起點、分欄與頁碼重新起算。
/// </summary>
public static partial class DocxToOdtConverter
{
    // ---------- 區塊展開 ----------

    /// <summary>
    /// 依文件順序展開本文區塊：區塊層級的內容控制項（目錄、封面、書目等常包在其中）與自訂 XML 區塊
    /// 只是容器，其中的段落與表格視為本文的一部分。
    /// </summary>
    private static List<OpenXmlElement> FlattenBlocks(IEnumerable<OpenXmlElement> elements)
    {
        var result = new List<OpenXmlElement>();
        FlattenBlocks(elements, result);
        return result;
    }

    private static void FlattenBlocks(IEnumerable<OpenXmlElement> elements, List<OpenXmlElement> result)
    {
        foreach (OpenXmlElement element in elements)
        {
            switch (element)
            {
                case WP.Paragraph or WP.Table:
                    result.Add(element);
                    break;
                case WP.SdtBlock sdt when sdt.SdtContentBlock is not null:
                    FlattenBlocks(sdt.SdtContentBlock.ChildElements, result);
                    break;
                case WP.CustomXmlBlock customXml:
                    FlattenBlocks(customXml.ChildElements, result);
                    break;
            }
        }
    }

    // ---------- 本文迴圈 ----------

    // 某個章節起頭要套用在第一個區塊上的設定。
    private readonly record struct SectionStart(bool Pending, string? MasterPageName, bool PageBreak, int? PageNumber);

    private static SectionStart StartOf(List<SectionInfo> sections, int index, ref string activeMasterPage)
    {
        SectionInfo section = sections[index];
        if (index == 0)
        {
            // 第一個章節本來就從第一頁開始；只有頁碼不是從 1 起算時才需要在第一個區塊標示。
            return section.PageNumberStart is null
                ? default
                : new SectionStart(true, activeMasterPage, false, section.PageNumberStart);
        }

        // 連續章節不換頁，也就不能改頁面設定或重新起算頁碼。
        if (section.Continuous)
        {
            return default;
        }

        if (section.MasterPageName is not null)
        {
            activeMasterPage = section.MasterPageName;
        }

        // 重新起算頁碼時一併指定目前的主頁面：LibreOffice 只有在「帶主頁面的換頁」上才套用起始頁碼。
        string? master = section.MasterPageName ?? (section.PageNumberStart is null ? null : activeMasterPage);
        return new SectionStart(true, master, section.NeedsPageBreak, section.PageNumberStart);
    }

    private static TextDocument ConvertBody(WP.Body body, BodyContext context, TextDocument odtDocument)
    {
        List<SectionInfo> sections = CollectSections(body);
        ConfigureSections(context, sections);
        List<OpenXmlElement> blocks = FlattenBlocks(body.ChildElements);
        Dictionary<int, TocGroup> tocGroups = FindTocGroups(blocks);
        OdfNode bodyRoot = odtDocument.BodyTextRoot;

        int sectionIndex = 0;
        string activeMasterPage = "Standard";
        SectionStart start = StartOf(sections, 0, ref activeMasterPage);
        OdfNode? columnStart = null;
        for (int index = 0; index < blocks.Count; index++)
        {
            OpenXmlElement child = blocks[index];
            OdfNode? before = bodyRoot.LastChild;
            if (tocGroups.TryGetValue(index, out TocGroup toc))
            {
                ConvertToc(context, blocks, toc);
                index = toc.End;
                child = blocks[index];
            }
            else if (child is WP.Paragraph paragraph)
            {
                ConvertBodyParagraph(context, paragraph);
            }
            else if (child is WP.Table table)
            {
                ConvertBodyTable(context, table);
            }

            // 章節的第一個區塊套用起點設定並作為分欄範圍的起點；區塊尚未產生
            // （例如只承載分頁符號的段落）時延後到下一個區塊。
            OdfNode? first = before is null ? bodyRoot.FirstChild : before.NextSibling;
            if (first is not null)
            {
                if (start.Pending)
                {
                    ApplySectionStart(context, first, start);
                    start = default;
                }

                columnStart ??= first;
            }

            // 帶有 w:sectPr 的段落是該章節的最後一個段落：下一個區塊屬於下一個章節。
            if (child is WP.Paragraph { ParagraphProperties.SectionProperties: not null }
                && sectionIndex < sections.Count - 1)
            {
                WrapInColumns(context, bodyRoot, columnStart, sections[sectionIndex]);
                columnStart = null;
                sectionIndex++;
                SectionStart next = StartOf(sections, sectionIndex, ref activeMasterPage);
                start = next.Pending ? next : start;
            }
        }

        WrapInColumns(context, bodyRoot, columnStart, sections[Math.Min(sectionIndex, sections.Count - 1)]);
        return odtDocument;
    }

    /// <summary>
    /// 在章節的第一個區塊上標示章節起點：指定主頁面（<c>style:master-page-name</c>，ODF 於該處換頁並改用新的頁面設定）、
    /// 頁面設定相同但 Word 仍會換頁的章節以 <c>fo:break-before</c> 換頁，並依 <c>w:pgNumType</c> 重新起算頁碼（<c>style:page-number</c>）。
    /// </summary>
    private static void ApplySectionStart(BodyContext context, OdfNode block, SectionStart start)
    {
        OdfNode target = FindFirstTextBlock(block) ?? block;
        OdfNode autoStyles = TextDocumentDomHelper.FindOrCreateChild(
            context.Document.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        string styleName = "DocxSectionStyle" + (++context.SectionStyleCount).ToString(CultureInfo.InvariantCulture);
        var style = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        style.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
        if (start.MasterPageName is not null)
        {
            style.SetAttribute("master-page-name", OdfNamespaces.Style, start.MasterPageName, "style");
        }

        bool isTable = target.LocalName == "table" && target.NamespaceUri == OdfNamespaces.Table;
        string propertiesName = isTable ? "table-properties" : "paragraph-properties";
        var properties = new OdfNode(OdfNodeType.Element, propertiesName, OdfNamespaces.Style, "style");
        if (start.PageBreak && start.MasterPageName is null)
        {
            properties.SetAttribute("break-before", OdfNamespaces.Fo, "page", "fo");
        }

        if (start.PageNumber is int pageNumber)
        {
            properties.SetAttribute("page-number", OdfNamespaces.Style, pageNumber.ToString(CultureInfo.InvariantCulture), "style");
        }

        if ((start.PageBreak && start.MasterPageName is null) || start.PageNumber is not null)
        {
            style.AppendChild(properties);
        }

        string styleAttributeNamespace = isTable ? OdfNamespaces.Table : OdfNamespaces.Text;
        string styleAttributePrefix = isTable ? "table" : "text";
        style.SetAttribute("family", OdfNamespaces.Style, isTable ? "table" : "paragraph", "style");
        string? existing = target.GetAttribute("style-name", styleAttributeNamespace);
        if (!string.IsNullOrEmpty(existing))
        {
            style.SetAttribute("parent-style-name", OdfNamespaces.Style, existing!, "style");
        }

        target.SetAttribute("style-name", styleAttributeNamespace, styleName, styleAttributePrefix);
        autoStyles.AppendChild(style);
    }

    // ---------- 分欄 ----------

    /// <summary>
    /// 章節有多欄（<c>w:cols</c>）時，把該章節的區塊包進套用分欄樣式的 <c>text:section</c>。
    /// </summary>
    private static void WrapInColumns(BodyContext context, OdfNode bodyRoot, OdfNode? columnStart, SectionInfo section)
    {
        if (columnStart is null || columnStart.Parent != bodyRoot || section.Columns is not { } columns)
        {
            return;
        }

        int count = columns.ColumnCount?.Value ?? 1;
        if (count < 2)
        {
            return;
        }

        var members = new List<OdfNode>();
        for (OdfNode? node = columnStart; node is not null; node = node.NextSibling)
        {
            members.Add(node);
        }

        OdfNode autoStyles = TextDocumentDomHelper.FindOrCreateChild(
            context.Document.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
        string number = (++context.ColumnSectionCount).ToString(CultureInfo.InvariantCulture);
        string styleName = "DocxColumns" + number;
        autoStyles.AppendChild(CreateColumnsStyle(styleName, columns, count));

        var sectionNode = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        sectionNode.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        sectionNode.SetAttribute("name", OdfNamespaces.Text, "DocxSection" + number, "text");
        bodyRoot.InsertBefore(sectionNode, columnStart);
        foreach (OdfNode member in members)
        {
            bodyRoot.RemoveChild(member);
            sectionNode.AppendChild(member);
        }
    }

    private static OdfNode CreateColumnsStyle(string styleName, WP.Columns columns, int count)
    {
        var style = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        style.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
        style.SetAttribute("family", OdfNamespaces.Style, "section", "style");
        var properties = new OdfNode(OdfNodeType.Element, "section-properties", OdfNamespaces.Style, "style");
        properties.SetAttribute("dont-balance-text-columns", OdfNamespaces.Text, "false", "text");
        var columnsNode = new OdfNode(OdfNodeType.Element, "columns", OdfNamespaces.Style, "style");
        columnsNode.SetAttribute("column-count", OdfNamespaces.Fo, count.ToString(CultureInfo.InvariantCulture), "fo");

        double gapTwips = double.TryParse(columns.Space?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double space) ? space : 720;
        columnsNode.SetAttribute("column-gap", OdfNamespaces.Fo, FormatCentimeters(TwipsToCentimeters(gapTwips)), "fo");

        if (columns.Separator?.Value == true)
        {
            var line = new OdfNode(OdfNodeType.Element, "column-sep", OdfNamespaces.Style, "style");
            line.SetAttribute("style", OdfNamespaces.Style, "solid", "style");
            line.SetAttribute("width", OdfNamespaces.Style, "0.018cm", "style");
            line.SetAttribute("color", OdfNamespaces.Style, "#000000", "style");
            line.SetAttribute("height", OdfNamespaces.Style, "100%", "style");
            columnsNode.AppendChild(line);
        }

        WP.Column[] custom = columns.Elements<WP.Column>().ToArray();
        bool equalWidth = columns.EqualWidth?.Value ?? true;
        if (!equalWidth && custom.Length == count)
        {
            for (int index = 0; index < custom.Length; index++)
            {
                double width = double.TryParse(custom[index].Width?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double parsedWidth) ? parsedWidth : 1;
                double after = index == custom.Length - 1 ? 0 : (double.TryParse(custom[index].Space?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double parsedSpace) ? parsedSpace : gapTwips);
                var column = new OdfNode(OdfNodeType.Element, "column", OdfNamespaces.Style, "style");
                column.SetAttribute("rel-width", OdfNamespaces.Style, Math.Max(1, (int)width).ToString(CultureInfo.InvariantCulture) + "*", "style");
                column.SetAttribute("start-indent", OdfNamespaces.Fo, "0cm", "fo");
                column.SetAttribute("end-indent", OdfNamespaces.Fo, FormatCentimeters(TwipsToCentimeters(after)), "fo");
                columnsNode.AppendChild(column);
            }
        }

        properties.AppendChild(columnsNode);
        style.AppendChild(properties);
        return style;
    }

    // ---------- 目錄 ----------

    // 目錄欄位在本文區塊清單中的範圍（含起點與終點段落）與欄位指令。
    private readonly record struct TocGroup(int Start, int End, string Instruction);

    /// <summary>
    /// 找出 <c>TOC</c> 複雜欄位：欄位起點在一個段落，結果是之後的多個段落，終點在最後一個段落。
    /// 目錄項目內的 <c>PAGEREF</c> 等巢狀欄位以深度計數略過。
    /// </summary>
    private static Dictionary<int, TocGroup> FindTocGroups(List<OpenXmlElement> blocks)
    {
        var groups = new Dictionary<int, TocGroup>();
        int depth = 0;
        int candidateStart = -1;
        int tocStart = -1;
        bool collecting = false;
        var instruction = new StringBuilder();
        string tocInstruction = string.Empty;
        for (int index = 0; index < blocks.Count; index++)
        {
            if (blocks[index] is not WP.Paragraph paragraph)
            {
                continue;
            }

            foreach (OpenXmlElement element in paragraph.Descendants())
            {
                if (element is WP.FieldChar fieldChar)
                {
                    WP.FieldCharValues? type = fieldChar.FieldCharType?.Value;
                    if (type == WP.FieldCharValues.Begin)
                    {
                        depth++;
                        if (depth == 1 && tocStart < 0)
                        {
                            collecting = true;
                            candidateStart = index;
                            instruction.Clear();
                        }
                    }
                    else if (type == WP.FieldCharValues.Separate)
                    {
                        if (collecting && depth == 1)
                        {
                            collecting = false;
                            string text = instruction.ToString().TrimStart();
                            if (text.StartsWith("TOC", StringComparison.OrdinalIgnoreCase)
                                && (text.Length == 3 || char.IsWhiteSpace(text[3])))
                            {
                                tocStart = candidateStart;
                                tocInstruction = text;
                            }
                        }
                    }
                    else if (type == WP.FieldCharValues.End)
                    {
                        collecting = false;
                        depth = Math.Max(0, depth - 1);
                        if (depth == 0 && tocStart >= 0)
                        {
                            groups[tocStart] = new TocGroup(tocStart, index, tocInstruction);
                            tocStart = -1;
                        }
                    }
                }
                else if (element is WP.FieldCode code && collecting)
                {
                    instruction.Append(code.Text);
                }
            }
        }

        return groups;
    }

    /// <summary>
    /// 把 <c>TOC</c> 欄位轉成 <c>text:table-of-content</c>：欄位指令的標題層級範圍（<c>\o "1-3"</c>）寫入來源，
    /// Word 儲存的目錄項目段落（含超連結與頁碼）放在 <c>text:index-body</c>，由 ODF 編輯器更新目錄時重新產生。
    /// </summary>
    private static void ConvertToc(BodyContext context, List<OpenXmlElement> blocks, TocGroup group)
    {
        OdfNode root = context.Document.BodyTextRoot;
        var toc = new OdfNode(OdfNodeType.Element, "table-of-content", OdfNamespaces.Text, "text");
        toc.SetAttribute("name", OdfNamespaces.Text, "Table of Contents" + (++context.TocCount).ToString(CultureInfo.InvariantCulture), "text");
        toc.SetAttribute("protected", OdfNamespaces.Text, "false", "text");

        var source = new OdfNode(OdfNodeType.Element, "table-of-content-source", OdfNamespaces.Text, "text");
        Match levels = Regex.Match(group.Instruction, "\\\\o\\s+\"?(\\d+)-(\\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (levels.Success)
        {
            source.SetAttribute("outline-level", OdfNamespaces.Text, levels.Groups[2].Value, "text");
            source.SetAttribute("use-outline-level", OdfNamespaces.Text, "true", "text");
        }

        toc.AppendChild(source);
        var indexBody = new OdfNode(OdfNodeType.Element, "index-body", OdfNamespaces.Text, "text");
        toc.AppendChild(indexBody);
        root.AppendChild(toc);

        for (int index = group.Start; index <= group.End; index++)
        {
            if (blocks[index] is not WP.Paragraph paragraph || !TocParagraphHasContent(paragraph))
            {
                continue;
            }

            ConvertBodyParagraph(context, paragraph);
            var produced = new List<OdfNode>();
            for (OdfNode? node = toc.NextSibling; node is not null; node = node.NextSibling)
            {
                produced.Add(node);
            }

            foreach (OdfNode node in produced)
            {
                root.RemoveChild(node);
                indexBody.AppendChild(node);
            }
        }
    }

    // 只含欄位起訖字元的段落（例如目錄最後只放欄位結束的段落）不轉成空白項目。
    private static bool TocParagraphHasContent(WP.Paragraph paragraph) =>
        GetParagraphPlainText(paragraph).Length > 0 || paragraph.Descendants<WP.Drawing>().Any();
}

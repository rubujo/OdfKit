using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Export.PdfInternal;
using OdfKit.Styles;
using OdfKit.Text;

#if NETSTANDARD2_0
using ArgumentNullException = OdfKit.Export.Shim.ArgumentNullException;
#endif

using OdfKit.Compliance;
namespace OdfKit.Export;

/// <summary>
/// Exports ODF documents to PDF.
/// 將 TextDocument 匯出為 PDF 的工具類別。
/// </summary>
/// <remarks>
/// 完全以託管程式碼排版並產生 PDF（不依賴第三方 PDF 函式庫）：內嵌 TrueType 字型子集，
/// 支援段落、標題、清單、表格（含合併儲存格與標題列重複）、超連結、腳註與章節附註，以及 PNG／JPEG／GIF／BMP 圖片。
/// </remarks>
public static class OdfPdfExporter
{
    /// <summary>
    /// Exports PDF to a caller-owned stream and returns a report.
    /// 將 PDF 匯出至呼叫端擁有的資料流並回傳報告。
    /// </summary>
    public static OdfExportReport ExportToStream(TextDocument document, Stream destination)
    {
        long start = destination.CanSeek ? destination.Length : 0;
        Export(document, destination);
        long written = destination.CanSeek ? destination.Length - start : 0;
        return new OdfExportReport(OdfExportFormat.Pdf, "managed-pdf") { BytesWritten = written };
    }

    /// <summary>
    /// Exports PDF to a file path and returns a report.
    /// 將 PDF 匯出至檔案路徑並回傳報告。
    /// </summary>
    public static OdfExportReport ExportToPath(TextDocument document, string path)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(document, nameof(document));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(null, nameof(path));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string temporaryPath = OdfAtomicFile.CreateTemporaryPath(path);
        try
        {
            OdfExportReport report;
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                report = ExportToStream(document, stream);
            }

            OdfAtomicFile.Publish(temporaryPath, Path.GetFullPath(path));
            return report;
        }
        finally
        {
            OdfAtomicFile.TryDelete(temporaryPath);
        }
    }

    /// <summary>
    /// Renders PDF and asynchronously copies it to a caller-owned stream.
    /// 產生 PDF，並非同步複製至呼叫端擁有的資料流。
    /// </summary>
    public static async Task<OdfExportReport> ExportToStreamAsync(TextDocument document, Stream destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long start = destination.CanSeek ? destination.Length : 0;
        await Task.Run(() => Export(document, destination), cancellationToken).ConfigureAwait(false);
        long written = destination.CanSeek ? destination.Length - start : 0;
        return new OdfExportReport(OdfExportFormat.Pdf, "managed-pdf") { BytesWritten = written };
    }

    /// <summary>
    /// Renders PDF and asynchronously writes it to a file path.
    /// 產生 PDF，並非同步寫入檔案路徑。
    /// </summary>
    public static async Task<OdfExportReport> ExportToPathAsync(TextDocument document, string path, CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(document, nameof(document));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(null, nameof(path));
        cancellationToken.ThrowIfCancellationRequested();
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string temporaryPath = OdfAtomicFile.CreateTemporaryPath(path);
        try
        {
            OdfExportReport report;
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                report = await ExportToStreamAsync(document, stream, cancellationToken).ConfigureAwait(false);
            }

            OdfAtomicFile.Publish(temporaryPath, Path.GetFullPath(path));
            return report;
        }
        finally
        {
            OdfAtomicFile.TryDelete(temporaryPath);
        }
    }

    /// <summary>
    /// Exports the specified ODF document to PDF.
    /// 將 ODT 文字文件轉換並寫入 PDF 資料流。
    /// </summary>
    /// <param name="document">The source or target object. / 來源文字文件</param>
    /// <param name="pdfStream">The source or target object. / 要寫入 PDF 的目標資料流</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當任一必要參數為 null 時拋出</exception>
    public static void Export(TextDocument document, Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pdfStream);

        byte[] pdf = Render(document);
        pdfStream.Write(pdf, 0, pdf.Length);
    }

    private const int MaxRepeatedRowsAndColumns = 100;
    private const int MaxListDepth = 8;
    private const double BodySize = 10;
    private const double NoteSize = 8;
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;

    private static byte[] Render(TextDocument odfDoc)
    {
        // 文字分段沿用文件的字型情境（而非固定 Default），與 ODF 文字入口一致；
        // 未設定時 TextDocument.FontContext 即為 OdfFontContext.Default。
        OdfFontContext fontContext = odfDoc.FontContext;

        // 依作業系統決定預設的中文字型名稱，避免中文在匯出 PDF 時因為預設字型不支援而顯示為方塊字。
        string defaultFont = "DFKai-SB";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            defaultFont = "PingFang TC";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            defaultFont = "Noto Sans CJK TC";
        }

        (double pageWidth, double pageHeight) = ReadPageSize(odfDoc);
        double margin = 2.5 * PdfLayoutEngine.PointsPerCentimeter;
        var fonts = new PdfFontProvider(fontContext);
        var images = new PdfImageTable();
        var engine = new PdfLayoutEngine(fonts, defaultFont, images, pageWidth, pageHeight, margin);
        var context = new ExportContext(odfDoc, defaultFont, fontContext, images, engine.ContentWidth, pageHeight - (2 * margin));

        var content = new PdfContent();
        var rootSink = new ContentSink(content);
        ConvertBlocks(odfDoc.BodyTextRoot, rootSink, context, 0);
        EmitNotes(context.Endnotes, rootSink, context, 12);
        engine.Layout(content);
        return WriteDocument(odfDoc, engine, fonts, images);
    }

    private static byte[] WriteDocument(TextDocument odfDoc, PdfLayoutEngine engine, PdfFontProvider fonts, PdfImageTable images)
    {
        var builder = new PdfBuilder();
        int pagesObject = builder.Reserve();
        var fontObjects = new Dictionary<PdfFontResource, int>();
        foreach (PdfFontResource font in fonts.All)
        {
            if (engine.Pages.Any(page => page.Fonts.Contains(font)))
            {
                fontObjects[font] = font.Write(builder);
            }
        }

        var imageObjects = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((PdfImage image, string name) in images.All)
        {
            if (engine.Pages.Any(page => page.ImageNames.Contains(name)))
            {
                imageObjects[name] = image.Write(builder);
            }
        }

        var pageObjects = new List<int>();
        foreach (PdfPageBuilder page in engine.Pages)
        {
            int contentObject = builder.AddStream(string.Empty, Encoding.ASCII.GetBytes(page.Content), compress: true);
            var annotations = new StringBuilder();
            foreach ((double x1, double y1, double x2, double y2, string uri) in page.Links)
            {
                int annotation = builder.Add(
                    "<< /Type /Annot /Subtype /Link /Rect [" + PdfCore.Number(x1) + " " + PdfCore.Number(y1) + " "
                    + PdfCore.Number(x2) + " " + PdfCore.Number(y2) + "] /Border [0 0 0] /A << /S /URI /URI ("
                    + EscapeUri(PdfLayoutEngine.BuildUri(uri)) + ") >> >>");
                annotations.Append(PdfBuilder.Ref(annotation)).Append(' ');
            }

            var resources = new StringBuilder("<< ");
            if (page.Fonts.Count > 0)
            {
                resources.Append("/Font << ");
                foreach (PdfFontResource font in page.Fonts)
                {
                    resources.Append('/').Append(font.ResourceName).Append(' ').Append(PdfBuilder.Ref(fontObjects[font])).Append(' ');
                }

                resources.Append(">> ");
            }

            if (page.ImageNames.Count > 0)
            {
                resources.Append("/XObject << ");
                foreach (string name in page.ImageNames)
                {
                    resources.Append('/').Append(name).Append(' ').Append(PdfBuilder.Ref(imageObjects[name])).Append(' ');
                }

                resources.Append(">> ");
            }

            resources.Append(">>");
            pageObjects.Add(builder.Add(
                "<< /Type /Page /Parent " + PdfBuilder.Ref(pagesObject) + " /MediaBox [0 0 " + PdfCore.Number(page.Width) + " "
                + PdfCore.Number(page.Height) + "] /Resources " + resources + " /Contents " + PdfBuilder.Ref(contentObject)
                + (annotations.Length > 0 ? " /Annots [" + annotations + "]" : string.Empty) + " >>"));
        }

        var kids = new StringBuilder();
        foreach (int pageObject in pageObjects)
        {
            kids.Append(PdfBuilder.Ref(pageObject)).Append(' ');
        }

        builder.Set(pagesObject, "<< /Type /Pages /Kids [" + kids + "] /Count " + pageObjects.Count.ToString(CultureInfo.InvariantCulture) + " >>");
        int catalog = builder.Add("<< /Type /Catalog /Pages " + PdfBuilder.Ref(pagesObject) + " >>");

        var info = new StringBuilder("<< /Producer (OdfKit) /Creator (OdfKit) /CreationDate (" + PdfCore.Date(DateTime.UtcNow) + ")");
        string? title = odfDoc.Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            info.Append(" /Title ").Append(PdfCore.TextString(title!));
        }

        info.Append(" >>");
        int infoObject = builder.Add(info.ToString());

        byte[] id;
#if NETSTANDARD2_0
        using (var random = RandomNumberGenerator.Create())
        {
            id = new byte[16];
            random.GetBytes(id);
        }
#else
        id = RandomNumberGenerator.GetBytes(16);
#endif
        using var output = new MemoryStream();
        builder.Write(output, catalog, infoObject, id);
        return output.ToArray();
    }

    private static string EscapeUri(string uri)
    {
        var sb = new StringBuilder(uri.Length);
        foreach (char c in uri)
        {
            if (c == '\\' || c == '(' || c == ')')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private sealed class ExportContext(
        TextDocument document,
        string defaultFont,
        OdfFontContext fontContext,
        PdfImageTable images,
        double contentWidth,
        double contentHeight)
    {
        internal TextDocument Document { get; } = document;

        internal string DefaultFont { get; } = defaultFont;

        internal OdfFontContext FontContext { get; } = fontContext;

        internal PdfImageTable Images { get; } = images;

        internal double ContentWidth { get; } = contentWidth;

        internal double ContentHeight { get; } = contentHeight;

        internal List<PendingNote> PendingFootnotes { get; } = [];

        internal List<PendingNote> Endnotes { get; } = [];

        internal bool InNote { get; set; }

        internal int NextNoteNumber { get; set; } = 1;
    }

    private sealed record PendingNote(string Label, OdfNode Body);

    // 頁面大小沿用 ODT 的預設頁面設定。
    private static (double Width, double Height) ReadPageSize(TextDocument document)
    {
        try
        {
            OdfPageSetup setup = document.GetDefaultPageSetup();
            double width = setup.PageWidth;
            double height = setup.PageHeight;
            if (width > 5 && height > 5)
            {
                return (width * PdfLayoutEngine.PointsPerCentimeter, height * PdfLayoutEngine.PointsPerCentimeter);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException)
        {
            // 讀不到頁面設定時維持預設的 A4。
        }

        return (A4Width, A4Height);
    }

    // ---------- 區塊容器 ----------

    // 內容區與表格儲存格都能新增段落；只有內容區能新增表格（儲存格內的巢狀表格改以段落輸出）。
    private abstract class BlockSink
    {
        internal abstract ParagraphBlock AddParagraph(RunStyle style);

        internal abstract TableBlock? AddTable();
    }

    private sealed class ContentSink(PdfContent content) : BlockSink
    {
        internal override ParagraphBlock AddParagraph(RunStyle style)
        {
            var paragraph = new ParagraphBlock(style);
            content.Blocks.Add(paragraph);
            return paragraph;
        }

        internal override TableBlock? AddTable()
        {
            var table = new TableBlock();
            content.Blocks.Add(table);
            return table;
        }
    }

    private sealed class CellSink(TableCellModel cell) : BlockSink
    {
        internal override ParagraphBlock AddParagraph(RunStyle style)
        {
            var paragraph = new ParagraphBlock(style);
            cell.Blocks.Add(paragraph);
            return paragraph;
        }

        internal override TableBlock? AddTable() => null;
    }

    private static RunStyle NormalStyle() => new(null, BodySize, false, false, false, null, null);

    private static void ConvertBlocks(OdfNode parent, BlockSink sink, ExportContext context, int listDepth)
    {
        foreach (OdfNode node in parent.Children)
        {
            ConvertBlock(node, sink, context, listDepth);
        }
    }

    private static void ConvertBlock(OdfNode node, BlockSink sink, ExportContext context, int listDepth)
    {
        if (node.NodeType != OdfNodeType.Element)
        {
            return;
        }

        if (node.NamespaceUri == OdfNamespaces.Table)
        {
            if (node.LocalName == "table")
            {
                ConvertTable(node, sink, context);
            }

            return;
        }

        if (node.NamespaceUri != OdfNamespaces.Text)
        {
            return;
        }

        switch (node.LocalName)
        {
            case "h":
                {
                    int level = int.TryParse(node.GetAttribute("outline-level", OdfNamespaces.Text), out int l) ? l : 1;
                    level = level < 1 ? 1 : (level > 6 ? 6 : level);
                    (double size, bool italic, double before, double after) = level switch
                    {
                        1 => (16.0, false, 12.0, 6.0),
                        2 => (14.0, false, 6.0, 6.0),
                        3 => (13.0, false, 6.0, 3.0),
                        4 => (12.0, false, 6.0, 3.0),
                        5 => (11.0, false, 6.0, 3.0),
                        _ => (10.0, true, 6.0, 3.0),
                    };
                    ParagraphBlock para = sink.AddParagraph(new RunStyle(null, size, true, italic, false, null, null));
                    para.SpaceBefore = before;
                    para.SpaceAfter = after;
                    para.KeepWithNext = true;
                    ConvertParagraphContent(node, para, para.BaseStyle, context);
                    break;
                }

            case "p":
                {
                    RunStyle style = ApplyParagraphTextStyle(context.Document, node, NormalStyle());
                    ParagraphBlock para = sink.AddParagraph(style);
                    para.Align = ReadAlignment(context.Document, node);
                    ConvertParagraphContent(node, para, style, context);
                    break;
                }

            case "list":
                ConvertList(node, sink, context, listDepth, null);
                break;
            case "section":
            case "table-of-content":
            case "alphabetical-index":
            case "illustration-index":
            case "table-index":
            case "user-index":
            case "bibliography":
            case "index-body":
            case "index-title":
                ConvertBlocks(node, sink, context, listDepth);
                break;
        }

        EmitNotes(context.PendingFootnotes, sink, context, 2);
    }

    // 腳註接在引用它的區塊後面，以小字輸出；章節附註累積到文件結尾。
    private static void EmitNotes(List<PendingNote> notes, BlockSink sink, ExportContext context, double spaceBefore)
    {
        if (notes.Count == 0)
        {
            return;
        }

        context.InNote = true;
        try
        {
            bool first = true;
            foreach (PendingNote note in notes)
            {
                var style = new RunStyle(null, NoteSize, false, false, false, null, null);
                ParagraphBlock para = sink.AddParagraph(style);
                para.SpaceBefore = first ? spaceBefore : 0;
                para.SpaceAfter = 2;
                para.LeftIndent = 0;
                AddSegmentedText(para, note.Label + " ", style, context);
                foreach (OdfNode child in note.Body.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Text
                        && (child.LocalName == "p" || child.LocalName == "h"))
                    {
                        if (!ReferenceEquals(child, FirstParagraph(note.Body)))
                        {
                            AddSegmentedText(para, " ", style, context);
                        }

                        ConvertParagraphContent(child, para, style, context);
                    }
                }

                first = false;
            }
        }
        finally
        {
            context.InNote = false;
            notes.Clear();
        }
    }

    private static OdfNode? FirstParagraph(OdfNode body)
    {
        foreach (OdfNode child in body.Children)
        {
            if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Text
                && (child.LocalName == "p" || child.LocalName == "h"))
            {
                return child;
            }
        }

        return null;
    }

    private static void AddNote(OdfNode note, ParagraphBlock paragraph, RunStyle style, ExportContext context)
    {
        if (context.InNote)
        {
            return;
        }

        OdfNode? citation = null;
        OdfNode? body = null;
        foreach (OdfNode child in note.Children)
        {
            if (child.NodeType != OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Text)
            {
                continue;
            }

            if (child.LocalName == "note-citation")
            {
                citation = child;
            }
            else if (child.LocalName == "note-body")
            {
                body = child;
            }
        }

        if (body is null)
        {
            return;
        }

        string label = (citation?.TextContent ?? string.Empty).Trim();
        if (label.Length == 0)
        {
            label = context.NextNoteNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        context.NextNoteNumber++;
        RunStyle small = style.With(false, false, false, null, Math.Max(6, style.Size * 0.7));
        AddSegmentedText(paragraph, label, small, context);
        bool endnote = string.Equals(note.GetAttribute("note-class", OdfNamespaces.Text), "endnote", StringComparison.Ordinal);
        (endnote ? context.Endnotes : context.PendingFootnotes).Add(new PendingNote(label, body));
    }

    private static PdfAlign ReadAlignment(TextDocument document, OdfNode node)
    {
        string? styleName = node.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return PdfAlign.Left;
        }

        string? align = document.StyleEngine.GetStyleProperty(styleName!, "text-align", OdfNamespaces.Fo, "paragraph");
        return align switch
        {
            "center" => PdfAlign.Center,
            "end" or "right" => PdfAlign.Right,
            "justify" => PdfAlign.Justify,
            _ => PdfAlign.Left,
        };
    }

    // 段落樣式上的字元屬性（大小、粗體、斜體、顏色）作為段落預設。
    private static RunStyle ApplyParagraphTextStyle(TextDocument document, OdfNode node, RunStyle style)
    {
        string? styleName = node.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return style;
        }

        return ApplyTextProperties(document, styleName!, style);
    }

    // ---------- 清單 ----------

    private static void ConvertList(OdfNode list, BlockSink sink, ExportContext context, int level, string? inheritedStyleName)
    {
        if (level > MaxListDepth)
        {
            return;
        }

        string? styleName = list.GetAttribute("style-name", OdfNamespaces.Text) ?? inheritedStyleName;
        OdfNode? levelStyle = FindListLevelStyle(context.Document, styleName, level + 1);
        bool ordered = levelStyle is not null && levelStyle.LocalName == "list-level-style-number";
        int counter = 0;
        if (ordered && int.TryParse(levelStyle!.GetAttribute("start-value", OdfNamespaces.Text), out int start) && start > 0)
        {
            counter = start - 1;
        }

        foreach (OdfNode item in list.Children)
        {
            if (item.NamespaceUri != OdfNamespaces.Text || (item.LocalName != "list-item" && item.LocalName != "list-header"))
            {
                continue;
            }

            bool isHeader = item.LocalName == "list-header";
            bool firstParagraph = true;
            if (!isHeader)
            {
                counter++;
            }

            foreach (OdfNode content in item.Children)
            {
                if (content.NodeType != OdfNodeType.Element)
                {
                    continue;
                }

                if (content.NamespaceUri == OdfNamespaces.Text && content.LocalName == "list")
                {
                    ConvertList(content, sink, context, level + 1, styleName);
                }
                else if (content.NamespaceUri == OdfNamespaces.Text && (content.LocalName == "p" || content.LocalName == "h"))
                {
                    RunStyle style = ApplyParagraphTextStyle(context.Document, content, NormalStyle());
                    ParagraphBlock para = sink.AddParagraph(style);
                    para.Align = ReadAlignment(context.Document, content);
                    para.LeftIndent = 0.9 * (level + 1) * PdfLayoutEngine.PointsPerCentimeter;
                    para.FirstLineIndent = -0.6 * PdfLayoutEngine.PointsPerCentimeter;
                    if (firstParagraph && !isHeader)
                    {
                        para.Inlines.Add(new TextInline(BuildListLabel(levelStyle, ordered, counter) + " ", style, true));
                    }

                    ConvertParagraphContent(content, para, style, context);
                    firstParagraph = false;
                }
                else
                {
                    ConvertBlock(content, sink, context, level + 1);
                }
            }
        }
    }

    private static string BuildListLabel(OdfNode? levelStyle, bool ordered, int number)
    {
        if (!ordered)
        {
            string? bullet = levelStyle?.GetAttribute("bullet-char", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(bullet) || bullet![0] is >= '' and <= '')
            {
                return "•";
            }

            return bullet.Substring(0, 1);
        }

        string format = levelStyle!.GetAttribute("num-format", OdfNamespaces.Style) ?? "1";
        string prefix = levelStyle.GetAttribute("num-prefix", OdfNamespaces.Style) ?? string.Empty;
        string suffix = levelStyle.GetAttribute("num-suffix", OdfNamespaces.Style) ?? ".";
        string text = format switch
        {
            "a" => ToLetters(number, upper: false),
            "A" => ToLetters(number, upper: true),
            "i" => ToRoman(number).ToLowerInvariant(),
            "I" => ToRoman(number),
            _ => number.ToString(CultureInfo.InvariantCulture),
        };
        return prefix + text + suffix;
    }

    private static string ToLetters(int number, bool upper)
    {
        if (number < 1)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (int value = number; value > 0; value = (value - 1) / 26)
        {
            builder.Insert(0, (char)((upper ? 'A' : 'a') + ((value - 1) % 26)));
        }

        return builder.ToString();
    }

    private static string ToRoman(int number)
    {
        if (number < 1 || number > 3999)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        (int Value, string Symbol)[] map =
        [
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        ];
        var builder = new StringBuilder();
        int remaining = number;
        foreach ((int value, string symbol) in map)
        {
            while (remaining >= value)
            {
                builder.Append(symbol);
                remaining -= value;
            }
        }

        return builder.ToString();
    }

    private static OdfNode? FindListLevelStyle(TextDocument document, string? styleName, int level)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return null;
        }

        string wanted = level.ToString(CultureInfo.InvariantCulture);
        foreach (OdfNode root in new[] { document.ContentDom, document.StylesDom })
        {
            foreach (OdfNode section in root.Children)
            {
                if (section.NamespaceUri != OdfNamespaces.Office
                    || (section.LocalName != "automatic-styles" && section.LocalName != "styles"))
                {
                    continue;
                }

                foreach (OdfNode listStyle in section.Children)
                {
                    if (listStyle.NamespaceUri != OdfNamespaces.Text
                        || listStyle.LocalName != "list-style"
                        || listStyle.GetAttribute("name", OdfNamespaces.Style) != styleName)
                    {
                        continue;
                    }

                    foreach (OdfNode levelStyle in listStyle.Children)
                    {
                        if (levelStyle.LocalName.StartsWith("list-level-style-", StringComparison.Ordinal)
                            && levelStyle.GetAttribute("level", OdfNamespaces.Text) == wanted)
                        {
                            return levelStyle;
                        }
                    }
                }
            }
        }

        return null;
    }

    // ---------- 表格 ----------

    /// <summary>
    /// 轉換 <c>table:table</c>：欄寬平分頁面可用寬度，合併儲存格記錄欄／列跨度，
    /// 標題列重複於每頁，重複列有上限（試算表風格的尾端空列不展開）。
    /// </summary>
    private static void ConvertTable(OdfNode tableNode, BlockSink sink, ExportContext context)
    {
        TableBlock? table = sink.AddTable();
        if (table is null)
        {
            // 儲存格內的巢狀表格：以段落輸出其儲存格內容，文字不遺失。
            foreach (OdfNode row in EnumerateRows(tableNode, out _))
            {
                foreach (OdfNode cell in row.Children)
                {
                    if (cell.NamespaceUri == OdfNamespaces.Table && cell.LocalName == "table-cell")
                    {
                        ConvertBlocks(cell, sink, context, 0);
                    }
                }
            }

            return;
        }

        List<OdfNode> rows = EnumerateRows(tableNode, out HashSet<OdfNode> headerRows);
        int columns = CountColumns(tableNode, rows);
        if (columns == 0 || rows.Count == 0)
        {
            return;
        }

        table.Columns = columns;
        foreach (OdfNode rowNode in rows)
        {
            int rowRepeat = ReadRepeat(rowNode, "number-rows-repeated");
            bool empty = !rowNode.Children.Any(cell => cell.NamespaceUri == OdfNamespaces.Table
                && cell.LocalName == "table-cell"
                && HasContent(cell));
            if (empty && rowRepeat > 1)
            {
                continue;
            }

            for (int copy = 0; copy < rowRepeat; copy++)
            {
                var row = new TableRowModel { IsHeader = headerRows.Contains(rowNode) };
                table.Rows.Add(row);
                int column = 0;
                foreach (OdfNode cellNode in rowNode.Children)
                {
                    if (cellNode.NamespaceUri != OdfNamespaces.Table)
                    {
                        continue;
                    }

                    int columnRepeat = cellNode.LocalName == "table-cell" || cellNode.LocalName == "covered-table-cell"
                        ? ReadRepeat(cellNode, "number-columns-repeated")
                        : 0;
                    for (int repeatIndex = 0; repeatIndex < columnRepeat && column < columns; repeatIndex++, column++)
                    {
                        if (cellNode.LocalName != "table-cell")
                        {
                            continue;
                        }

                        var cell = new TableCellModel { Column = column };
                        if (int.TryParse(cellNode.GetAttribute("number-columns-spanned", OdfNamespaces.Table), out int colSpan) && colSpan > 1)
                        {
                            cell.ColumnSpan = Math.Min(colSpan, columns - column);
                        }

                        if (int.TryParse(cellNode.GetAttribute("number-rows-spanned", OdfNamespaces.Table), out int rowSpan) && rowSpan > 1)
                        {
                            cell.RowSpan = Math.Min(rowSpan, MaxRepeatedRowsAndColumns);
                        }

                        row.Cells.Add(cell);
                        ConvertBlocks(cellNode, new CellSink(cell), context, 0);
                    }
                }
            }
        }
    }

    private static bool HasContent(OdfNode cell) =>
        cell.Children.Any(child => child.NodeType == OdfNodeType.Element) || !string.IsNullOrEmpty(cell.TextContent);

    private static int ReadRepeat(OdfNode node, string attribute)
    {
        string? text = node.GetAttribute(attribute, OdfNamespaces.Table);
        return int.TryParse(text, out int value) && value > 1 ? Math.Min(value, MaxRepeatedRowsAndColumns) : 1;
    }

    private static List<OdfNode> EnumerateRows(OdfNode table, out HashSet<OdfNode> headerRows)
    {
        var rows = new List<OdfNode>();
        headerRows = [];
        foreach (OdfNode part in table.Children)
        {
            if (part.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            if (part.LocalName == "table-row")
            {
                rows.Add(part);
            }
            else if (part.LocalName == "table-header-rows" || part.LocalName == "table-rows" || part.LocalName == "table-row-group")
            {
                foreach (OdfNode row in part.Children)
                {
                    if (row.NamespaceUri == OdfNamespaces.Table && row.LocalName == "table-row")
                    {
                        rows.Add(row);
                        if (part.LocalName == "table-header-rows")
                        {
                            headerRows.Add(row);
                        }
                    }
                }
            }
        }

        return rows;
    }

    private static int CountColumns(OdfNode table, List<OdfNode> rows)
    {
        int declared = 0;
        foreach (OdfNode child in table.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Table && child.LocalName == "table-column")
            {
                declared += ReadRepeat(child, "number-columns-repeated");
            }
        }

        int measured = 0;
        foreach (OdfNode row in rows)
        {
            int count = 0;
            foreach (OdfNode cell in row.Children)
            {
                if (cell.NamespaceUri == OdfNamespaces.Table
                    && (cell.LocalName == "table-cell" || cell.LocalName == "covered-table-cell"))
                {
                    count += ReadRepeat(cell, "number-columns-repeated");
                }
            }

            measured = Math.Max(measured, count);
        }

        return Math.Min(Math.Max(declared, measured), MaxRepeatedRowsAndColumns);
    }

    // ---------- 行內內容 ----------

    private static void ConvertParagraphContent(OdfNode odfPara, ParagraphBlock paragraph, RunStyle style, ExportContext context)
    {
        bool hasChildren = false;
        foreach (OdfNode child in odfPara.Children)
        {
            hasChildren = true;
            ProcessInline(child, paragraph, style, context);
        }

        if (!hasChildren)
        {
            AddSegmentedText(paragraph, odfPara.TextContent ?? string.Empty, style, context);
        }
    }

    private static void ProcessInline(OdfNode child, ParagraphBlock paragraph, RunStyle style, ExportContext context)
    {
        if (child.NodeType == OdfNodeType.Text)
        {
            AddSegmentedText(paragraph, child.TextContent ?? string.Empty, style, context);
            return;
        }

        if (child.NodeType != OdfNodeType.Element)
        {
            return;
        }

        if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "frame")
        {
            AddFrameImage(child, paragraph, style, context);
            return;
        }

        if (child.NamespaceUri != OdfNamespaces.Text)
        {
            return;
        }

        switch (child.LocalName)
        {
            case "span":
                {
                    RunStyle spanStyle = ApplySpanStyle(context.Document, child, style);
                    foreach (OdfNode grandChild in child.Children)
                    {
                        ProcessInline(grandChild, paragraph, spanStyle, context);
                    }

                    break;
                }

            case "a":
                {
                    string? href = child.GetAttribute("href", OdfNamespaces.XLink);
                    RunStyle target = IsSafeHref(href) ? style.WithLink(href!.Trim()) : style;
                    foreach (OdfNode grandChild in child.Children)
                    {
                        ProcessInline(grandChild, paragraph, target, context);
                    }

                    break;
                }

            case "s":
                {
                    int count = 1;
                    string? cStr = child.GetAttribute("c", OdfNamespaces.Text);
                    if (!string.IsNullOrEmpty(cStr) && int.TryParse(cStr, out int c) && c > 0)
                    {
                        count = Math.Min(c, 4096); // 防禦性上限：PDF 中不需超過 4096 個連續空白
                    }

                    paragraph.Inlines.Add(new TextInline(new string(' ', count), style, true));
                    break;
                }

            case "tab":
                paragraph.Inlines.Add(new TabInline(style));
                break;

            case "line-break":
                paragraph.Inlines.Add(new BreakInline(style));
                break;

            case "note":
                AddNote(child, paragraph, style, context);
                break;

            case "bookmark-ref":
            case "page-number":
            case "page-count":
            case "date":
            case "time":
            case "title":
            case "subject":
            case "initial-creator":
            case "file-name":
            case "word-count":
            case "character-count":
                // 欄位以儲存的顯示文字輸出。
                AddSegmentedText(paragraph, child.TextContent ?? string.Empty, style, context);
                break;
        }
    }

    // 只允許網頁連結、郵件、電話與 FTP；其他協定（例如 javascript:）只保留連結文字。
    private static bool IsSafeHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        string text = href!.Trim();
        int colon = text.IndexOf(':');
        if (colon < 0 || text[0] == '#')
        {
            // 相對位址與頁內錨點在 PDF 裡沒有意義，只保留連結文字。
            return false;
        }

        string scheme = text.Substring(0, colon);
        return scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("tel", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase);
    }

    // 字元樣式：粗體、斜體、底線、顏色與大小；亞洲字型的粗體與斜體屬性當作後備。
    private static RunStyle ApplySpanStyle(TextDocument document, OdfNode span, RunStyle style)
    {
        string? styleName = span.GetAttribute("style-name", OdfNamespaces.Text);
        return string.IsNullOrWhiteSpace(styleName) ? style : ApplyTextProperties(document, styleName!, style);
    }

    private static RunStyle ApplyTextProperties(TextDocument document, string styleName, RunStyle style)
    {
        string? weight = document.StyleEngine.GetStyleProperty(styleName, "font-weight", OdfNamespaces.Fo, "text")
            ?? document.StyleEngine.GetStyleProperty(styleName, "font-weight-asian", OdfNamespaces.Style, "text");
        string? fontStyle = document.StyleEngine.GetStyleProperty(styleName, "font-style", OdfNamespaces.Fo, "text")
            ?? document.StyleEngine.GetStyleProperty(styleName, "font-style-asian", OdfNamespaces.Style, "text");
        string? underline = document.StyleEngine.GetStyleProperty(styleName, "text-underline-style", OdfNamespaces.Style, "text");
        string? colorText = document.StyleEngine.GetStyleProperty(styleName, "color", OdfNamespaces.Fo, "text");
        string? sizeText = document.StyleEngine.GetStyleProperty(styleName, "font-size", OdfNamespaces.Fo, "text");

        bool bold = string.Equals(weight, "bold", StringComparison.OrdinalIgnoreCase);
        bool italic = string.Equals(fontStyle, "italic", StringComparison.OrdinalIgnoreCase);
        bool hasUnderline = !string.IsNullOrWhiteSpace(underline) && !string.Equals(underline, "none", StringComparison.OrdinalIgnoreCase);
        return style.With(bold, italic, hasUnderline, ParseHexColor(colorText), ParsePoints(sizeText));
    }

    private static PdfColor? ParseHexColor(string? text)
    {
        if (string.IsNullOrEmpty(text) || text![0] != '#' || text.Length != 7)
        {
            return null;
        }

        return TryParseHex(text, out int rgb)
            ? new PdfColor(((rgb >> 16) & 0xFF) / 255.0, ((rgb >> 8) & 0xFF) / 255.0, (rgb & 0xFF) / 255.0)
            : null;
    }

    // 長度字串去掉兩個字元的單位後解析數字；net 目標用範圍型多載避免配置子字串。
    private static bool TryParseLengthNumber(string text, out double value)
    {
#if NETSTANDARD2_0
        return double.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#else
        return double.TryParse(text.AsSpan(0, text.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#endif
    }

    private static bool TryParseHex(string text, out int value)
    {
#if NETSTANDARD2_0
        return int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
#else
        return int.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
#endif
    }

    private static double? ParsePoints(string? text)
    {
        if (string.IsNullOrEmpty(text) || !text!.EndsWith("pt", StringComparison.Ordinal))
        {
            return null;
        }

        return TryParseLengthNumber(text, out double size) && size > 0
            ? size
            : null;
    }

    // 圖片內嵌；讀不到、格式不支援或過大時略過，不中斷整份匯出。
    private static void AddFrameImage(OdfNode frame, ParagraphBlock paragraph, RunStyle style, ExportContext context)
    {
        OdfNode? image = frame.Children.FirstOrDefault(
            child => child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "image");
        string? href = image?.GetAttribute("href", OdfNamespaces.XLink);
        if (string.IsNullOrEmpty(href) || href!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using Stream stream = context.Document.Package.GetEntryStream(href.TrimStart('.', '/'));
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            if (buffer.Length == 0 || buffer.Length > 5 * 1024 * 1024)
            {
                return;
            }

            byte[] bytes = buffer.ToArray();
            PdfImage? decoded = PdfImage.TryLoad(bytes);
            if (decoded is null)
            {
                return;
            }

            (PdfImage _, string name) = context.Images.Add(bytes, decoded);
            double? width = ReadCentimeters(frame.GetAttribute("width", OdfNamespaces.Svg));
            double? height = ReadCentimeters(frame.GetAttribute("height", OdfNamespaces.Svg));
            double widthPoints = width is double w ? w * PdfLayoutEngine.PointsPerCentimeter : decoded.Width * 0.75;
            double heightPoints = height is double h && width is not null
                ? h * PdfLayoutEngine.PointsPerCentimeter
                : widthPoints * decoded.Height / decoded.Width;
            double scale = Math.Min(1.0, Math.Min(context.ContentWidth / widthPoints, context.ContentHeight / heightPoints));
            paragraph.Inlines.Add(new ImageInline(name, widthPoints * scale, heightPoints * scale, style.Link));
        }
        catch (Exception ex) when (ex is IOException or KeyNotFoundException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // 圖片格式不支援或讀取失敗：略過這張圖片。
        }
    }

    private static double? ReadCentimeters(string? text)
    {
        if (string.IsNullOrEmpty(text) || !text!.EndsWith("cm", StringComparison.Ordinal))
        {
            return null;
        }

        return TryParseLengthNumber(text, out double value) && value > 0 ? value : null;
    }

    private static void AddSegmentedText(ParagraphBlock paragraph, string text, RunStyle style, ExportContext context)
    {
        var segments = context.FontContext.SegmentText(text, context.DefaultFont);
        foreach (var (segText, fontName) in segments)
        {
            if (fontName == context.DefaultFont)
            {
                paragraph.Inlines.Add(new TextInline(segText, style, false));
            }
            else
            {
                context.FontContext.WarnIfUnresolvable(fontName, "CNS 11643 高位字面文字 PDF 匯出");
                paragraph.Inlines.Add(new TextInline(segText, style.WithFont(fontName), false));
            }
        }
    }
}

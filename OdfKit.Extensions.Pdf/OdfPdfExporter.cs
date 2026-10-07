using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using PdfSharp.Fonts;

#if NETSTANDARD2_0
using ArgumentNullException = OdfKit.Export.Shim.ArgumentNullException;
#endif

using OdfKit.Compliance;
namespace OdfKit.Export;

/// <summary>
/// Exports ODF documents to PDF.
/// 將 TextDocument 匯出為 PDF 的工具類別。
/// </summary>
public static class OdfPdfExporter
{
    /// <summary>
    /// Exports PDF to a caller-owned stream and returns a report.
    /// 將 PDF 匯出至呼叫端擁有的資料流並回傳報告。
    /// </summary>
    public static OdfExportReport ExportToStream(TextDocument document, Stream destination)
    {
        // 以 Length（而非 Position）計算寫入位元組數：PDFsharp 的 PdfDocument.Save 在寫完後
        // 可能將資料流游標移回起點（方便呼叫端立即讀回），若改用 Position 相減會誤算為 0。
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

        // 直接對目的地資料流序列化（與同步版 ExportToStream 相同路徑），省去先寫入中繼
        // MemoryStream 再整體 CopyToAsync 的雙重緩衝。PDFsharp 的 PdfDocument.Save 本身
        // 仍需先解析完整物件圖／xref 表才能序列化（第三方庫的整體序列化限制，無法避免），
        // 此處僅消除 OdfKit 自行外加的第二層緩衝。
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

    static OdfPdfExporter()
    {
        try
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new OdfPdfFontResolver();
            }
        }
        catch
        {
            // 若已註冊則忽略。
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

        var migraDoc = BuildMigraDoc(document);
        var renderer = new PdfDocumentRenderer { Document = migraDoc };
        renderer.RenderDocument();
        using (renderer.PdfDocument)
        {
            renderer.PdfDocument.Save(pdfStream);
        }
    }

    private const int MaxRepeatedRowsAndColumns = 100;
    private const int MaxListDepth = 8;

    private static Document BuildMigraDoc(TextDocument odfDoc)
    {
        var doc = new Document();

        // 文字分段沿用文件的字型情境（而非固定 Default），與 ODF 文字入口一致；
        // 未設定時 TextDocument.FontContext 即為 OdfFontContext.Default。
        OdfFontContext fontContext = odfDoc.FontContext;

        // 依作業系統決定預設的中文字型名稱，以防止中文在導出 PDF 時因為 Arial 字型不支援而顯示為方塊字
        // 在 Windows 平台上使用標楷體（DFKai-SB），因為它是標準 .ttf 檔，能避開 PDFsharp 無法直接解析 .ttc（如微軟正黑體）的 NullReferenceException 限制
        string defaultChineseFont = "DFKai-SB";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            defaultChineseFont = "PingFang TC";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            defaultChineseFont = "Noto Sans CJK TC";
        }

        // 套用中文字型至預設樣式
        foreach (string name in new[] { "Normal", "Heading1", "Heading2", "Heading3", "Heading4", "Heading5", "Heading6" })
        {
            if (doc.Styles[name] is Style style)
            {
                style.Font.Name = defaultChineseFont;
            }
        }

        var section = doc.AddSection();
        ApplyPageSize(odfDoc, section);

        var context = new ExportContext(odfDoc, defaultChineseFont, fontContext);
        ConvertBlocks(odfDoc.BodyTextRoot, new SectionSink(section), context, 0);
        return doc;
    }

    private sealed class ExportContext(TextDocument document, string defaultFont, OdfFontContext fontContext)
    {
        internal TextDocument Document { get; } = document;

        internal string DefaultFont { get; } = defaultFont;

        internal OdfFontContext FontContext { get; } = fontContext;

        // 頁面可用寬度（公分），表格以此平分欄寬。
        internal double TextWidthCm { get; set; } = 16;
    }

    // 頁面大小沿用 ODT 的預設頁面設定（邊界維持 MigraDoc 預設的 2.5 公分）。
    private static void ApplyPageSize(TextDocument document, Section section)
    {
        try
        {
            OdfPageSetup setup = document.GetDefaultPageSetup();
            double width = setup.PageWidth;
            double height = setup.PageHeight;
            if (width > 5 && height > 5)
            {
                section.PageSetup.PageWidth = Unit.FromCentimeter(width);
                section.PageSetup.PageHeight = Unit.FromCentimeter(height);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException)
        {
            // 讀不到頁面設定時維持預設的 A4。
        }
    }

    // ---------- 區塊容器 ----------

    // 區段（Section）與表格儲存格（Cell）都能新增段落；只有區段能新增表格（MigraDoc 不支援表格內再放表格）。
    private abstract class BlockSink
    {
        internal abstract Paragraph AddParagraph(string? style);

        internal abstract Table? AddTable();
    }

    private sealed class SectionSink(Section section) : BlockSink
    {
        internal override Paragraph AddParagraph(string? style) =>
            style is null ? section.AddParagraph() : section.AddParagraph(string.Empty, style);

        internal override Table? AddTable() => section.AddTable();
    }

    private sealed class CellSink(Cell cell) : BlockSink
    {
        internal override Paragraph AddParagraph(string? style) =>
            style is null ? cell.AddParagraph() : cell.AddParagraph(string.Empty);

        internal override Table? AddTable() => null;
    }

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
                    Paragraph para = sink.AddParagraph("Heading" + level.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    ConvertParagraphContent(node, new ParagraphInline(para), context);
                    break;
                }
            case "p":
                {
                    Paragraph para = sink.AddParagraph(null);
                    ApplyParagraphAlignment(context.Document, node, para);
                    ConvertParagraphContent(node, new ParagraphInline(para), context);
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
    }

    private static void ApplyParagraphAlignment(TextDocument document, OdfNode node, Paragraph paragraph)
    {
        string? styleName = node.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return;
        }

        string? align = document.StyleEngine.GetStyleProperty(styleName!, "text-align", OdfNamespaces.Fo, "paragraph");
        switch (align)
        {
            case "center":
                paragraph.Format.Alignment = ParagraphAlignment.Center;
                break;
            case "end":
            case "right":
                paragraph.Format.Alignment = ParagraphAlignment.Right;
                break;
            case "justify":
                paragraph.Format.Alignment = ParagraphAlignment.Justify;
                break;
        }
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
                    Paragraph para = sink.AddParagraph(null);
                    double indent = 0.9 * (level + 1);
                    para.Format.LeftIndent = Unit.FromCentimeter(indent);
                    para.Format.FirstLineIndent = Unit.FromCentimeter(-0.6);
                    if (firstParagraph && !isHeader)
                    {
                        para.AddText(BuildListLabel(levelStyle, ordered, counter) + " ");
                    }

                    ConvertParagraphContent(content, new ParagraphInline(para), context);
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
            _ => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return prefix + text + suffix;
    }

    private static string ToLetters(int number, bool upper)
    {
        if (number < 1)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
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
            return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        (int Value, string Symbol)[] map =
        [
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        ];
        var builder = new System.Text.StringBuilder();
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

        string wanted = level.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
    /// 轉換 <c>table:table</c>：欄寬平分頁面可用寬度，合併儲存格轉成 MigraDoc 的 <c>MergeRight</c>／<c>MergeDown</c>，
    /// 標題列重複於每頁，重複列有上限（試算表風格的尾端空列不展開）。
    /// </summary>
    private static void ConvertTable(OdfNode tableNode, BlockSink sink, ExportContext context)
    {
        Table? table = sink.AddTable();
        if (table is null)
        {
            // 儲存格內的巢狀表格：MigraDoc 不支援，以段落輸出其儲存格內容，文字不遺失。
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

        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        Unit columnWidth = Unit.FromCentimeter(Math.Max(1.0, context.TextWidthCm / columns));
        for (int index = 0; index < columns; index++)
        {
            table.AddColumn(columnWidth);
        }

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
                Row row = table.AddRow();
                row.HeadingFormat = headerRows.Contains(rowNode);
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

                        Cell cell = row.Cells[column];
                        if (int.TryParse(cellNode.GetAttribute("number-columns-spanned", OdfNamespaces.Table), out int colSpan) && colSpan > 1)
                        {
                            cell.MergeRight = Math.Min(colSpan, columns - column) - 1;
                        }

                        if (int.TryParse(cellNode.GetAttribute("number-rows-spanned", OdfNamespaces.Table), out int rowSpan) && rowSpan > 1)
                        {
                            cell.MergeDown = Math.Min(rowSpan, MaxRepeatedRowsAndColumns) - 1;
                        }

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

    // 段落、格式化文字與超連結在 MigraDoc 裡各自有相同的新增方法但沒有共同介面；以包裝類別統一，行內轉換只寫一份。
    private abstract class InlineSink
    {
        internal abstract void AddText(string text);

        internal abstract InlineSink AddFormatted();

        internal abstract InlineSink AddHyperlink(string target);

        internal abstract void AddTab();

        internal abstract void AddLineBreak();

        internal abstract void AddImage(string source, double? widthCm);

        internal virtual void SetFont(string fontName)
        {
        }

        internal virtual void ApplyStyle(bool bold, bool italic, bool underline, Color? color, double? sizePoints)
        {
        }
    }

    private sealed class ParagraphInline(Paragraph paragraph) : InlineSink
    {
        internal override void AddText(string text) => paragraph.AddText(text);

        internal override InlineSink AddFormatted() => new FormattedInline(paragraph.AddFormattedText());

        internal override InlineSink AddHyperlink(string target) =>
            new HyperlinkInline(paragraph.AddHyperlink(target, HyperlinkType.Url));

        internal override void AddTab() => paragraph.AddTab();

        internal override void AddLineBreak() => paragraph.AddLineBreak();

        internal override void AddImage(string source, double? widthCm)
        {
            MigraDoc.DocumentObjectModel.Shapes.Image image = paragraph.AddImage(source);
            image.LockAspectRatio = true;
            if (widthCm is double width)
            {
                image.Width = Unit.FromCentimeter(width);
            }
        }
    }

    private sealed class FormattedInline(FormattedText text) : InlineSink
    {
        internal override void AddText(string value) => text.AddText(value);

        internal override InlineSink AddFormatted() => new FormattedInline(text.AddFormattedText());

        internal override InlineSink AddHyperlink(string target) =>
            new HyperlinkInline(text.AddHyperlink(target, HyperlinkType.Url));

        internal override void AddTab() => text.AddTab();

        internal override void AddLineBreak() => text.AddLineBreak();

        internal override void AddImage(string source, double? widthCm)
        {
            MigraDoc.DocumentObjectModel.Shapes.Image image = text.AddImage(source);
            image.LockAspectRatio = true;
            if (widthCm is double width)
            {
                image.Width = Unit.FromCentimeter(width);
            }
        }

        internal override void SetFont(string fontName) => text.Font.Name = fontName;

        internal override void ApplyStyle(bool bold, bool italic, bool underline, Color? color, double? sizePoints)
        {
            if (bold)
            {
                text.Bold = true;
            }

            if (italic)
            {
                text.Italic = true;
            }

            if (underline)
            {
                text.Underline = Underline.Single;
            }

            if (color is Color value)
            {
                text.Color = value;
            }

            if (sizePoints is double size)
            {
                text.Size = Unit.FromPoint(size);
            }
        }
    }

    private sealed class HyperlinkInline(Hyperlink hyperlink) : InlineSink
    {
        internal override void AddText(string text) => hyperlink.AddText(text);

        internal override InlineSink AddFormatted() => new FormattedInline(hyperlink.AddFormattedText());

        internal override InlineSink AddHyperlink(string target) => this;

        internal override void AddTab() => hyperlink.AddTab();

        internal override void AddLineBreak() => hyperlink.AddText(" ");

        internal override void AddImage(string source, double? widthCm)
        {
        }
    }

    private static void ConvertParagraphContent(OdfNode odfPara, InlineSink sink, ExportContext context)
    {
        bool hasChildren = false;
        foreach (OdfNode child in odfPara.Children)
        {
            hasChildren = true;
            ProcessInline(child, sink, context);
        }

        if (!hasChildren)
        {
            AddSegmentedText(sink, odfPara.TextContent ?? string.Empty, context);
        }
    }

    private static void ProcessInline(OdfNode child, InlineSink parent, ExportContext context)
    {
        if (child.NodeType == OdfNodeType.Text)
        {
            AddSegmentedText(parent, child.TextContent ?? string.Empty, context);
            return;
        }

        if (child.NodeType != OdfNodeType.Element)
        {
            return;
        }

        if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "frame")
        {
            AddFrameImage(child, parent, context);
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
                    InlineSink formatted = parent.AddFormatted();
                    ApplySpanStyle(context.Document, child, formatted);
                    foreach (OdfNode grandChild in child.Children)
                    {
                        ProcessInline(grandChild, formatted, context);
                    }

                    break;
                }

            case "a":
                {
                    string? href = child.GetAttribute("href", OdfNamespaces.XLink);
                    InlineSink target = IsSafeHref(href) ? parent.AddHyperlink(href!) : parent;
                    foreach (OdfNode grandChild in child.Children)
                    {
                        ProcessInline(grandChild, target, context);
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

                    parent.AddText(new string(' ', count));
                    break;
                }

            case "tab":
                parent.AddTab();
                break;

            case "line-break":
                parent.AddLineBreak();
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
                AddSegmentedText(parent, child.TextContent ?? string.Empty, context);
                break;
        }
    }

    // 只允許網頁連結、郵件、電話與頁內錨點；其他協定（例如 javascript:）只保留連結文字。
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
    private static void ApplySpanStyle(TextDocument document, OdfNode span, InlineSink formatted)
    {
        string? styleName = span.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return;
        }

        string? weight = document.StyleEngine.GetStyleProperty(styleName!, "font-weight", OdfNamespaces.Fo, "text")
            ?? document.StyleEngine.GetStyleProperty(styleName!, "font-weight-asian", OdfNamespaces.Style, "text");
        string? fontStyle = document.StyleEngine.GetStyleProperty(styleName!, "font-style", OdfNamespaces.Fo, "text")
            ?? document.StyleEngine.GetStyleProperty(styleName!, "font-style-asian", OdfNamespaces.Style, "text");
        string? underline = document.StyleEngine.GetStyleProperty(styleName!, "text-underline-style", OdfNamespaces.Style, "text");
        string? colorText = document.StyleEngine.GetStyleProperty(styleName!, "color", OdfNamespaces.Fo, "text");
        string? sizeText = document.StyleEngine.GetStyleProperty(styleName!, "font-size", OdfNamespaces.Fo, "text");

        bool bold = string.Equals(weight, "bold", StringComparison.OrdinalIgnoreCase);
        bool italic = string.Equals(fontStyle, "italic", StringComparison.OrdinalIgnoreCase);
        bool hasUnderline = !string.IsNullOrWhiteSpace(underline) && !string.Equals(underline, "none", StringComparison.OrdinalIgnoreCase);
        formatted.ApplyStyle(bold, italic, hasUnderline, ParseHexColor(colorText), ParsePoints(sizeText));
    }

    private static Color? ParseHexColor(string? text)
    {
        if (string.IsNullOrEmpty(text) || text![0] != '#' || text.Length != 7)
        {
            return null;
        }

        return TryParseHex(text, out int rgb)
            ? new Color((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF))
            : null;
    }

    // 長度字串去掉兩個字元的單位後解析數字；net 目標用範圍型多載避免配置子字串。
    private static bool TryParseLengthNumber(string text, out double value)
    {
#if NETSTANDARD2_0
        return double.TryParse(text.Substring(0, text.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
#else
        return double.TryParse(text.AsSpan(0, text.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
#endif
    }

    private static bool TryParseHex(string text, out int value)
    {
#if NETSTANDARD2_0
        return int.TryParse(text.Substring(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
#else
        return int.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
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

    // 圖片以 base64 內嵌給 PDFsharp；讀不到或過大時略過，不中斷整份匯出。
    private static void AddFrameImage(OdfNode frame, InlineSink parent, ExportContext context)
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

            double? width = null;
            string? widthText = frame.GetAttribute("width", OdfNamespaces.Svg);
            if (!string.IsNullOrEmpty(widthText) && widthText!.EndsWith("cm", StringComparison.Ordinal)
                && TryParseLengthNumber(widthText, out double parsed)
                && parsed > 0)
            {
                width = Math.Min(parsed, context.TextWidthCm);
            }

            parent.AddImage("base64:" + Convert.ToBase64String(buffer.ToArray()), width);
        }
        catch (Exception ex) when (ex is IOException or KeyNotFoundException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // 圖片格式不支援或讀取失敗：略過這張圖片。
        }
    }

    private static void AddSegmentedText(InlineSink sink, string text, ExportContext context)
    {
        var segments = context.FontContext.SegmentText(text, context.DefaultFont);
        foreach (var (segText, fontName) in segments)
        {
            if (fontName == context.DefaultFont)
            {
                sink.AddText(segText);
            }
            else
            {
                context.FontContext.WarnIfUnresolvable(fontName, "CNS 11643 高位字面文字 PDF 匯出");
                InlineSink run = sink.AddFormatted();
                run.SetFont(fontName);
                run.AddText(segText);
            }
        }
    }
}

internal sealed class OdfPdfFontResolver : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string? resolved = Styles.OdfFontContext.Default.ResolveFontFallback(familyName, IsUsablePdfFont);
        if (resolved is not null)
        {
            return new FontResolverInfo(resolved, isBold, isItalic);
        }

        if (OdfKit.Internal.OdfStringHelper.Contains(familyName, "Courier", StringComparison.OrdinalIgnoreCase))
        {
            return new FontResolverInfo("Courier New", isBold, isItalic);
        }
        return new FontResolverInfo("Arial", isBold, isItalic);
    }

    public byte[] GetFont(string faceName)
    {
        foreach (string candidate in Styles.OdfFontContext.Default.GetFontFallbackCandidates(faceName))
        {
            string? fontPath = Styles.OdfFontContext.Default.ResolveFontPath(candidate);
            if (fontPath is not null && File.Exists(fontPath))
            {
                byte[] data = File.ReadAllBytes(fontPath);
                if (!Styles.OdfFontContext.IsTrueTypeCollection(fontPath))
                {
                    return data;
                }

                byte[]? firstFace = TryExtractFirstFace(data);
                if (firstFace is not null)
                {
                    return firstFace;
                }

                OdfKitDiagnostics.Warn(
                    OdfLocalizer.GetMessage("Diag_OdfPdfExporter_TrueTypeCollectionFontFallback", candidate));
            }
        }

        string[] fallbacks = { "Arial", "Courier New", "Liberation Sans", "DejaVu Sans" };
        foreach (var fb in fallbacks)
        {
            string? fontPath = Styles.OdfFontContext.Default.ResolveFontPath(fb);
            if (fontPath is not null && File.Exists(fontPath))
            {
                return File.ReadAllBytes(fontPath);
            }
        }

        string[] standardDirs = {
            @"C:\Windows\Fonts",
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            "/Library/Fonts"
        };
        foreach (var dir in standardDirs)
        {
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return File.ReadAllBytes(files[0]);
                }
            }
        }

        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPdfExporter_UnableResolveFont", faceName));
    }

    private static bool IsUsablePdfFont(string familyName)
    {
        string? fontPath = Styles.OdfFontContext.Default.ResolveFontPath(familyName);
        return fontPath is not null && File.Exists(fontPath);
    }

    // 將 TrueType Collection 的第一個字型面重組為獨立 TTF，供不支援 .ttc 的 PDFsharp 使用。
    private static byte[]? TryExtractFirstFace(byte[] ttc)
    {
        try
        {
            if (ttc.Length < 16)
            {
                return null;
            }

            static uint U32(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
            static int U16(byte[] b, int o) => (b[o] << 8) | b[o + 1];

            if (U32(ttc, 0) != 0x74746366u || U32(ttc, 8) == 0)
            {
                return null;
            }

            int faceOffset = checked((int)U32(ttc, 12));
            int tableCount = U16(ttc, faceOffset + 4);
            int headerSize = 12 + (16 * tableCount);
            var records = new (uint Tag, uint Checksum, int Offset, int Length)[tableCount];
            long total = headerSize;
            for (int i = 0; i < tableCount; i++)
            {
                int r = faceOffset + 12 + (16 * i);
                records[i] = (U32(ttc, r), U32(ttc, r + 4), checked((int)U32(ttc, r + 8)), checked((int)U32(ttc, r + 12)));
                if ((long)records[i].Offset + records[i].Length > ttc.Length)
                {
                    return null;
                }

                total += (records[i].Length + 3L) & ~3L;
            }

            if (total > int.MaxValue)
            {
                return null;
            }

            var output = new byte[total];
            Array.Copy(ttc, faceOffset, output, 0, 12);
            int position = headerSize;
            for (int i = 0; i < tableCount; i++)
            {
                int r = 12 + (16 * i);
                Array.Copy(ttc, faceOffset + r, output, r, 8);
                output[r + 8] = (byte)(position >> 24);
                output[r + 9] = (byte)(position >> 16);
                output[r + 10] = (byte)(position >> 8);
                output[r + 11] = (byte)position;
                Array.Copy(ttc, faceOffset + r + 12, output, r + 12, 4);
                Array.Copy(ttc, records[i].Offset, output, position, records[i].Length);
                position += (records[i].Length + 3) & ~3;
            }

            return output;
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}

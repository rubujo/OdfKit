using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// 把段落與表格排版成多頁：貪婪斷行（含中日韓禁則）、對齊、清單縮排、表格列分頁與標題列重複。
/// Lays paragraphs and tables out into pages: greedy line breaking (with CJK line-break rules), alignment,
/// hanging indents, table row pagination and repeated header rows.
/// </summary>
internal sealed class PdfLayoutEngine
{
    internal const double PointsPerCentimeter = 72.0 / 2.54;
    private const double TabStop = 36;
    private const double CellPaddingX = 3;
    private const double CellPaddingY = 2;

    private readonly PdfFontProvider _fonts;
    private readonly PdfFontResource _defaultFont;
    private readonly PdfImageTable _images;
    private readonly List<PdfPageBuilder> _pages = [];
    private readonly double _pageWidth;
    private readonly double _pageHeight;
    private readonly double _margin;
    private PdfPageBuilder _page;
    private double _y;

    internal PdfLayoutEngine(PdfFontProvider fonts, string defaultFontName, PdfImageTable images, double pageWidth, double pageHeight, double margin)
    {
        _fonts = fonts;
        _defaultFont = fonts.GetRequired(defaultFontName);
        _images = images;
        _pageWidth = pageWidth;
        _pageHeight = pageHeight;
        _margin = margin;
        _page = NewPageCore();
    }

    internal IReadOnlyList<PdfPageBuilder> Pages => _pages;

    internal double ContentWidth => _pageWidth - (2 * _margin);

    private double Top => _margin;

    private double Bottom => _pageHeight - _margin;

    private PdfPageBuilder NewPageCore()
    {
        var page = new PdfPageBuilder(_pageWidth, _pageHeight);
        _pages.Add(page);
        _y = Top;
        return page;
    }

    private void NewPage() => _page = NewPageCore();

    // ---------- 內容流 ----------

    internal void Layout(PdfContent content)
    {
        for (int index = 0; index < content.Blocks.Count; index++)
        {
            object block = content.Blocks[index];
            if (block is ParagraphBlock paragraph)
            {
                // 標題與其後第一個內容單位一起換頁，避免標題孤立在頁底。
                double keep = 0;
                if (paragraph.KeepWithNext && index + 1 < content.Blocks.Count)
                {
                    keep = content.Blocks[index + 1] switch
                    {
                        TableBlock => 70,
                        ParagraphBlock next => FirstLineHeight(next),
                        _ => 0,
                    };
                }

                FlowParagraph(paragraph, _margin, ContentWidth, keep);
            }
            else if (block is TableBlock table)
            {
                FlowTable(table);
            }
        }
    }

    private double FirstLineHeight(ParagraphBlock paragraph)
    {
        List<LineBox> lines = BreakLines(paragraph, ContentWidth);
        return lines.Count > 0 ? lines[0].Height + paragraph.SpaceBefore : 0;
    }

    private void FlowParagraph(ParagraphBlock paragraph, double originX, double width, double keepWithNextHeight)
    {
        List<LineBox> lines = BreakLines(paragraph, width);
        double before = _y > Top ? paragraph.SpaceBefore : 0;
        if (paragraph.KeepWithNext && lines.Count > 0 && _y + before + lines[0].Height + paragraph.SpaceAfter + keepWithNextHeight > Bottom && _y > Top)
        {
            NewPage();
            before = 0;
        }

        _y += before;
        foreach (LineBox line in lines)
        {
            if (_y + line.Height > Bottom && _y > Top)
            {
                NewPage();
            }

            DrawLine(_page, line, originX, _y);
            _y += line.Height;
        }

        _y += paragraph.SpaceAfter;
    }

    // ---------- 斷行 ----------

    private enum AtomKind
    {
        Glyph,
        Space,
        Tab,
        Break,
        Image,
    }

    private sealed class Atom
    {
        internal AtomKind Kind;
        internal int CodePoint;
        internal PdfFontResource? Font;
        internal int Glyph;
        internal double Width;
        internal RunStyle Style = null!;
        internal bool Preserve;
        internal ImageInline? Image;
    }

    private sealed class Fragment
    {
        internal PdfFontResource? Font;
        internal RunStyle Style = null!;
        internal double X;
        internal double Width;
        internal List<(int Glyph, int CodePoint)> Glyphs = [];
        internal ImageInline? Image;
    }

    private sealed class LineBox
    {
        internal List<Fragment> Fragments = [];
        internal double Height;
        internal double Ascent;
    }

    private List<Atom> BuildAtoms(ParagraphBlock paragraph)
    {
        var atoms = new List<Atom>();
        foreach (PdfInline inline in paragraph.Inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    AppendText(atoms, text);
                    break;
                case TabInline tab:
                    atoms.Add(new Atom { Kind = AtomKind.Tab, Style = tab.Style });
                    break;
                case BreakInline lineBreak:
                    atoms.Add(new Atom { Kind = AtomKind.Break, Style = lineBreak.Style });
                    break;
                case ImageInline image:
                    atoms.Add(new Atom
                    {
                        Kind = AtomKind.Image,
                        Image = image,
                        Width = image.Width,
                        Style = paragraph.BaseStyle,
                    });
                    break;
            }
        }

        return atoms;
    }

    private void AppendText(List<Atom> atoms, TextInline text)
    {
        PdfFontResource primary = (text.Style.FontName is null ? null : _fonts.TryGet(text.Style.FontName)) ?? _defaultFont;
        string value = text.Text;
        for (int index = 0; index < value.Length; index++)
        {
            int codePoint = value[index];
            if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
            {
                codePoint = char.ConvertToUtf32(value[index], value[index + 1]);
                index++;
            }

            if (codePoint == ' ' || codePoint == '\t' || codePoint == '\n' || codePoint == '\r')
            {
                bool collapsible = !text.Preserve;
                if (collapsible && atoms.Count > 0 && atoms[atoms.Count - 1].Kind == AtomKind.Space && !atoms[atoms.Count - 1].Preserve)
                {
                    continue;
                }

                AddGlyph(atoms, primary, ' ', text.Style, AtomKind.Space, text.Preserve);
                continue;
            }

            if (codePoint < 0x20 || codePoint == 0x7F || (codePoint >= 0x200B && codePoint <= 0x200F) || codePoint == 0xFEFF)
            {
                continue;
            }

            AddGlyph(atoms, primary, codePoint, text.Style, AtomKind.Glyph, false);
        }
    }

    private void AddGlyph(List<Atom> atoms, PdfFontResource primary, int codePoint, RunStyle style, AtomKind kind, bool preserve)
    {
        (PdfFontResource font, int glyph) = _fonts.Resolve(primary, codePoint);
        double advance = font.Font.GetAdvance(glyph) * style.Size / font.Font.UnitsPerEm;
        atoms.Add(new Atom
        {
            Kind = kind,
            CodePoint = codePoint,
            Font = font,
            Glyph = glyph,
            Width = advance,
            Style = style,
            Preserve = preserve,
        });
    }

    private static bool IsCjk(int cp) =>
        (cp >= 0x2E80 && cp <= 0x9FFF) || (cp >= 0xAC00 && cp <= 0xD7AF) || (cp >= 0xF900 && cp <= 0xFAFF)
        || (cp >= 0xFE30 && cp <= 0xFE4F) || (cp >= 0xFF00 && cp <= 0xFFEF) || (cp >= 0x1100 && cp <= 0x11FF)
        || (cp >= 0x1B000 && cp <= 0x1B16F) || (cp >= 0x20000 && cp <= 0x3FFFF);

    private static readonly HashSet<int> NoBreakBeforeSet = BuildSet("，。、．：；！？）］｝〕〉》」』】〙〗〟’”ー…‥ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮ々!?,.;:)]}%");

    private static readonly HashSet<int> NoBreakAfterSet = BuildSet("（［｛〔〈《「『【〘〖〝‘“([{");

    private static HashSet<int> BuildSet(string characters)
    {
        var set = new HashSet<int>();
        foreach (char c in characters)
        {
            set.Add(c);
        }

        return set;
    }

    private static bool NoBreakBefore(int cp) => NoBreakBeforeSet.Contains(cp);

    private static bool NoBreakAfter(int cp) => NoBreakAfterSet.Contains(cp);

    private static bool CanBreakBetween(Atom a, Atom b)
    {
        if (a.Kind == AtomKind.Space || a.Kind == AtomKind.Tab)
        {
            return true;
        }

        if (a.Kind == AtomKind.Image || b.Kind == AtomKind.Image)
        {
            return true;
        }

        if (b.Kind != AtomKind.Glyph)
        {
            return false;
        }

        if (IsCjk(a.CodePoint) || IsCjk(b.CodePoint))
        {
            return !NoBreakBefore(b.CodePoint) && !NoBreakAfter(a.CodePoint);
        }

        return (a.CodePoint == '-' || (a.CodePoint >= 0x2010 && a.CodePoint <= 0x2014)) && char.IsLetterOrDigit((char)Math.Min(b.CodePoint, 0xFFFF));
    }

    private List<LineBox> BreakLines(ParagraphBlock paragraph, double contentWidth)
    {
        List<Atom> atoms = BuildAtoms(paragraph);
        var lines = new List<LineBox>();
        double firstStart = Math.Max(0, paragraph.LeftIndent + paragraph.FirstLineIndent);
        double restStart = Math.Max(0, paragraph.LeftIndent);
        double firstWidth = Math.Max(20, contentWidth - firstStart);
        double restWidth = Math.Max(20, contentWidth - restStart);

        if (atoms.Count == 0)
        {
            lines.Add(BuildLine(atoms, 0, -1, firstStart, firstWidth, paragraph, isLast: true, forced: false));
            return lines;
        }

        int index = 0;
        bool first = true;
        while (index < atoms.Count)
        {
            double start = first ? firstStart : restStart;
            double width = first ? firstWidth : restWidth;
            while (index < atoms.Count && atoms[index].Kind == AtomKind.Space && !atoms[index].Preserve)
            {
                index++;
            }

            if (index >= atoms.Count)
            {
                break;
            }

            double used = 0;
            int lastBreak = -1;
            int i = index;
            bool forced = false;
            while (i < atoms.Count)
            {
                Atom atom = atoms[i];
                if (atom.Kind == AtomKind.Break)
                {
                    forced = true;
                    break;
                }

                if (atom.Kind == AtomKind.Tab)
                {
                    double position = start + used;
                    double next = (Math.Floor((position + 0.5) / TabStop) + 1) * TabStop;
                    atom.Width = Math.Max(1, next - position);
                }

                double newUsed = used + atom.Width;
                if (atom.Kind != AtomKind.Space && newUsed > width + 0.01 && i > index)
                {
                    break;
                }

                used = newUsed;
                if (i + 1 < atoms.Count && CanBreakBetween(atom, atoms[i + 1]))
                {
                    lastBreak = i;
                }

                i++;
            }

            int end;
            int next2;
            if (forced)
            {
                end = i - 1;
                next2 = i + 1;
            }
            else if (i >= atoms.Count)
            {
                end = atoms.Count - 1;
                next2 = atoms.Count;
            }
            else if (lastBreak >= index)
            {
                end = lastBreak;
                next2 = lastBreak + 1;
            }
            else
            {
                end = Math.Max(index, i - 1);
                next2 = end + 1;
            }

            bool isLast = next2 >= atoms.Count && !forced;
            lines.Add(BuildLine(atoms, index, end, start, width, paragraph, isLast, forced));
            index = next2;
            first = false;
            if (forced && index >= atoms.Count)
            {
                // 段落以強制換行結尾時補一個空行。
                lines.Add(BuildLine(atoms, 0, -1, restStart, restWidth, paragraph, isLast: true, forced: false));
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(BuildLine(atoms, 0, -1, firstStart, firstWidth, paragraph, isLast: true, forced: false));
        }

        return lines;
    }

    private LineBox BuildLine(List<Atom> atoms, int start, int end, double lineStart, double width, ParagraphBlock paragraph, bool isLast, bool forced)
    {
        var line = new LineBox();
        // 行尾空白不計入寬度與對齊。
        int visibleEnd = end;
        while (visibleEnd >= start && (atoms[visibleEnd].Kind == AtomKind.Space && !atoms[visibleEnd].Preserve))
        {
            visibleEnd--;
        }

        double contentWidth = 0;
        double ascent = 0;
        double descent = 0;
        for (int i = start; i <= visibleEnd; i++)
        {
            Atom atom = atoms[i];
            contentWidth += atom.Width;
            (double a, double d) = Metrics(atom);
            ascent = Math.Max(ascent, a);
            descent = Math.Max(descent, d);
        }

        if (visibleEnd < start)
        {
            (double a, double d) = BaseMetrics(paragraph.BaseStyle);
            ascent = a;
            descent = d;
        }

        line.Ascent = ascent;
        line.Height = ascent + descent;

        double offset = 0;
        double extraPerGap = 0;
        var gaps = new HashSet<int>();
        double slack = width - contentWidth;
        switch (paragraph.Align)
        {
            case PdfAlign.Center:
                offset = Math.Max(0, slack / 2);
                break;
            case PdfAlign.Right:
                offset = Math.Max(0, slack);
                break;
            case PdfAlign.Justify:
                if (!isLast && !forced && slack > 0.5)
                {
                    bool hasSpace = false;
                    for (int i = start; i < visibleEnd; i++)
                    {
                        if (atoms[i].Kind == AtomKind.Space)
                        {
                            hasSpace = true;
                            gaps.Add(i);
                        }
                    }

                    if (!hasSpace)
                    {
                        for (int i = start; i < visibleEnd; i++)
                        {
                            if (CanBreakBetween(atoms[i], atoms[i + 1]))
                            {
                                gaps.Add(i);
                            }
                        }
                    }

                    if (gaps.Count > 0)
                    {
                        extraPerGap = slack / gaps.Count;
                    }
                }

                break;
        }

        double x = lineStart + offset;
        Fragment? current = null;
        for (int i = start; i <= visibleEnd; i++)
        {
            Atom atom = atoms[i];
            if (atom.Kind == AtomKind.Image && atom.Image is ImageInline image)
            {
                line.Fragments.Add(new Fragment { Style = atom.Style, X = x, Width = atom.Width, Image = image });
                x += atom.Width + (gaps.Contains(i) ? extraPerGap : 0);
                current = null;
                continue;
            }

            if (atom.Kind == AtomKind.Tab || atom.Font is null)
            {
                x += atom.Width;
                current = null;
                continue;
            }

            if (current is null || current.Font != atom.Font || !current.Style.SameAs(atom.Style))
            {
                current = new Fragment { Font = atom.Font, Style = atom.Style, X = x };
                line.Fragments.Add(current);
            }

            current.Glyphs.Add((atom.Glyph, atom.CodePoint));
            current.Width += atom.Width;
            x += atom.Width;
            if (gaps.Contains(i))
            {
                current.Width += extraPerGap;
                x += extraPerGap;
                current = null;
            }
        }

        return line;
    }

    private (double Ascent, double Descent) Metrics(Atom atom)
    {
        if (atom.Kind == AtomKind.Image && atom.Image is ImageInline image)
        {
            return (image.Height, 0);
        }

        return atom.Font is null ? BaseMetrics(atom.Style) : FontMetrics(atom.Font, atom.Style.Size);
    }

    private (double Ascent, double Descent) BaseMetrics(RunStyle style)
    {
        PdfFontResource font = (style.FontName is null ? null : _fonts.TryGet(style.FontName)) ?? _defaultFont;
        return FontMetrics(font, style.Size);
    }

    private static (double Ascent, double Descent) FontMetrics(PdfFontResource font, double size)
    {
        double scale = size / font.Font.UnitsPerEm;
        double ascent = font.Font.Ascent * scale;
        double descent = -font.Font.Descent * scale;
        double natural = ascent + descent + (font.Font.LineGap * scale);
        double target = Math.Max(natural, size * 1.2);
        double extra = target - ascent - descent;
        if (extra > 0)
        {
            ascent += extra * 0.8;
            descent += extra * 0.2;
        }

        return (ascent, descent);
    }

    // ---------- 繪製 ----------

    private static void DrawLine(PdfPageBuilder page, LineBox line, double originX, double top)
    {
        double baseline = top + line.Ascent;
        foreach (Fragment fragment in line.Fragments)
        {
            double x = originX + fragment.X;
            if (fragment.Image is ImageInline image)
            {
                page.Image(image.ResourceName, x, baseline - image.Height, image.Width, image.Height);
                if (image.Link is string imageLink)
                {
                    page.Link(x, baseline - image.Height, x + image.Width, baseline, imageLink);
                }

                continue;
            }

            RunStyle style = fragment.Style;
            PdfColor color = style.Color ?? PdfColor.Black;
            page.Text(fragment.Font!, style.Size, x, baseline, fragment.Glyphs, style.Bold, style.Italic, color);
            if (style.Underline)
            {
                double underlineY = baseline + (style.Size * 0.12);
                page.Line(x, underlineY, x + fragment.Width, underlineY, Math.Max(0.4, style.Size * 0.05), color);
            }

            if (style.Link is string link)
            {
                page.Link(x, baseline - (style.Size * 0.9), x + fragment.Width, baseline + (style.Size * 0.25), link);
            }
        }
    }

    // ---------- 表格 ----------

    private sealed class CellLayout
    {
        internal TableCellModel Model = null!;
        internal int Row;
        internal List<(LineBox Line, double GapBefore)> Lines = [];
        internal double ContentHeight;
        internal int RowSpan = 1;
    }

    private void FlowTable(TableBlock table)
    {
        int columns = Math.Max(1, table.Columns);
        double columnWidth = Math.Max(28, ContentWidth / columns);
        double originX = _margin;
        int rowCount = table.Rows.Count;
        var cellsByRow = new List<CellLayout>[rowCount];
        var rowHeights = new double[rowCount];
        for (int r = 0; r < rowCount; r++)
        {
            cellsByRow[r] = [];
            foreach (TableCellModel cell in table.Rows[r].Cells)
            {
                int column = Math.Min(cell.Column, columns - 1);
                int columnSpan = Math.Max(1, Math.Min(cell.ColumnSpan, columns - column));
                var layout = new CellLayout { Model = cell, Row = r, RowSpan = Math.Max(1, Math.Min(cell.RowSpan, rowCount - r)) };
                double innerWidth = Math.Max(10, (columnWidth * columnSpan) - (2 * CellPaddingX));
                double total = 0;
                bool firstBlock = true;
                foreach (ParagraphBlock block in cell.Blocks)
                {
                    List<LineBox> lines = BreakLines(block, innerWidth);
                    for (int i = 0; i < lines.Count; i++)
                    {
                        double gap = i == 0 && !firstBlock ? block.SpaceBefore : 0;
                        layout.Lines.Add((lines[i], gap));
                        total += gap + lines[i].Height;
                    }

                    total += block.SpaceAfter;
                    firstBlock = false;
                }

                layout.ContentHeight = total;
                cellsByRow[r].Add(layout);
            }
        }

        // 列高：先看不跨列的儲存格，再把跨列儲存格不足的高度補在其最後一列。
        for (int r = 0; r < rowCount; r++)
        {
            double height = 0;
            foreach (CellLayout cell in cellsByRow[r])
            {
                if (cell.RowSpan == 1)
                {
                    height = Math.Max(height, cell.ContentHeight + (2 * CellPaddingY));
                }
            }

            rowHeights[r] = Math.Max(height, 14);
        }

        for (int r = 0; r < rowCount; r++)
        {
            foreach (CellLayout cell in cellsByRow[r])
            {
                if (cell.RowSpan <= 1)
                {
                    continue;
                }

                double have = 0;
                for (int k = r; k < r + cell.RowSpan; k++)
                {
                    have += rowHeights[k];
                }

                double need = cell.ContentHeight + (2 * CellPaddingY);
                if (need > have)
                {
                    rowHeights[r + cell.RowSpan - 1] += need - have;
                }
            }
        }

        double pageContent = Bottom - Top;
        int headerRows = 0;
        while (headerRows < rowCount && table.Rows[headerRows].IsHeader)
        {
            headerRows++;
        }

        double headerHeight = 0;
        for (int r = 0; r < headerRows; r++)
        {
            headerHeight += rowHeights[r];
        }

        if (headerHeight > pageContent / 2)
        {
            headerRows = 0;
            headerHeight = 0;
        }

        _y += 4;
        int row = 0;
        while (row < rowCount)
        {
            // 以跨列儲存格把相連的列組成一個不可分割的區塊。
            int groupEnd = row;
            for (int r = row; r <= groupEnd; r++)
            {
                foreach (CellLayout cell in cellsByRow[r])
                {
                    groupEnd = Math.Max(groupEnd, Math.Min(rowCount - 1, r + cell.RowSpan - 1));
                }
            }

            double groupHeight = 0;
            for (int r = row; r <= groupEnd; r++)
            {
                groupHeight += rowHeights[r];
            }

            if (groupHeight > pageContent)
            {
                // 單一區塊比整頁還高：改成逐列處理，且取消跨列，內容以行為單位切開。
                for (int r = row; r <= groupEnd; r++)
                {
                    foreach (CellLayout cell in cellsByRow[r])
                    {
                        cell.RowSpan = 1;
                    }

                    FlowTallRow(cellsByRow[r], originX, columnWidth, columns, table.Rows[r].IsHeader);
                }

                row = groupEnd + 1;
                continue;
            }

            // 第一個內容區塊與標題列一起移到下一頁，避免標題列孤立在頁底。
            if (row == 0 && headerRows > 0 && headerHeight + FirstBodyHeight(cellsByRow, rowHeights, headerRows, rowCount) > Bottom - _y && _y > Top)
            {
                NewPage();
            }

            if (_y + groupHeight > Bottom && _y > Top)
            {
                NewPage();
                if (row >= headerRows && headerRows > 0 && _y + headerHeight + groupHeight <= Bottom)
                {
                    DrawRowRange(cellsByRow, rowHeights, 0, headerRows - 1, originX, columnWidth, columns);
                }
            }

            DrawRowRange(cellsByRow, rowHeights, row, groupEnd, originX, columnWidth, columns);
            row = groupEnd + 1;
        }

        _y += 6;
    }

    private static double FirstBodyHeight(List<CellLayout>[] cellsByRow, double[] rowHeights, int headerRows, int rowCount)
    {
        if (headerRows >= rowCount)
        {
            return 0;
        }

        int end = headerRows;
        for (int r = headerRows; r <= end; r++)
        {
            foreach (CellLayout cell in cellsByRow[r])
            {
                end = Math.Max(end, Math.Min(rowCount - 1, r + cell.RowSpan - 1));
            }
        }

        double height = 0;
        for (int r = headerRows; r <= end; r++)
        {
            height += rowHeights[r];
        }

        return height;
    }

    private void DrawRowRange(List<CellLayout>[] cellsByRow, double[] rowHeights, int firstRow, int lastRow, double originX, double columnWidth, int columns)
    {
        double rowTop = _y;
        var tops = new double[lastRow - firstRow + 2];
        tops[0] = rowTop;
        for (int r = firstRow; r <= lastRow; r++)
        {
            tops[r - firstRow + 1] = tops[r - firstRow] + rowHeights[r];
        }

        for (int r = firstRow; r <= lastRow; r++)
        {
            foreach (CellLayout cell in cellsByRow[r])
            {
                int column = Math.Min(cell.Model.Column, columns - 1);
                int columnSpan = Math.Max(1, Math.Min(cell.Model.ColumnSpan, columns - column));
                double x = originX + (column * columnWidth);
                double width = columnWidth * columnSpan;
                int endRow = Math.Min(lastRow, r + cell.RowSpan - 1);
                double top = tops[r - firstRow];
                double height = tops[endRow - firstRow + 1] - top;
                _page.Rectangle(x, top, width, height, 0.5, PdfColor.Gray);
                DrawCellLines(_page, cell.Lines, x + CellPaddingX, top + CellPaddingY, 0, cell.Lines.Count);
            }
        }

        _y = tops[lastRow - firstRow + 1];
    }

    private static void DrawCellLines(PdfPageBuilder page, List<(LineBox Line, double GapBefore)> lines, double x, double top, int from, int to)
    {
        double y = top;
        for (int i = from; i < to; i++)
        {
            y += lines[i].GapBefore;
            DrawLine(page, lines[i].Line, x, y);
            y += lines[i].Line.Height;
        }
    }

    /// <summary>
    /// 單列比整頁還高：逐頁切出能放下的行，邊框畫在每一頁的片段上。
    /// </summary>
    private void FlowTallRow(List<CellLayout> cells, double originX, double columnWidth, int columns, bool isHeader)
    {
        _ = isHeader;
        var consumed = new int[cells.Count];
        bool remaining = true;
        while (remaining)
        {
            if (_y + 30 > Bottom)
            {
                NewPage();
            }

            double available = Bottom - _y - (2 * CellPaddingY);
            double used = 0;
            remaining = false;
            var take = new int[cells.Count];
            for (int c = 0; c < cells.Count; c++)
            {
                double height = 0;
                int index = consumed[c];
                while (index < cells[c].Lines.Count)
                {
                    double next = cells[c].Lines[index].GapBefore + cells[c].Lines[index].Line.Height;
                    if (height + next > available && index > consumed[c])
                    {
                        break;
                    }

                    height += next;
                    index++;
                }

                take[c] = index;
                used = Math.Max(used, height);
                if (index < cells[c].Lines.Count)
                {
                    remaining = true;
                }
            }

            double rowHeight = used + (2 * CellPaddingY);
            for (int c = 0; c < cells.Count; c++)
            {
                int column = Math.Min(cells[c].Model.Column, columns - 1);
                int columnSpan = Math.Max(1, Math.Min(cells[c].Model.ColumnSpan, columns - column));
                double x = originX + (column * columnWidth);
                _page.Rectangle(x, _y, columnWidth * columnSpan, rowHeight, 0.5, PdfColor.Gray);
                DrawCellLines(_page, cells[c].Lines, x + CellPaddingX, _y + CellPaddingY, consumed[c], take[c]);
                consumed[c] = take[c];
            }

            _y += rowHeight;
            if (remaining)
            {
                NewPage();
            }
        }
    }

    // ---------- 整份文件 ----------

    internal static string BuildUri(string url)
    {
        var sb = new StringBuilder(url.Length);
        foreach (byte b in Encoding.UTF8.GetBytes(url))
        {
            if (b > 0x20 && b < 0x7F)
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }
}

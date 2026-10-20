using System.Collections.Generic;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// 段落對齊方式。
/// </summary>
internal enum PdfAlign
{
    Left,
    Center,
    Right,
    Justify,
}

/// <summary>
/// 行內文字樣式（不可變）。
/// </summary>
internal sealed class RunStyle(string? fontName, double size, bool bold, bool italic, bool underline, PdfColor? color, string? link)
{
    internal string? FontName { get; } = fontName;

    internal double Size { get; } = size;

    internal bool Bold { get; } = bold;

    internal bool Italic { get; } = italic;

    internal bool Underline { get; } = underline;

    internal PdfColor? Color { get; } = color;

    internal string? Link { get; } = link;

    internal RunStyle WithFont(string? name) => new(name, Size, Bold, Italic, Underline, Color, Link);

    internal RunStyle WithLink(string url) => new(FontName, Size, Bold, Italic, true, PdfColor.Link, url);

    internal RunStyle With(bool bold, bool italic, bool underline, PdfColor? color, double? size) =>
        new(FontName, size ?? Size, Bold || bold, Italic || italic, Underline || underline, color ?? Color, Link);

    internal bool SameAs(RunStyle other) =>
        FontName == other.FontName && Size == other.Size && Bold == other.Bold && Italic == other.Italic
        && Underline == other.Underline && Color.Equals(other.Color) && Link == other.Link;
}

internal abstract class PdfInline
{
}

internal sealed class TextInline(string text, RunStyle style, bool preserve) : PdfInline
{
    internal string Text { get; } = text;

    internal RunStyle Style { get; } = style;

    /// <summary>
    /// 為 <see langword="true"/> 時空白不合併（來自 <c>text:s</c>）。
    /// </summary>
    internal bool Preserve { get; } = preserve;
}

internal sealed class TabInline(RunStyle style) : PdfInline
{
    internal RunStyle Style { get; } = style;
}

internal sealed class BreakInline(RunStyle style) : PdfInline
{
    internal RunStyle Style { get; } = style;
}

internal sealed class ImageInline(string resourceName, double width, double height, string? link) : PdfInline
{
    internal string ResourceName { get; } = resourceName;

    internal double Width { get; } = width;

    internal double Height { get; } = height;

    internal string? Link { get; } = link;
}

internal sealed class ParagraphBlock(RunStyle baseStyle)
{
    internal List<PdfInline> Inlines { get; } = [];

    internal RunStyle BaseStyle { get; } = baseStyle;

    internal PdfAlign Align { get; set; }

    internal double LeftIndent { get; set; }

    internal double FirstLineIndent { get; set; }

    internal double SpaceBefore { get; set; }

    internal double SpaceAfter { get; set; }

    internal bool KeepWithNext { get; set; }
}

internal sealed class TableCellModel
{
    internal int Column { get; set; }

    internal int ColumnSpan { get; set; } = 1;

    internal int RowSpan { get; set; } = 1;

    internal List<ParagraphBlock> Blocks { get; } = [];
}

internal sealed class TableRowModel
{
    internal bool IsHeader { get; set; }

    internal List<TableCellModel> Cells { get; } = [];
}

internal sealed class TableBlock
{
    internal int Columns { get; set; }

    internal List<TableRowModel> Rows { get; } = [];
}

/// <summary>
/// 文件內容：段落或表格的順序序列。
/// </summary>
internal sealed class PdfContent
{
    internal List<object> Blocks { get; } = [];
}

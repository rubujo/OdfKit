using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// RGB 顏色（0～1）。
/// </summary>
internal readonly struct PdfColor(double r, double g, double b) : IEquatable<PdfColor>
{
    internal static readonly PdfColor Black = new(0, 0, 0);

    internal static readonly PdfColor Link = new(0, 0, 1);

    internal static readonly PdfColor Gray = new(0.5, 0.5, 0.5);

    internal double R { get; } = r;

    internal double G { get; } = g;

    internal double B { get; } = b;

    internal string Fill() => PdfCore.Number(R) + " " + PdfCore.Number(G) + " " + PdfCore.Number(B) + " rg";

    internal string Stroke() => PdfCore.Number(R) + " " + PdfCore.Number(G) + " " + PdfCore.Number(B) + " RG";

    public bool Equals(PdfColor other) => R == other.R && G == other.G && B == other.B;

    public override bool Equals(object? obj) => obj is PdfColor other && Equals(other);

    public override int GetHashCode() => (R, G, B).GetHashCode();
}

/// <summary>
/// 一頁的內容串流與資源：以「由上往下」座標接受繪製，並轉成 PDF 座標。
/// One page's content stream and resources; accepts top-down coordinates and converts them to PDF coordinates.
/// </summary>
internal sealed class PdfPageBuilder(double width, double height)
{
    private readonly StringBuilder _content = new();
    private readonly List<(double X1, double Y1, double X2, double Y2, string Uri)> _links = [];

    internal double Width { get; } = width;

    internal double Height { get; } = height;

    internal HashSet<PdfFontResource> Fonts { get; } = [];

    internal HashSet<string> ImageNames { get; } = [];

    internal IReadOnlyList<(double X1, double Y1, double X2, double Y2, string Uri)> Links => _links;

    internal string Content => _content.ToString();

    /// <summary>
    /// 在基線 (x, baselineTop) 繪製字形序列。
    /// </summary>
    internal void Text(
        PdfFontResource font,
        double size,
        double x,
        double baselineTop,
        IReadOnlyList<(int Glyph, int CodePoint)> glyphs,
        bool bold,
        bool italic,
        PdfColor color)
    {
        if (glyphs.Count == 0)
        {
            return;
        }

        Fonts.Add(font);
        var hex = new StringBuilder(glyphs.Count * 4);
        foreach ((int glyph, int codePoint) in glyphs)
        {
            font.Use(glyph, codePoint);
            hex.Append(font.CodeOf(glyph).ToString("X4", CultureInfo.InvariantCulture));
        }

        double y = Height - baselineTop;
        _content.Append("q BT /").Append(font.ResourceName).Append(' ').Append(PdfCore.Number(size)).Append(" Tf ")
            .Append(color.Fill()).Append(' ');
        if (bold)
        {
            _content.Append("2 Tr ").Append(color.Stroke()).Append(' ').Append(PdfCore.Number(size * 0.03)).Append(" w ");
        }

        _content.Append(italic ? "1 0 0.2126 1 " : "1 0 0 1 ").Append(PdfCore.Number(x)).Append(' ').Append(PdfCore.Number(y))
            .Append(" Tm <").Append(hex).Append("> Tj ET Q\n");
    }

    internal void Line(double x1, double yTop1, double x2, double yTop2, double thickness, PdfColor color)
    {
        _content.Append("q ").Append(color.Stroke()).Append(' ').Append(PdfCore.Number(thickness)).Append(" w ")
            .Append(PdfCore.Number(x1)).Append(' ').Append(PdfCore.Number(Height - yTop1)).Append(" m ")
            .Append(PdfCore.Number(x2)).Append(' ').Append(PdfCore.Number(Height - yTop2)).Append(" l S Q\n");
    }

    internal void Rectangle(double x, double yTop, double width, double height, double thickness, PdfColor color)
    {
        _content.Append("q ").Append(color.Stroke()).Append(' ').Append(PdfCore.Number(thickness)).Append(" w ")
            .Append(PdfCore.Number(x)).Append(' ').Append(PdfCore.Number(Height - yTop - height)).Append(' ')
            .Append(PdfCore.Number(width)).Append(' ').Append(PdfCore.Number(height)).Append(" re S Q\n");
    }

    internal void Image(string name, double x, double yTop, double width, double height)
    {
        ImageNames.Add(name);
        _content.Append("q ").Append(PdfCore.Number(width)).Append(" 0 0 ").Append(PdfCore.Number(height)).Append(' ')
            .Append(PdfCore.Number(x)).Append(' ').Append(PdfCore.Number(Height - yTop - height)).Append(" cm /")
            .Append(name).Append(" Do Q\n");
    }

    internal void Link(double x1, double yTop1, double x2, double yTop2, string uri) =>
        _links.Add((x1, Height - yTop2, x2, Height - yTop1, uri));
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Styles;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// 一個內嵌到 PDF 的字型：記錄用到的字形，輸出時寫入子集字型、寬度表與 ToUnicode 對照。
/// A font embedded into the PDF: tracks used glyphs and writes the subset font, widths and ToUnicode map.
/// </summary>
internal sealed class PdfFontResource
{
    private readonly SortedDictionary<int, int> _used = [];

    private readonly CffFont? _cff;

    internal PdfFontResource(SfntFont font, string resourceName)
    {
        Font = font;
        ResourceName = resourceName;
        byte[]? cffTable = font.IsCff ? font.GetCffTable() : null;
        _cff = cffTable is null ? null : CffFont.TryParse(cffTable);
    }

    internal SfntFont Font { get; }

    internal string ResourceName { get; }

    /// <summary>
    /// 內容串流中該字形使用的碼（Identity-H 的 CID）：CID 鍵控 CFF 取 charset 的 CID，其餘等於字形編號。
    /// </summary>
    internal int CodeOf(int glyph) => _cff is { IsCidKeyed: true } cff ? cff.CidOf(glyph) : glyph;

    internal void Use(int glyph, int codePoint)
    {
        if (!_used.ContainsKey(glyph))
        {
            _used[glyph] = codePoint;
        }
    }

    /// <summary>
    /// 寫入字型相關物件，回傳 Type0 字型物件編號。
    /// </summary>
    internal int Write(PdfBuilder builder)
    {
        bool cff = Font.IsCff;
        string fileKind = "FontFile2";
        string fileDictionary;
        byte[] fontBytes;
        byte[]? cffSubset = _cff?.BuildSubset(_used.Keys);
        if (cffSubset is not null)
        {
            fontBytes = cffSubset;
            fileKind = "FontFile3";
            fileDictionary = "/Subtype /CIDFontType0C";
        }
        else if (cff)
        {
            fontBytes = Font.BuildStandalone();
            fileKind = "FontFile3";
            fileDictionary = "/Subtype /OpenType";
        }
        else
        {
            fontBytes = Font.CanSubset ? Font.BuildSubset(_used.Keys) : Font.BuildStandalone();
            fileDictionary = "/Length1 " + fontBytes.Length.ToString(CultureInfo.InvariantCulture);
        }

        int fileObject = builder.AddStream(fileDictionary, fontBytes, compress: true);
        var coded = new SortedDictionary<int, (int Glyph, int CodePoint)>();
        foreach (KeyValuePair<int, int> used in _used)
        {
            coded[CodeOf(used.Key)] = (used.Key, used.Value);
        }

        string baseName = SubsetTag() + "+" + Font.PostScriptName;
        double scale = 1000.0 / Font.UnitsPerEm;
        string descriptor = "<< /Type /FontDescriptor /FontName " + PdfCore.Name(baseName)
            + " /Flags 4 /FontBBox [" + PdfCore.Number(Font.XMin * scale) + " " + PdfCore.Number(Font.YMin * scale) + " "
            + PdfCore.Number(Font.XMax * scale) + " " + PdfCore.Number(Font.YMax * scale) + "]"
            + " /ItalicAngle " + PdfCore.Number(Font.ItalicAngle)
            + " /Ascent " + PdfCore.Number(Font.Ascent * scale)
            + " /Descent " + PdfCore.Number(Font.Descent * scale)
            + " /CapHeight " + PdfCore.Number(Font.CapHeight * scale)
            + " /StemV 80 /" + fileKind + " " + PdfBuilder.Ref(fileObject) + " >>";
        int descriptorObject = builder.Add(descriptor);

        var widths = new StringBuilder();
        int previous = -2;
        bool open = false;
        foreach (KeyValuePair<int, (int Glyph, int CodePoint)> entry in coded)
        {
            int code = entry.Key;
            int glyph = entry.Value.Glyph;
            if (code != previous + 1)
            {
                if (open)
                {
                    widths.Append("] ");
                }

                widths.Append(code.ToString(CultureInfo.InvariantCulture)).Append(" [");
                open = true;
            }
            else
            {
                widths.Append(' ');
            }

            widths.Append(PdfCore.Number(Font.GetAdvance(glyph) * scale));
            previous = code;
        }

        if (open)
        {
            widths.Append(']');
        }

        string cidFont = "<< /Type /Font /Subtype /" + (cff ? "CIDFontType0" : "CIDFontType2") + " /BaseFont " + PdfCore.Name(baseName)
            + " /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor " + PdfBuilder.Ref(descriptorObject)
            + " /DW 1000 /W [" + widths + "]" + (cff ? string.Empty : " /CIDToGIDMap /Identity") + " >>";
        int cidObject = builder.Add(cidFont);

        int toUnicode = builder.AddStream(string.Empty, Encoding.ASCII.GetBytes(BuildToUnicode(coded)), compress: true);
        return builder.Add("<< /Type /Font /Subtype /Type0 /BaseFont " + PdfCore.Name(baseName) + " /Encoding /Identity-H /DescendantFonts ["
            + PdfBuilder.Ref(cidObject) + "] /ToUnicode " + PdfBuilder.Ref(toUnicode) + " >>");
    }

    private string SubsetTag()
    {
        uint hash = 2166136261;
        foreach (int glyph in _used.Keys)
        {
            hash = unchecked((hash ^ (uint)glyph) * 16777619);
        }

        var tag = new char[6];
        for (int i = 0; i < 6; i++)
        {
            tag[i] = (char)('A' + (hash % 26));
            hash = unchecked((hash * 1103515245) + 12345);
            hash ^= hash >> 11;
        }

        return new string(tag);
    }

    private static string BuildToUnicode(SortedDictionary<int, (int Glyph, int CodePoint)> coded)
    {
        var sb = new StringBuilder();
        sb.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n");
        sb.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
        sb.Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
        sb.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        var entries = new List<KeyValuePair<int, int>>();
        foreach (KeyValuePair<int, (int Glyph, int CodePoint)> item in coded)
        {
            entries.Add(new KeyValuePair<int, int>(item.Key, item.Value.CodePoint));
        }

        for (int start = 0; start < entries.Count; start += 100)
        {
            int count = Math.Min(100, entries.Count - start);
            sb.Append(count.ToString(CultureInfo.InvariantCulture)).Append(" beginbfchar\n");
            for (int i = 0; i < count; i++)
            {
                KeyValuePair<int, int> entry = entries[start + i];
                sb.Append('<').Append(entry.Key.ToString("X4", CultureInfo.InvariantCulture)).Append("> <");
                int codePoint = entry.Value;
                if (codePoint > 0xFFFF)
                {
                    int value = codePoint - 0x10000;
                    sb.Append((0xD800 + (value >> 10)).ToString("X4", CultureInfo.InvariantCulture));
                    sb.Append((0xDC00 + (value & 0x3FF)).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(codePoint.ToString("X4", CultureInfo.InvariantCulture));
                }

                sb.Append(">\n");
            }

            sb.Append("endbfchar\n");
        }

        sb.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend");
        return sb.ToString();
    }
}

/// <summary>
/// 依字型名稱解析字型檔並載入，提供逐字元的字形解析與缺字後備。
/// Resolves font names to files and loads them, providing per-character glyph resolution with fallback.
/// </summary>
internal sealed class PdfFontProvider(OdfFontContext fontContext)
{
    private static readonly string[] GlyphFallbackNames =
    [
        "Arial", "Segoe UI", "Liberation Sans", "DejaVu Sans", "Noto Sans", "Noto Sans CJK TC",
        "Microsoft JhengHei", "PingFang TC", "DFKai-SB", "MS Gothic",
    ];

    private readonly Dictionary<string, PdfFontResource?> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PdfFontResource> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PdfFontResource> _all = [];

    internal IReadOnlyList<PdfFontResource> All => _all;

    /// <summary>
    /// 取得字型；主字型必須存在，找不到時擲出例外。
    /// </summary>
    internal PdfFontResource GetRequired(string fontName)
    {
        PdfFontResource? font = TryGet(fontName) ?? TryGetFirstAvailable();
        return font ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPdfExporter_UnableResolveFont", fontName));
    }

    internal PdfFontResource? TryGet(string fontName)
    {
        if (_byName.TryGetValue(fontName, out PdfFontResource? cached))
        {
            return cached;
        }

        PdfFontResource? font = null;
        string? path = ResolvePath(fontName);
        if (path is not null)
        {
            font = LoadPath(path);
        }

        _byName[fontName] = font;
        return font;
    }

    /// <summary>
    /// 解析字元對應的字形：先試主字型，缺字時依序試後備字型；都沒有時回傳主字型的 .notdef。
    /// </summary>
    internal (PdfFontResource Font, int Glyph) Resolve(PdfFontResource primary, int codePoint)
    {
        int glyph = primary.Font.GetGlyph(codePoint);
        if (glyph != 0)
        {
            return (primary, glyph);
        }

        foreach (string name in GlyphFallbackNames)
        {
            PdfFontResource? candidate = TryGet(name);
            if (candidate is null || ReferenceEquals(candidate, primary))
            {
                continue;
            }

            int candidateGlyph = candidate.Font.GetGlyph(codePoint);
            if (candidateGlyph != 0)
            {
                return (candidate, candidateGlyph);
            }
        }

        return (primary, 0);
    }

    private string? ResolvePath(string fontName)
    {
        string? path = fontContext.ResolveFontPath(fontName);
        if (path is not null && File.Exists(path))
        {
            return path;
        }

        string? resolved = fontContext.ResolveFontFallback(fontName, name =>
        {
            string? candidate = fontContext.ResolveFontPath(name);
            return candidate is not null && File.Exists(candidate);
        });
        if (resolved is not null)
        {
            string? candidate = fontContext.ResolveFontPath(resolved);
            if (candidate is not null && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private PdfFontResource? TryGetFirstAvailable()
    {
        foreach (string name in new[] { "Arial", "Courier New", "Liberation Sans", "DejaVu Sans" })
        {
            PdfFontResource? font = TryGet(name);
            if (font is not null)
            {
                return font;
            }
        }

        foreach (string directory in new[] { @"C:\Windows\Fonts", "/usr/share/fonts", "/usr/local/share/fonts", "/Library/Fonts" })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*.ttf", SearchOption.AllDirectories))
            {
                PdfFontResource? font = LoadPath(file);
                if (font is not null)
                {
                    return font;
                }
            }
        }

        return null;
    }

    private PdfFontResource? LoadPath(string path)
    {
        if (_byPath.TryGetValue(path, out PdfFontResource? existing))
        {
            return existing;
        }

        SfntFont? font;
        try
        {
            font = SfntFont.TryLoad(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (font is null)
        {
            return null;
        }

        var resource = new PdfFontResource(font, "F" + (_all.Count + 1).ToString(CultureInfo.InvariantCulture));
        _byPath[path] = resource;
        _all.Add(resource);
        return resource;
    }
}

using System.Runtime.InteropServices;
using System.Text;
using OdfKit.Core;
using OdfKit.Export;
using OdfKit.Export.PdfInternal;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證完全託管的 PDF 匯出：文字（含中日韓斷行禁則）、清單、表格、連結、圖片與字型子集，
/// 以專案自己的 PDF 讀取器與 ToUnicode 對照擷取結果，不依賴外部 PDF 函式庫。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class PdfManagedExporterTests
{
    private const string Namespaces = """
        xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
        xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
        xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
        xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
        xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"
        xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
        xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
        xmlns:xlink="http://www.w3.org/1999/xlink"
        xmlns:dc="http://purl.org/dc/elements/1.1/"
        """;

    // 1x1 白色 JPEG（單一灰階元件）。
    private const string TinyJpegBase64 =
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=";

    private static TextDocument LoadFlat(string body, string meta = "")
    {
        string xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document {Namespaces} office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.text">
              <office:meta>{meta}</office:meta>
              <office:body><office:text>{body}</office:text></office:body>
            </office:document>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return (TextDocument)OdfDocument.Load(stream, "test.fodt");
    }

    private static (PdfFile File, byte[] Bytes) Export(TextDocument document)
    {
        using var stream = new MemoryStream();
        OdfPdfExporter.ExportToStream(document, stream);
        byte[] bytes = stream.ToArray();
        PdfFile file = PdfFile.Open(bytes);
        Assert.False(file.Rebuilt, "匯出的 PDF 交叉參照表必須正確，不應靠重建掃描才讀得到。");
        return (file, bytes);
    }

    private static bool HasCjkFont()
    {
        string name = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "PingFang TC"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Noto Sans CJK TC" : "DFKai-SB";
        string? path = OdfFontContext.Default.ResolveFontPath(name);
        return path is not null && File.Exists(path);
    }

    private static string AllText(PdfFile file) =>
        string.Concat(PdfTestSupport.Pages(file).SelectMany(page => PdfTestSupport.Lines(file, page)));

    private static string Compact(string value) => new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    [Fact]
    public void FootnotesFollowTheirParagraphAndEndnotesGoToTheEnd()
    {
        using TextDocument document = LoadFlat(
            """
            <text:p>Alpha<text:note text:id="f1" text:note-class="footnote"><text:note-citation>1</text:note-citation><text:note-body><text:p>FootnoteBody</text:p></text:note-body></text:note> beta<text:note text:id="e1" text:note-class="endnote"><text:note-citation>i</text:note-citation><text:note-body><text:p>EndnoteBody</text:p></text:note-body></text:note>.</text:p>
            <text:p>SecondParagraph</text:p>
            """);
        (PdfFile file, _) = Export(document);
        string text = Compact(AllText(file));

        int alpha = text.IndexOf("Alpha1beta", StringComparison.Ordinal);
        int footnote = text.IndexOf("1FootnoteBody", StringComparison.Ordinal);
        int second = text.IndexOf("SecondParagraph", StringComparison.Ordinal);
        int endnote = text.IndexOf("iEndnoteBody", StringComparison.Ordinal);

        Assert.True(alpha >= 0, text);
        Assert.True(footnote > alpha && footnote < second, text);
        Assert.True(endnote > second, text);
    }

    [Fact]
    public void EmptyDocumentProducesOneValidPage()
    {
        using TextDocument document = TextDocument.Create();
        (PdfFile file, byte[] bytes) = Export(document);

        Assert.Single(PdfTestSupport.Pages(file));
        Assert.StartsWith("%PDF-1.7", Encoding.ASCII.GetString(bytes, 0, 8), StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", Encoding.ASCII.GetString(bytes, bytes.Length - 6, 6), StringComparison.Ordinal);
    }

    [Fact]
    public void ExportedTextRoundTripsThroughToUnicode()
    {
        using TextDocument document = TextDocument.Create();
        document.AddHeading("Quarterly Report", 1);
        document.AddParagraph("The quick brown fox jumps over the lazy dog 0123456789.");
        if (HasCjkFont())
        {
            document.AddParagraph("這是臺灣正體中文的測試內容，包含標點符號與 English words。");
        }

        (PdfFile file, _) = Export(document);
        string text = Compact(AllText(file));

        Assert.Contains("QuarterlyReport", text, StringComparison.Ordinal);
        Assert.Contains("Thequickbrownfoxjumpsoverthelazydog0123456789.", text, StringComparison.Ordinal);
        if (HasCjkFont())
        {
            Assert.Contains("這是臺灣正體中文的測試內容，包含標點符號與Englishwords。", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LongCjkParagraphWrapsWithoutClosingPunctuationAtLineStart()
    {
        if (!HasCjkFont())
        {
            Assert.Skip("找不到預設中文字型，略過中日韓斷行測試。");
        }

        string sentence = "標點不應出現在行首，「引號」與（括號）也一樣。開括號不應出現在行尾；臺灣正體中文的排版需要處理禁則。";
        using TextDocument document = TextDocument.Create();
        document.AddParagraph(string.Concat(Enumerable.Repeat(sentence, 8)));

        (PdfFile file, _) = Export(document);
        IReadOnlyList<string> lines = PdfTestSupport.Lines(file, PdfTestSupport.Pages(file)[0]);

        Assert.True(lines.Count > 3, "長段落應該自動換成多行。");
        foreach (string line in lines)
        {
            Assert.False("，。、．：；！？）］｝〕〉》」』】".Contains(line[0]), $"行首不應是結束標點：{line}");
            Assert.False("（［｛〔〈《「『【".Contains(line[^1]), $"行尾不應是開始標點：{line}");
        }
    }

    [Fact]
    public void LongLatinParagraphWrapsAtWordBoundaries()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph(string.Join(' ', Enumerable.Range(0, 120).Select(i => "word" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        (PdfFile file, _) = Export(document);
        IReadOnlyList<string> lines = PdfTestSupport.Lines(file, PdfTestSupport.Pages(file)[0]);

        Assert.True(lines.Count > 2);
        foreach (string line in lines)
        {
            // 每行的最後一個詞必須完整（以數字結尾的完整 wordN），不會在詞中被切斷。
            Assert.Matches(@"word\d+\s*$", line);
        }
    }

    [Fact]
    public void ContentThatDoesNotFitFlowsOntoFurtherPages()
    {
        using TextDocument document = TextDocument.Create();
        for (int i = 0; i < 120; i++)
        {
            document.AddParagraph("Paragraph number " + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " with some filler text.");
        }

        (PdfFile file, _) = Export(document);
        IReadOnlyList<PdfDict> pages = PdfTestSupport.Pages(file);

        Assert.True(pages.Count >= 2);
        string text = Compact(AllText(file));
        Assert.Contains("Paragraphnumber0withsomefillertext.", text, StringComparison.Ordinal);
        Assert.Contains("Paragraphnumber119withsomefillertext.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void HyperlinksBecomeUriAnnotationsAndUnsafeSchemesAreDropped()
    {
        using TextDocument document = LoadFlat("""
            <text:p>Visit <text:a xlink:href="https://example.org/a?x=1&amp;y=2">the site</text:a>,
            <text:a xlink:href="mailto:someone@example.org">mail</text:a> and
            <text:a xlink:href="javascript:alert(1)">evil</text:a>.</text:p>
            """);
        (PdfFile file, byte[] bytes) = Export(document);
        PdfDict page = PdfTestSupport.Pages(file)[0];

        List<object?>? annotations = file.ResolveArray(page["Annots"]);
        Assert.NotNull(annotations);
        string[] uris = annotations!
            .Select(item => file.ResolveDict(item)!)
            .Select(annotation => file.ResolveDict(annotation["A"])!)
            .Select(action => ((PdfString)file.Resolve(action["URI"])!).Text())
            .ToArray();

        Assert.Equal(["https://example.org/a?x=1&y=2", "mailto:someone@example.org"], uris);
        Assert.DoesNotContain("javascript", Encoding.Latin1.GetString(bytes), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evil", Compact(AllText(file)), StringComparison.Ordinal);
    }

    [Fact]
    public void ListsKeepLabelsAndNesting()
    {
        using TextDocument document = LoadFlat("""
            <text:list><text:list-item><text:p>First item</text:p>
              <text:list><text:list-item><text:p>Nested item</text:p></text:list-item></text:list>
            </text:list-item><text:list-item><text:p>Second item</text:p></text:list-item></text:list>
            """);
        (PdfFile file, _) = Export(document);
        IReadOnlyList<string> lines = PdfTestSupport.Lines(file, PdfTestSupport.Pages(file)[0]);

        Assert.Equal(3, lines.Count);
        Assert.Contains("First item", lines[0], StringComparison.Ordinal);
        Assert.Contains("Nested item", lines[1], StringComparison.Ordinal);
        Assert.Contains("Second item", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void TableHeaderRowsRepeatOnEveryPage()
    {
        string rows = string.Concat(Enumerable.Range(1, 90).Select(i =>
            $"<table:table-row><table:table-cell><text:p>row{i}</text:p></table:table-cell><table:table-cell><text:p>value{i}</text:p></table:table-cell></table:table-row>"));
        using TextDocument document = LoadFlat($"""
            <table:table table:name="T"><table:table-column table:number-columns-repeated="2"/>
              <table:table-header-rows><table:table-row><table:table-cell><text:p>HeaderA</text:p></table:table-cell><table:table-cell><text:p>HeaderB</text:p></table:table-cell></table:table-row></table:table-header-rows>
              {rows}
            </table:table>
            """);
        (PdfFile file, _) = Export(document);
        IReadOnlyList<PdfDict> pages = PdfTestSupport.Pages(file);

        Assert.True(pages.Count >= 2, "九十列的表格必須跨頁。");
        foreach (PdfDict page in pages)
        {
            Assert.Contains("HeaderA", Compact(string.Concat(PdfTestSupport.Lines(file, page))), StringComparison.Ordinal);
        }

        string text = Compact(AllText(file));
        Assert.Contains("row1value1", text, StringComparison.Ordinal);
        Assert.Contains("row90value90", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MergedCellsKeepTheirTextAndTableStaysReadable()
    {
        using TextDocument document = LoadFlat("""
            <table:table table:name="T"><table:table-column table:number-columns-repeated="3"/>
              <table:table-row>
                <table:table-cell table:number-columns-spanned="2"><text:p>spanned across two columns</text:p></table:table-cell><table:covered-table-cell/>
                <table:table-cell table:number-rows-spanned="2"><text:p>tall cell</text:p></table:table-cell></table:table-row>
              <table:table-row><table:table-cell><text:p>left</text:p></table:table-cell><table:table-cell><text:p>middle</text:p></table:table-cell><table:covered-table-cell/></table:table-row>
            </table:table>
            """);
        (PdfFile file, _) = Export(document);
        string text = Compact(AllText(file));

        Assert.Contains("spannedacrosstwocolumns", text, StringComparison.Ordinal);
        Assert.Contains("tallcell", text, StringComparison.Ordinal);
        Assert.Contains("left", text, StringComparison.Ordinal);
        Assert.Contains("middle", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CellTallerThanOnePageIsSplitWithoutLosingText()
    {
        string words = string.Join(' ', Enumerable.Range(0, 1500).Select(i => "alpha"));
        using TextDocument document = LoadFlat($"""
            <table:table table:name="Tall"><table:table-column table:number-columns-repeated="2"/>
              <table:table-row><table:table-cell><text:p>{words}</text:p></table:table-cell><table:table-cell><text:p>short</text:p></table:table-cell></table:table-row>
            </table:table>
            """);
        (PdfFile file, _) = Export(document);

        Assert.True(PdfTestSupport.Pages(file).Count >= 2);
        string text = AllText(file);
        Assert.Equal(1500, text.Split("alpha").Length - 1);
    }

    [Fact]
    public void ImagesAreEmbeddedOncePerDistinctContent()
    {
        byte[] png = BuildPng(width: 4, height: 2, colorType: 6);
        string pngBase64 = Convert.ToBase64String(png);
        string jpeg = TinyJpegBase64;
        using TextDocument document = LoadFlat($"""
            <text:p>one <draw:frame svg:width="2cm" svg:height="1cm"><draw:image><office:binary-data>{pngBase64}</office:binary-data></draw:image></draw:frame>
            two <draw:frame svg:width="2cm" svg:height="1cm"><draw:image><office:binary-data>{pngBase64}</office:binary-data></draw:image></draw:frame>
            three <draw:frame svg:width="2cm"><draw:image><office:binary-data>{jpeg}</office:binary-data></draw:image></draw:frame></text:p>
            """);
        (PdfFile file, _) = Export(document);
        PdfDict page = PdfTestSupport.Pages(file)[0];
        PdfDict resources = file.ResolveDict(page["Resources"])!;
        PdfDict images = file.ResolveDict(resources["XObject"])!;

        Assert.Equal(2, images.Count);
        PdfDict[] dictionaries = images.Values.Select(value => ((PdfStream)file.Resolve(value)!).Dictionary).ToArray();
        Assert.Contains(dictionaries, d => d.ContainsKey("SMask"));
        Assert.Contains(dictionaries, d => d.TryGetValue("Filter", out object? f) && f is PdfName { Value: "DCTDecode" });
    }

    [Fact]
    public void UnreadableImageIsSkippedWithoutFailingTheExport()
    {
        string garbage = Convert.ToBase64String(Encoding.ASCII.GetBytes("this is not an image"));
        using TextDocument document = LoadFlat($"""
            <text:p>before <draw:frame svg:width="2cm"><draw:image><office:binary-data>{garbage}</office:binary-data></draw:image></draw:frame> after</text:p>
            """);
        (PdfFile file, _) = Export(document);

        Assert.Contains("beforeafter", Compact(AllText(file)), StringComparison.Ordinal);
    }

    [Fact]
    public void FontsAreSubsetSoTheFileStaysSmall()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Subset me please.");
        (_, byte[] bytes) = Export(document);

        Assert.True(bytes.Length < 250_000, $"內嵌字型子集後的 PDF 應遠小於整份字型檔，實際 {bytes.Length} 位元組。");
    }

    [Fact]
    public void DocumentTitleAndProducerAreWrittenToTheInfoDictionary()
    {
        using TextDocument document = LoadFlat("<text:p>body</text:p>", "<dc:title>My Title 標題</dc:title>");
        (PdfFile file, _) = Export(document);
        PdfDict info = file.ResolveDict(file.Trailer["Info"])!;

        Assert.Equal("My Title 標題", ((PdfString)file.Resolve(info["Title"])!).Text());
        Assert.Equal("OdfKit", ((PdfString)file.Resolve(info["Producer"])!).Text());
    }

    [Fact]
    public void RunsAndSpacesAndTabsAndLineBreaksAreKept()
    {
        using TextDocument document = LoadFlat("""
            <text:p>alpha<text:tab/>beta<text:line-break/>gamma<text:s text:c="3"/>delta</text:p>
            """);
        (PdfFile file, _) = Export(document);
        IReadOnlyList<string> lines = PdfTestSupport.Lines(file, PdfTestSupport.Pages(file)[0]);

        Assert.Equal(2, lines.Count);
        Assert.Equal("alphabeta", Compact(lines[0]));
        Assert.Equal("gammadelta", Compact(lines[1]));
        Assert.Contains("   ", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ExportToStreamLeavesPositionAtEndAndReportsLength()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("report");
        using var stream = new MemoryStream();

        OdfExportReport report = OdfPdfExporter.ExportToStream(document, stream);

        Assert.Equal(stream.Length, report.BytesWritten);
        Assert.Equal(stream.Length, stream.Position);
    }

    // ---------- 字型與影像單元 ----------

    [Fact]
    public void FontSubsetKeepsUsedGlyphsAndShrinksTheFont()
    {
        string? path = FindLatinFontPath();
        if (path is null)
        {
            Assert.Skip("找不到可用的 TrueType 字型。");
        }

        byte[] original = File.ReadAllBytes(path);
        SfntFont? font = SfntFont.TryLoad(original);
        Assert.NotNull(font);
        if (!font!.CanSubset)
        {
            Assert.Skip("測試用字型是 CFF 輪廓，無法子集化。");
        }

        int glyphA = font.GetGlyph('A');
        int glyphB = font.GetGlyph('B');
        Assert.NotEqual(0, glyphA);
        byte[] subset = font.BuildSubset([glyphA, glyphB]);

        SfntFont? reloaded = SfntFont.TryLoad(subset);
        Assert.NotNull(reloaded);
        Assert.Equal(font.GetAdvance(glyphA), reloaded!.GetAdvance(glyphA));
        Assert.Equal(font.GetAdvance(glyphB), reloaded.GetAdvance(glyphB));
        Assert.Equal(font.UnitsPerEm, reloaded.UnitsPerEm);
        Assert.True(subset.Length < original.Length / 2 || original.Length < 200_000);
    }

    [Fact]
    public void FontLookupFindsGlyphsAndAdvances()
    {
        string? path = FindLatinFontPath();
        if (path is null)
        {
            Assert.Skip("找不到可用的 TrueType 字型。");
        }

        SfntFont? font = SfntFont.TryLoad(File.ReadAllBytes(path));
        Assert.NotNull(font);
        Assert.NotEqual(0, font!.GetGlyph('A'));
        Assert.True(font.GetAdvance(font.GetGlyph('A')) > 0);
        Assert.Equal(0, font.GetGlyph(0x10FFFF));
        Assert.False(string.IsNullOrWhiteSpace(font.PostScriptName));
    }

    [Fact]
    public void PngDecoderHandlesColorTypesAndTransparency()
    {
        PdfImage? rgba = PdfImage.TryLoad(BuildPng(4, 2, colorType: 6));
        Assert.NotNull(rgba);
        Assert.Equal(4, rgba!.Width);
        Assert.Equal(2, rgba.Height);
        Assert.NotNull(rgba.Alpha);

        PdfImage? rgb = PdfImage.TryLoad(BuildPng(3, 3, colorType: 2));
        Assert.NotNull(rgb);
        Assert.Null(rgb!.Alpha);
        Assert.Equal(27, rgb.Data.Length);

        PdfImage? gray = PdfImage.TryLoad(BuildPng(5, 1, colorType: 0));
        Assert.NotNull(gray);
        Assert.Equal(15, gray!.Data.Length);
    }

    [Fact]
    public void ImageDecodersRejectTruncatedAndUnknownData()
    {
        Assert.Null(PdfImage.TryLoad([]));
        Assert.Null(PdfImage.TryLoad(Encoding.ASCII.GetBytes("GIF89a....")));
        byte[] png = BuildPng(4, 4, colorType: 6);
        Assert.Null(PdfImage.TryLoad(png.AsSpan(0, png.Length / 2).ToArray()));
        Assert.NotNull(PdfImage.TryLoad(Convert.FromBase64String(TinyJpegBase64)));
    }

    [Fact]
    public void DeflateRoundTripsAndInflateRejectsGarbage()
    {
        byte[] data = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("compress me ", 200)));
        byte[] packed = PdfCore.Deflate(data);

        Assert.True(packed.Length < data.Length);
        Assert.Equal(data, PdfCore.Inflate(packed, 0, packed.Length));
        Assert.Null(PdfCore.Inflate([1, 2, 3, 4, 5, 6], 0, 6));
    }

    [Fact]
    public void TextStringsAndNamesAreEscaped()
    {
        Assert.Equal(@"(a\(b\)\\c)", PdfCore.TextString(@"a(b)\c"));
        Assert.Equal("<FEFF4E2D6587>", PdfCore.TextString("中文"));
        Assert.Equal("/A#20B#2F", PdfCore.Name("A B/"));
    }

    private static string? FindLatinFontPath()
    {
        foreach (string name in new[] { "Arial", "Liberation Sans", "DejaVu Sans", "Noto Sans", "Segoe UI" })
        {
            string? path = OdfFontContext.Default.ResolveFontPath(name);
            if (path is not null && File.Exists(path) && !path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    // 組出最小的非交錯 PNG（8 位元）；CRC 欄位填 0，解碼器不驗證。
    private static byte[] BuildPng(int width, int height, int colorType)
    {
        int channels = colorType switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 1 };
        var raw = new List<byte>();
        for (int y = 0; y < height; y++)
        {
            raw.Add(0);
            for (int x = 0; x < width * channels; x++)
            {
                raw.Add((byte)((x * 37) + (y * 11)));
            }
        }

        using var output = new MemoryStream();
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(output, "IHDR", [.. BigEndian(width), .. BigEndian(height), 8, (byte)colorType, 0, 0, 0]);
        WriteChunk(output, "IDAT", PdfCore.Deflate([.. raw]));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        output.Write(BigEndian(data.Length));
        output.Write(Encoding.ASCII.GetBytes(type));
        output.Write(data);
        output.Write([0, 0, 0, 0]);
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OdfKit.Export.PdfInternal;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 測試用的 PDF 檢視輔助：以專案自己的讀取器走訪頁面，並依 ToUnicode 對照擷取文字，
/// 不依賴任何外部 PDF 函式庫。
/// </summary>
internal static class PdfTestSupport
{
    internal sealed record TextRun(double X, double Y, string Text);

    internal static string FindFixture(string name)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "OdfKit.slnx")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!, "tests", "fixtures", "pdf", name);
    }

    internal static IReadOnlyList<PdfDict> Pages(PdfFile file)
    {
        var pages = new List<PdfDict>();
        PdfDict? catalog = file.ResolveDict(file.Trailer["Root"]);
        Assert.NotNull(catalog);
        Collect(file, file.ResolveDict(catalog!["Pages"]), pages, 0);
        return pages;
    }

    private static void Collect(PdfFile file, PdfDict? node, List<PdfDict> pages, int depth)
    {
        if (node is null || depth > 32)
        {
            return;
        }

        if (node.TryGetValue("Kids", out object? kidsValue) && file.ResolveArray(kidsValue) is List<object?> kids)
        {
            foreach (object? kid in kids)
            {
                Collect(file, file.ResolveDict(kid), pages, depth + 1);
            }
        }
        else
        {
            pages.Add(node);
        }
    }

    /// <summary>
    /// 依頁面順序回傳每個文字片段（位置與文字）。
    /// </summary>
    internal static IReadOnlyList<TextRun> ExtractRuns(PdfFile file, PdfDict page)
    {
        var runs = new List<TextRun>();
        var unicodeByFont = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
        PdfDict? resources = file.ResolveDict(page["Resources"]);
        PdfDict? fonts = resources is not null && resources.TryGetValue("Font", out object? fontValue) ? file.ResolveDict(fontValue) : null;
        if (fonts is not null)
        {
            foreach (KeyValuePair<string, object?> font in fonts)
            {
                PdfDict? type0 = file.ResolveDict(font.Value);
                if (type0 is not null && type0.TryGetValue("ToUnicode", out object? toUnicode)
                    && file.Resolve(toUnicode) is PdfStream stream
                    && file.DecodeStream(stream) is byte[] data)
                {
                    unicodeByFont[font.Key] = ParseToUnicode(Encoding.ASCII.GetString(data));
                }
            }
        }

        string content = ReadContent(file, page);
        var pattern = new Regex(@"/(?<font>\w+) [\d.]+ Tf .*? 1 0 [\d.]+ 1 (?<x>-?[\d.]+) (?<y>-?[\d.]+) Tm <(?<hex>[0-9A-F]*)> Tj", RegexOptions.None, TimeSpan.FromSeconds(5));
        foreach (Match match in pattern.Matches(content))
        {
            string fontName = match.Groups["font"].Value;
            string hex = match.Groups["hex"].Value;
            var text = new StringBuilder();
            for (int i = 0; i + 4 <= hex.Length; i += 4)
            {
                int glyph = int.Parse(hex.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                text.Append(unicodeByFont.TryGetValue(fontName, out Dictionary<int, string>? map) && map.TryGetValue(glyph, out string? value)
                    ? value
                    : "?");
            }

            runs.Add(new TextRun(
                double.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture),
                text.ToString()));
        }

        return runs;
    }

    /// <summary>
    /// 把同一基線的片段依 X 排序後串成一行，由上而下回傳。
    /// </summary>
    internal static IReadOnlyList<string> Lines(PdfFile file, PdfDict page)
    {
        return ExtractRuns(file, page)
            .GroupBy(run => Math.Round(run.Y, 1))
            .OrderByDescending(group => group.Key)
            .Select(group => string.Concat(group.OrderBy(run => run.X).Select(run => run.Text)))
            .ToList();
    }

    private static string ReadContent(PdfFile file, PdfDict page)
    {
        var builder = new StringBuilder();
        object? contents = file.Resolve(page["Contents"]);
        var streams = contents is List<object?> list ? list : [page["Contents"]];
        foreach (object? item in streams)
        {
            if (file.Resolve(item) is PdfStream stream && file.DecodeStream(stream) is byte[] data)
            {
                builder.Append(Encoding.ASCII.GetString(data)).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static Dictionary<int, string> ParseToUnicode(string cmap)
    {
        var map = new Dictionary<int, string>();
        foreach (Match match in Regex.Matches(cmap, @"<(?<g>[0-9A-F]{4})> <(?<u>[0-9A-F]{4,8})>", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            int glyph = int.Parse(match.Groups["g"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            string hex = match.Groups["u"].Value;
            var text = new StringBuilder();
            for (int i = 0; i + 4 <= hex.Length; i += 4)
            {
                text.Append((char)int.Parse(hex.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }

            map[glyph] = text.ToString();
        }

        return map;
    }
}

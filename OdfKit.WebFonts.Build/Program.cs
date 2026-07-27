using OdfKit.Compliance;
using OdfKit.WebFonts;
using OdfKit.WebFonts.Build;
using OdfKit.WebFonts.Windows;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        Console.WriteLine("odfkit-webfonts build (--font <path> | --eudc-code-page <id> [--eudc-typeface <name>]) (--text <path> | --content-root <dir>) --output <dir> [--content-extensions .cshtml,.razor,.aspx,.resx,.html,.txt] [--family <name>] [--profile <id>] [--formats woff2,woff,ttf] [--browser-targets chromium,firefox,webkit] [--font-display auto|block|swap|fallback|optional] [--slice-size <codepoints>] [--max-slices <count>] [--fallback-family <name> --fallback-local <name> --size-adjust <percent> --ascent-override <percent> --descent-override <percent> --line-gap-override <percent>] [--face <index>] [--encoding utf-8|big5|big5e|json-profile|euc-tw] [--big5e-map <path>] [--json-profile <path>] [--cns-mapping-archive <path>] [--max-corpus-bytes <bytes>] [--max-scalars <count>] [--max-source-bytes <bytes>] [--max-output-bytes <bytes>] [--skip-source-checksums true|false]");
        return 0;
    }

    if (!string.Equals(args[0], "build", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Unknown command.");
        return 2;
    }

    try
    {
        IReadOnlyDictionary<string, string> values = ParseArguments(args.Skip(1).ToArray());
        IReadOnlyList<string> contentPaths = DiscoverContentPaths(values);
        var options = new WebFontBuildOptions
        {
            FontPath = ResolveFontPath(values),
            TextPath = GetOptional(values, "text") ?? string.Empty,
            ContentPaths = contentPaths,
            MaxCorpusBytes = long.Parse(
                Get(values, "max-corpus-bytes", (16L * 1024 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                System.Globalization.CultureInfo.InvariantCulture),
            MaxUniqueUnicodeScalars = int.Parse(
                Get(values, "max-scalars", "100000"),
                System.Globalization.CultureInfo.InvariantCulture),
            UnicodeRangeSliceSize = int.Parse(
                Get(values, "slice-size", "0"),
                System.Globalization.CultureInfo.InvariantCulture),
            MaxSliceCount = int.Parse(
                Get(values, "max-slices", "512"),
                System.Globalization.CultureInfo.InvariantCulture),
            OutputDirectory = Required(values, "output"),
            FontFamily = Get(values, "family", "OdfKitWebFont"),
            FontDisplay = ParseFontDisplay(Get(values, "font-display", "swap")),
            FallbackMetrics = ParseFallbackMetrics(values),
            ProfileId = Get(values, "profile", "default"),
            FontSourceId = Get(values, "font-id", "source"),
            FaceIndex = int.Parse(Get(values, "face", "0"), System.Globalization.CultureInfo.InvariantCulture),
            LegacyEncoding = GetOptional(values, "encoding"),
            Big5EMappingPath = GetOptional(values, "big5e-map"),
            JsonProfilePath = GetOptional(values, "json-profile"),
            CnsMappingArchivePath = GetOptional(values, "cns-mapping-archive"),
            MaxSourceBytes = long.Parse(
                Get(values, "max-source-bytes", (256L * 1024 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                System.Globalization.CultureInfo.InvariantCulture),
            MaxOutputBytes = long.Parse(
                Get(values, "max-output-bytes", (32L * 1024 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                System.Globalization.CultureInfo.InvariantCulture),
            ValidateSourceChecksums = !bool.Parse(Get(values, "skip-source-checksums", "false")),
            Formats = ParseFormats(Get(values, "formats", "woff2")),
            RequiredBrowserTargets = ParseBrowserTargets(Get(values, "browser-targets", string.Empty))
        };
        WebFontManifest manifest = await new WebFontAssetBuilder().BuildAsync(options).ConfigureAwait(false);
        Console.WriteLine($"Generated {manifest.Assets.Count} assets for profile '{manifest.ProfileId}'.");
        return 0;
    }
    // FormatException 與 OverflowException 衍生自 SystemException 而非 ArgumentException：
    // 先前未攔截，使用者只要在數值選項打錯一個字（例如 --face x）就會得到未處理例外
    // 的堆疊追蹤，而不是乾淨的錯誤訊息與非零結束碼。
    catch (Exception exception) when (
        exception is ArgumentException
            or InvalidDataException
            or IOException
            or NotSupportedException
            or FormatException
            or OverflowException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static IReadOnlyDictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int index = 0; index < args.Length; index += 2)
    {
        if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        result.Add(args[index][2..], args[index + 1]);
    }

    return result;
}

static string Required(IReadOnlyDictionary<string, string> values, string name)
    => values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));

static string Get(IReadOnlyDictionary<string, string> values, string name, string defaultValue)
    => values.TryGetValue(name, out string? value) ? value : defaultValue;

static string? GetOptional(IReadOnlyDictionary<string, string> values, string name)
    => values.TryGetValue(name, out string? value) ? value : null;

static string ResolveFontPath(IReadOnlyDictionary<string, string> values)
{
    string? explicitPath = GetOptional(values, "font");
    string? codePageValue = GetOptional(values, "eudc-code-page");
    if (!string.IsNullOrWhiteSpace(explicitPath) == !string.IsNullOrWhiteSpace(codePageValue))
    {
        throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
    }

    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return explicitPath;
    }

    int codePage = int.Parse(codePageValue!, System.Globalization.CultureInfo.InvariantCulture);
    string? typeface = GetOptional(values, "eudc-typeface");
    return string.IsNullOrWhiteSpace(typeface)
        ? WindowsEudcFontSourceResolver.ResolveSystemDefaultFont(codePage)
        : WindowsEudcFontSourceResolver.ResolveAssociatedFont(codePage, typeface);
}

static IReadOnlyList<WebFontFormat> ParseFormats(string value)
    => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => item.ToLowerInvariant() switch
        {
            "woff2" => WebFontFormat.Woff2,
            "woff" => WebFontFormat.Woff,
            "ttf" => WebFontFormat.TrueType,
            // CFF 來源字型的引擎會拒絕 TrueType 輸出，因此少了 otf 就完全無法從 CLI
            // 產生獨立 OTF——儘管架構文件把 OTF 列為產品輸出格式。
            "otf" => WebFontFormat.OpenType,
            _ => throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"))
        })
        .ToArray();

static IReadOnlyList<WebFontBrowserTarget> ParseBrowserTargets(string value)
    => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => item.ToLowerInvariant() switch
        {
            "chromium" => WebFontBrowserTarget.Chromium,
            "firefox" => WebFontBrowserTarget.Firefox,
            "webkit" => WebFontBrowserTarget.WebKit,
            _ => throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"))
        })
        .Distinct()
        .ToArray();

static WebFontDisplayMode ParseFontDisplay(string value)
    => value.ToLowerInvariant() switch
    {
        "auto" => WebFontDisplayMode.Auto,
        "block" => WebFontDisplayMode.Block,
        "swap" => WebFontDisplayMode.Swap,
        "fallback" => WebFontDisplayMode.Fallback,
        "optional" => WebFontDisplayMode.Optional,
        _ => throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"))
    };

static WebFontFallbackMetrics? ParseFallbackMetrics(IReadOnlyDictionary<string, string> values)
{
    string? localName = GetOptional(values, "fallback-local");
    if (string.IsNullOrWhiteSpace(localName))
    {
        return null;
    }

    return new WebFontFallbackMetrics
    {
        FontFamily = Get(values, "fallback-family", "OdfKitWebFontFallback"),
        LocalFontName = localName,
        SizeAdjustPercentage = ParsePercentage(values, "size-adjust", 100),
        AscentOverridePercentage = ParsePercentage(values, "ascent-override", 100),
        DescentOverridePercentage = ParsePercentage(values, "descent-override", 20),
        LineGapOverridePercentage = ParsePercentage(values, "line-gap-override", 0)
    };
}

static double ParsePercentage(IReadOnlyDictionary<string, string> values, string name, double defaultValue)
    => double.Parse(
        Get(values, name, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        System.Globalization.CultureInfo.InvariantCulture);

static IReadOnlyList<string> DiscoverContentPaths(IReadOnlyDictionary<string, string> values)
{
    string? root = GetOptional(values, "content-root");
    if (string.IsNullOrWhiteSpace(root))
    {
        return Array.Empty<string>();
    }

    string fullRoot = Path.GetFullPath(root);
    if (!Directory.Exists(fullRoot))
    {
        throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
    }

    HashSet<string> extensions = Get(
            values,
            "content-extensions",
            ".txt,.html,.htm,.cshtml,.razor,.aspx,.ascx,.master,.resx,.json,.xml,.csv,.md")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (extensions.Count == 0 || extensions.Count > 64)
    {
        throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
    }

    return Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
        .Where(path => extensions.Contains(Path.GetExtension(path)))
        .Where(path => !HasExcludedSegment(fullRoot, path))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();
}

static bool HasExcludedSegment(string root, string path)
{
    string relative = Path.GetRelativePath(root, path);
    return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "bin" or "obj" or ".git" or "node_modules");
}

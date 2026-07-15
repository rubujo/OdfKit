using OdfKit.Compliance;
using OdfKit.WebFonts;
using OdfKit.WebFonts.Build;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        Console.WriteLine("odfkit-webfonts build --font <path> (--text <path> | --content-root <dir>) --output <dir> [--content-extensions .cshtml,.razor,.aspx,.resx,.html,.txt] [--family <name>] [--profile <id>] [--formats woff2,woff,ttf,otf] [--face <index>] [--encoding utf-8|big5|big5e|json-profile|euc-tw] [--big5e-map <path>] [--json-profile <path>] [--cns-mapping-archive <path>] [--max-corpus-bytes <bytes>] [--max-scalars <count>] [--pyftsubset <path>] [--fonttools-pythonpath <path>]");
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
            FontPath = Required(values, "font"),
            TextPath = GetOptional(values, "text") ?? string.Empty,
            ContentPaths = contentPaths,
            MaxCorpusBytes = long.Parse(
                Get(values, "max-corpus-bytes", (16L * 1024 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                System.Globalization.CultureInfo.InvariantCulture),
            MaxUniqueUnicodeScalars = int.Parse(
                Get(values, "max-scalars", "100000"),
                System.Globalization.CultureInfo.InvariantCulture),
            OutputDirectory = Required(values, "output"),
            FontFamily = Get(values, "family", "OdfKitWebFont"),
            ProfileId = Get(values, "profile", "default"),
            FontSourceId = Get(values, "font-id", "source"),
            FaceIndex = int.Parse(Get(values, "face", "0"), System.Globalization.CultureInfo.InvariantCulture),
            LegacyEncoding = GetOptional(values, "encoding"),
            Big5EMappingPath = GetOptional(values, "big5e-map"),
            JsonProfilePath = GetOptional(values, "json-profile"),
            CnsMappingArchivePath = GetOptional(values, "cns-mapping-archive"),
            FontToolsExecutable = Get(values, "pyftsubset", "pyftsubset"),
            FontToolsPythonModulePath = GetOptional(values, "fonttools-pythonpath"),
            Formats = ParseFormats(Get(values, "formats", "woff2,woff"))
        };
        WebFontManifest manifest = await new WebFontAssetBuilder().BuildAsync(options).ConfigureAwait(false);
        Console.WriteLine($"Generated {manifest.Assets.Count} assets for profile '{manifest.ProfileId}'.");
        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException)
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

static IReadOnlyList<WebFontFormat> ParseFormats(string value)
    => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => item.ToLowerInvariant() switch
        {
            "woff2" => WebFontFormat.Woff2,
            "woff" => WebFontFormat.Woff,
            "ttf" => WebFontFormat.TrueType,
            "otf" => WebFontFormat.OpenType,
            _ => throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"))
        })
        .ToArray();

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
        .Select(extension => extension.StartsWith(".", StringComparison.Ordinal) ? extension : $".{extension}")
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

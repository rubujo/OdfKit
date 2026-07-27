using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OdfKit.Compliance;
using OdfKit.WebFonts.Encoding.Legacy;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Profiles;

namespace OdfKit.WebFonts.Build;

/// <summary>
/// Builds static immutable WebFont assets, a manifest, and CSS.
/// 建置靜態不可變 WebFont 資產、manifest 與 CSS。
/// </summary>
public sealed class WebFontAssetBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Runs one build-time job.
    /// 執行單一建置期工作。
    /// </summary>
    /// <param name="options">The trusted build options. / 受信任的建置設定。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The generated manifest. / 產生的 manifest。</returns>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The public builder instance is an extensibility seam and changing this established method to static would break callers.")]
    public async Task<WebFontManifest> BuildAsync(
        WebFontBuildOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        string fontPath = Path.GetFullPath(options.FontPath);
        string outputDirectory = Path.GetFullPath(options.OutputDirectory);
        string text = await ReadTextAsync(options, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<WebFontTextSequence> sequences = SelectUniqueSequences(
            text,
            options.MaxUniqueUnicodeScalars);
        string sourceSha256 = ComputeSha256(fontPath);

        var engineOptions = new ManagedOpenTypeWebFontEngineOptions
        {
            MaxUnicodeScalars = options.MaxUniqueUnicodeScalars,
            MaxSourceBytes = options.MaxSourceBytes,
            MaxOutputBytes = options.MaxOutputBytes,
            ValidateSourceChecksums = options.ValidateSourceChecksums
        };
        engineOptions.FontSources.Add(options.FontSourceId, fontPath);
        var engine = new ManagedOpenTypeWebFontSubsetEngine(engineOptions);
        var assets = new List<WebFontAsset>();
        foreach (IReadOnlyList<WebFontTextSequence> slice in CreateSlices(sequences, options))
        {
            WebFontManifest sliceManifest = await engine.GenerateAsync(
                new WebFontSubsetRequest
                {
                    Face = new WebFontFaceIdentity
                    {
                        FontSourceId = options.FontSourceId,
                        SourceSha256 = sourceSha256,
                        FaceIndex = options.FaceIndex
                    },
                    ProfileId = options.ProfileId,
                    FontFamily = options.FontFamily,
                    Sequences = slice,
                    Formats = options.Formats,
                    RequiredBrowserTargets = options.RequiredBrowserTargets
                },
                outputDirectory,
                cancellationToken).ConfigureAwait(false);
            assets.AddRange(sliceManifest.Assets);
        }

        var engineManifest = new WebFontManifest
        {
            ProfileId = options.ProfileId,
            Assets = assets
        };
        string css = CreateCss(engineManifest, options);
        string cssSha256 = ComputeSha256(System.Text.Encoding.UTF8.GetBytes(css));
        string stylesheetFileName = $"webfonts.{cssSha256[..16]}.css";
        var manifest = new WebFontManifest
        {
            SchemaVersion = engineManifest.SchemaVersion,
            ProfileId = engineManifest.ProfileId,
            Assets = engineManifest.Assets,
            StylesheetFileName = stylesheetFileName,
            StylesheetSha256 = cssSha256
        };
        await WriteAtomicAsync(
            Path.Combine(outputDirectory, stylesheetFileName),
            css,
            cancellationToken).ConfigureAwait(false);
        await WriteAtomicAsync(
            Path.Combine(outputDirectory, "webfonts.css"),
            css,
            cancellationToken).ConfigureAwait(false);
        await WriteAtomicAsync(
            Path.Combine(outputDirectory, "webfonts.json"),
            JsonSerializer.Serialize(manifest, JsonOptions),
            cancellationToken).ConfigureAwait(false);
        return manifest;
    }

    private static async Task<string> ReadTextAsync(
        WebFontBuildOptions options,
        CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        long totalBytes = 0;
        if (!string.IsNullOrWhiteSpace(options.TextPath))
        {
            byte[] bytes = await ReadBoundedBytesAsync(
                options.TextPath,
                options.MaxCorpusBytes,
                cancellationToken).ConfigureAwait(false);
            totalBytes += bytes.LongLength;
            text.Append(options.LegacyEncoding?.ToLowerInvariant() switch
            {
                null or "utf-8" => new UTF8Encoding(false, true).GetString(bytes),
                "big5" or "cp950" => new Big5CharacterMappingProvider().Decode(bytes),
                "big5e" => DecodeBig5E(options, bytes),
                "json-profile" => DecodeJsonProfile(options, bytes),
                "euc-tw" or "cns11643" => DecodeCns11643(options, bytes),
                _ => throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"))
            });
        }

        foreach (string path in options.ContentPaths
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            byte[] bytes = await ReadBoundedBytesAsync(
                path,
                options.MaxCorpusBytes - totalBytes,
                cancellationToken).ConfigureAwait(false);
            totalBytes += bytes.LongLength;
            text.Append(new UTF8Encoding(false, true).GetString(bytes));
        }

        return text.ToString();
    }

    private static async Task<byte[]> ReadBoundedBytesAsync(
        string path,
        long remainingBytes,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || remainingBytes <= 0 || info.Length > remainingBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return await File.ReadAllBytesAsync(info.FullName, cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<WebFontTextSequence> SelectUniqueSequences(string text, int maximumCount)
    {
        Rune[] runes = text.EnumerateRunes().ToArray();
        var seenScalars = new HashSet<int>();
        var seenSequences = new HashSet<(int BaseScalar, int Selector)>();
        var result = new List<WebFontTextSequence>();

        // 以純量配對而非字串判重：先前每個語料字元都會配置一個字串，大型語料
        // 因而產生數千萬次配置。改以值型別鍵判重後，只有真正新出現的序列才具體化。
        for (int index = 0; index < runes.Length; index++)
        {
            Rune rune = runes[index];
            if (Rune.IsControl(rune) || rune.Value == 0xFEFF || IsVariationSelector(rune.Value))
            {
                continue;
            }

            Rune? selector = null;
            if (index + 1 < runes.Length && IsVariationSelector(runes[index + 1].Value))
            {
                selector = runes[++index];
            }

            if (seenScalars.Add(rune.Value) && seenScalars.Count > maximumCount)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            if (!seenSequences.Add((rune.Value, selector?.Value ?? -1)))
            {
                continue;
            }

            string value = selector is null
                ? rune.ToString()
                : string.Concat(rune.ToString(), selector.Value.ToString());
            result.Add(WebFontTextSequence.Create(value));
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return result;
    }

    internal static IReadOnlyList<IReadOnlyList<WebFontTextSequence>> CreateSlices(
        IReadOnlyList<WebFontTextSequence> sequences,
        WebFontBuildOptions options)
    {
        if (options.UnicodeRangeSliceSize == 0)
        {
            return [sequences];
        }

        IReadOnlyList<WebFontTextSequence>[] slices = sequences
            .GroupBy(sequence => GetBaseScalar(sequence) / options.UnicodeRangeSliceSize)
            .OrderBy(group => group.Key)
            .Select(group => (IReadOnlyList<WebFontTextSequence>)group
                .OrderBy(GetBaseScalar)
                .ThenBy(sequence => string.Join(",", sequence.UnicodeScalars), StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        if (slices.Length > options.MaxSliceCount)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return slices;
    }

    private static int GetBaseScalar(WebFontTextSequence sequence)
        => sequence.UnicodeScalars.First(scalar => !IsVariationSelector(scalar));

    private static bool IsVariationSelector(int scalar)
        => scalar is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF;

    private static string DecodeBig5E(WebFontBuildOptions options, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(options.Big5EMappingPath))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }

        using var reader = new StreamReader(options.Big5EMappingPath, new UTF8Encoding(false, true));
        Big5EMapping mapping = Big5EMapping.Load(reader, options.ProfileId);
        return new Big5ECharacterMappingProvider(mapping).Decode(bytes);
    }

    private static string DecodeJsonProfile(WebFontBuildOptions options, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(options.JsonProfilePath))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }

        using FileStream stream = File.OpenRead(options.JsonProfilePath);
        JsonCharacterMappingProvider provider = JsonCharacterMappingProvider.Load(
            stream,
            16 * 1024 * 1024,
            1_000_000);
        if (!string.Equals(provider.ProfileId, options.ProfileId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return provider.Decode(bytes);
    }

    private static string DecodeCns11643(WebFontBuildOptions options, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(options.CnsMappingArchivePath))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }

        string archivePath = Path.GetFullPath(options.CnsMappingArchivePath);
        if (!string.Equals(
                ComputeSha256(archivePath),
                Cns11643EucTwMappingProvider.VerifiedArchiveSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        List<ZipArchiveEntry> entries = archive.Entries
            .Where(entry => entry.FullName.Replace('\\', '/').StartsWith(
                    "Unicode/CNS2UNICODE_Unicode ",
                    StringComparison.Ordinal)
                && entry.FullName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.Ordinal)
            .ToList();
        if (entries.Count == 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        var readers = new List<TextReader>(entries.Count);
        try
        {
            foreach (ZipArchiveEntry entry in entries)
            {
                readers.Add(new StreamReader(entry.Open(), new UTF8Encoding(false, true)));
            }

            Cns11643EucTwMappingProvider provider = Cns11643EucTwMappingProvider.Load(
                readers,
                1_000_000);
            if (!string.Equals(provider.ProfileId, options.ProfileId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            return provider.Decode(bytes);
        }
        finally
        {
            foreach (TextReader reader in readers)
            {
                reader.Dispose();
            }
        }
    }

    internal static string CreateCss(WebFontManifest manifest, WebFontBuildOptions options)
    {
        var css = new StringBuilder();
        foreach (IGrouping<string, WebFontAsset> group in manifest.Assets
                     .GroupBy(
                         asset => $"{asset.FontFamily}\u001F{string.Join(",", asset.UnicodeRanges)}",
                         StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            WebFontAsset first = group.First();
            string sources = string.Join(
                ",\n       ",
                group.OrderBy(asset => GetCssFormatPriority(asset.Format))
                    .Select(asset => $"url('./{asset.Sha256}/{asset.FileName}') format('{GetCssFormat(asset.Format)}')"));
            css.AppendLine("@font-face {");
            css.Append("  font-family: '").Append(EscapeCss(first.FontFamily)).AppendLine("';");
            css.Append("  src: ").Append(sources).AppendLine(";");
            css.Append("  unicode-range: ").Append(string.Join(", ", first.UnicodeRanges)).AppendLine(";");
            css.Append("  font-display: ").Append(GetFontDisplay(options.FontDisplay)).AppendLine(";");
            css.AppendLine("}");
        }

        if (options.FallbackMetrics is not null)
        {
            AppendFallbackFace(css, options.FallbackMetrics);
        }

        return css.ToString();
    }

    private static void AppendFallbackFace(StringBuilder css, WebFontFallbackMetrics fallback)
    {
        css.AppendLine("@font-face {");
        css.Append("  font-family: '").Append(EscapeCss(fallback.FontFamily)).AppendLine("';");
        css.Append("  src: local('").Append(EscapeCss(fallback.LocalFontName)).AppendLine("');");
        css.Append("  size-adjust: ").Append(FormatPercentage(fallback.SizeAdjustPercentage)).AppendLine(";");
        css.Append("  ascent-override: ").Append(FormatPercentage(fallback.AscentOverridePercentage)).AppendLine(";");
        css.Append("  descent-override: ").Append(FormatPercentage(fallback.DescentOverridePercentage)).AppendLine(";");
        css.Append("  line-gap-override: ").Append(FormatPercentage(fallback.LineGapOverridePercentage)).AppendLine(";");
        css.AppendLine("}");
    }

    private static string FormatPercentage(double value)
        => string.Concat(
            value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "%");

    private static string GetFontDisplay(WebFontDisplayMode mode)
        => mode switch
        {
            WebFontDisplayMode.Auto => "auto",
            WebFontDisplayMode.Block => "block",
            WebFontDisplayMode.Swap => "swap",
            WebFontDisplayMode.Fallback => "fallback",
            WebFontDisplayMode.Optional => "optional",
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"))
        };

    private static void Validate(WebFontBuildOptions options)
    {
        if (options is null
            || string.IsNullOrWhiteSpace(options.FontPath)
            || !File.Exists(options.FontPath)
            || (string.IsNullOrWhiteSpace(options.TextPath) && options.ContentPaths.Count == 0)
            || (!string.IsNullOrWhiteSpace(options.TextPath) && !File.Exists(options.TextPath))
            || options.ContentPaths.Any(path => string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            || string.IsNullOrWhiteSpace(options.OutputDirectory)
            || string.IsNullOrWhiteSpace(options.FontFamily)
            || string.IsNullOrWhiteSpace(options.ProfileId)
            || string.IsNullOrWhiteSpace(options.FontSourceId)
            || options.FaceIndex < 0
            || options.MaxCorpusBytes <= 0
            || options.MaxUniqueUnicodeScalars <= 0
            || options.UnicodeRangeSliceSize < 0
            || options.UnicodeRangeSliceSize > 0x110000
            || options.MaxSliceCount <= 0
            || options.MaxSourceBytes <= 0
            || options.MaxOutputBytes <= 0
            || !Enum.IsDefined(options.FontDisplay)
            || !IsValidFallback(options.FallbackMetrics)
            || options.Formats.Count == 0
            || options.RequiredBrowserTargets is null
            || options.RequiredBrowserTargets.Any(
                target => !Enum.IsDefined(target)))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }
    }

    private static bool IsValidFallback(WebFontFallbackMetrics? fallback)
        => fallback is null
            || (!string.IsNullOrWhiteSpace(fallback.FontFamily)
                && !string.IsNullOrWhiteSpace(fallback.LocalFontName)
                && IsValidPercentage(fallback.SizeAdjustPercentage, allowZero: false)
                && IsValidPercentage(fallback.AscentOverridePercentage, allowZero: true)
                && IsValidPercentage(fallback.DescentOverridePercentage, allowZero: true)
                && IsValidPercentage(fallback.LineGapOverridePercentage, allowZero: true));

    private static bool IsValidPercentage(double value, bool allowZero)
        => double.IsFinite(value) && value <= 1000 && (allowZero ? value >= 0 : value > 0);

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// 以 CSS 字串字面值規則逸出值。
    /// </summary>
    /// <remarks>
    /// 僅替換反斜線與單引號並不足夠：CSS 字串不得含裸換行，換行會終止字串並成為
    /// 解析錯誤，可用來注入任意規則；若樣式被內嵌於 HTML，<c>&lt;</c> 亦可用於跳出
    /// <c>&lt;style&gt;</c>。這裡對控制字元與具語法意義的字元一律採用「反斜線 + 十六進位 +
    /// 空白」的標準逸出形式。
    /// </remarks>
    private static string EscapeCss(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (character is '\\' or '\'' or '"' or '<' or '>' or '&'
                || char.IsControl(character))
            {
                builder.Append('\\')
                    .Append(((int)character).ToString("x", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(' ');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string GetCssFormat(WebFontFormat format)
        => format switch
        {
            WebFontFormat.Woff2 => "woff2",
            WebFontFormat.Woff => "woff",
            WebFontFormat.TrueType => "truetype",
            WebFontFormat.OpenType => "opentype",
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"))
        };

    private static int GetCssFormatPriority(WebFontFormat format)
        => format switch
        {
            WebFontFormat.Woff2 => 0,
            WebFontFormat.Woff => 1,
            WebFontFormat.OpenType => 2,
            WebFontFormat.TrueType => 3,
            _ => int.MaxValue
        };
}

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
    public async Task<WebFontManifest> BuildAsync(
        WebFontBuildOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        string fontPath = Path.GetFullPath(options.FontPath);
        string outputDirectory = Path.GetFullPath(options.OutputDirectory);
        string text = await ReadTextAsync(options, cancellationToken).ConfigureAwait(false);
        WebFontTextSequence sequence = WebFontTextSequence.Create(
            SelectUniqueScalars(text, options.MaxUniqueUnicodeScalars));
        string sourceSha256 = ComputeSha256(fontPath);

        var engineOptions = new FontToolsWebFontEngineOptions
        {
            ExecutablePath = options.FontToolsExecutable,
            MaxUnicodeScalars = options.MaxUniqueUnicodeScalars
        };
        if (!string.IsNullOrWhiteSpace(options.FontToolsPythonModulePath))
        {
            engineOptions.ExecutablePrefixArguments.Add("-m");
            engineOptions.ExecutablePrefixArguments.Add("fontTools.subset.__main__");
            engineOptions.EnvironmentVariables["PYTHONPATH"] = Path.GetFullPath(options.FontToolsPythonModulePath);
        }

        engineOptions.FontSources.Add(options.FontSourceId, fontPath);
        var engine = new FontToolsWebFontSubsetEngine(engineOptions);
        WebFontManifest engineManifest = await engine.GenerateAsync(
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
                Sequences = [sequence],
                Formats = options.Formats
            },
            outputDirectory,
            cancellationToken).ConfigureAwait(false);

        string css = CreateCss(engineManifest);
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

    private static string SelectUniqueScalars(string text, int maximumCount)
    {
        var seen = new HashSet<int>();
        var result = new StringBuilder();
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (!seen.Add(rune.Value))
            {
                continue;
            }

            if (seen.Count > maximumCount)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            result.Append(rune.ToString());
        }

        if (result.Length == 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return result.ToString();
    }

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

    private static string CreateCss(WebFontManifest manifest)
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
            css.AppendLine("  font-display: swap;");
            css.AppendLine("}");
        }

        return css.ToString();
    }

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
            || options.Formats.Count == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }
    }

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

    private static string EscapeCss(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);

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

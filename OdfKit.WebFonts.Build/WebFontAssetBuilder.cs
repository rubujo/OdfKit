using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OdfKit.Compliance;
using OdfKit.WebFonts.Encoding.Legacy;
using OdfKit.WebFonts.OpenType;

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

    private static string CreateCss(WebFontManifest manifest)
    {
        var css = new StringBuilder();
        foreach (WebFontAsset asset in manifest.Assets)
        {
            string format = asset.Format switch
            {
                WebFontFormat.Woff2 => "woff2",
                WebFontFormat.Woff => "woff",
                WebFontFormat.TrueType => "truetype",
                WebFontFormat.OpenType => "opentype",
                _ => string.Empty
            };
            css.AppendLine("@font-face {");
            css.Append("  font-family: '").Append(EscapeCss(asset.FontFamily)).AppendLine("';");
            css.Append("  src: url('./").Append(asset.Sha256).Append('/').Append(asset.FileName).Append("') format('").Append(format).AppendLine("');");
            css.Append("  unicode-range: ").Append(string.Join(", ", asset.UnicodeRanges)).AppendLine(";");
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
}

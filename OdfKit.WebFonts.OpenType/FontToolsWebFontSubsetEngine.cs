using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Produces deterministic standalone font subsets with a trusted FontTools installation.
/// 使用受信任的 FontTools 安裝產生確定性的獨立字型子集。
/// </summary>
public sealed class FontToolsWebFontSubsetEngine : IWebFontSubsetEngine
{
    private readonly FontToolsWebFontEngineOptions _options;

    /// <summary>
    /// Initializes the bounded engine configuration.
    /// 初始化有界的引擎設定。
    /// </summary>
    /// <param name="options">The trusted engine options. / 受信任的引擎設定。</param>
    public FontToolsWebFontSubsetEngine(FontToolsWebFontEngineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(
            nameof(options),
            OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        ValidateOptions();
    }

    /// <inheritdoc />
    public async Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, destinationDirectory);
        string sourcePath = ResolveSource(request.Face);
        RejectUnverifiedFontTechnologies(sourcePath, request.Face.FaceIndex);
        Directory.CreateDirectory(destinationDirectory);

        int[] scalars = request.Sequences
            .SelectMany(sequence => sequence.UnicodeScalars)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        string unicodeArgument = string.Join(",", scalars.Select(value => $"U+{value:X}"));
        var assets = new List<WebFontAsset>(request.Formats.Count);

        foreach (WebFontFormat format in request.Formats.Distinct())
        {
            string extension = GetExtension(format);
            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "OdfKit.WebFonts",
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(temporaryDirectory);
            string temporaryPath = Path.Combine(temporaryDirectory, $"subset.{extension}");
            try
            {
                await RunSubsetAsync(
                    sourcePath,
                    request.Face.FaceIndex,
                    unicodeArgument,
                    format,
                    temporaryPath,
                    cancellationToken).ConfigureAwait(false);
                ValidateOutput(temporaryPath, format);

                var info = new FileInfo(temporaryPath);
                long outputLength = info.Length;
                string sha256 = ComputeSha256(temporaryPath);
                string fileName = $"{SanitizeFamily(request.FontFamily)}.{sha256[..16]}.{extension}";
                string hashDirectory = Path.Combine(destinationDirectory, sha256);
                Directory.CreateDirectory(hashDirectory);
                string finalPath = Path.Combine(hashDirectory, fileName);
                if (File.Exists(finalPath))
                {
                    if (!string.Equals(ComputeSha256(finalPath), sha256, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
                    }

                    File.Delete(temporaryPath);
                }
                else
                {
                    File.Copy(temporaryPath, finalPath);
                }

                assets.Add(new WebFontAsset
                {
                    FileName = fileName,
                    Sha256 = sha256,
                    ByteLength = outputLength,
                    Format = format,
                    FontFamily = request.FontFamily,
                    UnicodeRanges = CreateUnicodeRanges(scalars)
                });
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }

        return new WebFontManifest
        {
            ProfileId = request.ProfileId,
            Assets = assets
        };
    }

    private async Task RunSubsetAsync(
        string sourcePath,
        int faceIndex,
        string unicodes,
        WebFontFormat format,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach ((string key, string value) in _options.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        foreach (string argument in _options.ExecutablePrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add($"--output-file={outputPath}");
        startInfo.ArgumentList.Add($"--unicodes={unicodes}");
        startInfo.ArgumentList.Add("--layout-features=*");
        startInfo.ArgumentList.Add("--no-hinting");
        startInfo.ArgumentList.Add("--canonical-order");
        startInfo.ArgumentList.Add("--recalc-bounds");
        startInfo.ArgumentList.Add("--no-recalc-timestamp");
        if (faceIndex > 0 || IsCollection(sourcePath))
        {
            startInfo.ArgumentList.Add($"--font-number={faceIndex.ToString(CultureInfo.InvariantCulture)}");
        }

        string? flavor = format switch
        {
            WebFontFormat.Woff2 => "woff2",
            WebFontFormat.Woff => "woff",
            _ => null
        };
        if (flavor is not null)
        {
            startInfo.ArgumentList.Add($"--flavor={flavor}");
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"));
        }

        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        string error = await errorTask.ConfigureAwait(false);
        _ = await outputTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                OdfLocalizer.GetMessage("Err_WebFont_ProcessFailedWithDetail"),
                Truncate(error, 2048)));
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ExecutablePath)
            || _options.FontSources.Count == 0
            || _options.MaxSourceBytes <= 0
            || _options.MaxOutputBytes <= 0
            || _options.MaxUnicodeScalars <= 0
            || _options.ProcessTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }
    }

    private void ValidateRequest(WebFontSubsetRequest request, string destinationDirectory)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(destinationDirectory)
            || string.IsNullOrWhiteSpace(request.ProfileId)
            || string.IsNullOrWhiteSpace(request.FontFamily)
            || request.Sequences.Count == 0
            || request.Formats.Count == 0
            || request.Sequences.Sum(item => (long)item.UnicodeScalars.Count) > _options.MaxUnicodeScalars
            || request.Formats.Any(format => !Enum.IsDefined(format)))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }
    }

    private string ResolveSource(WebFontFaceIdentity face)
    {
        if (face.FaceIndex < 0
            || string.IsNullOrWhiteSpace(face.FontSourceId)
            || !_options.FontSources.TryGetValue(face.FontSourceId, out string? configuredPath))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        string path = Path.GetFullPath(configuredPath);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || info.Length > _options.MaxSourceBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        if (!string.IsNullOrWhiteSpace(face.SourceSha256)
            && !string.Equals(ComputeSha256(path), face.SourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return path;
    }

    private void ValidateOutput(string path, WebFontFormat format)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 4 || info.Length > _options.MaxOutputBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        Span<byte> signature = stackalloc byte[4];
        using FileStream stream = File.OpenRead(path);
        if (stream.Read(signature) != signature.Length || !HasExpectedSignature(signature, format))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }
    }

    private static bool HasExpectedSignature(ReadOnlySpan<byte> signature, WebFontFormat format)
        => format switch
        {
            WebFontFormat.Woff2 => signature.SequenceEqual("wOF2"u8),
            WebFontFormat.Woff => signature.SequenceEqual("wOFF"u8),
            WebFontFormat.TrueType => signature.SequenceEqual(new byte[] { 0, 1, 0, 0 }) || signature.SequenceEqual("true"u8),
            WebFontFormat.OpenType => signature.SequenceEqual("OTTO"u8),
            _ => false
        };

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string SanitizeFamily(string family)
    {
        string value = new(family
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            .Take(64)
            .ToArray());
        return value.Length == 0 ? "webfont" : value;
    }

    private static string GetExtension(WebFontFormat format)
        => format switch
        {
            WebFontFormat.Woff2 => "woff2",
            WebFontFormat.Woff => "woff",
            WebFontFormat.TrueType => "ttf",
            WebFontFormat.OpenType => "otf",
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"))
        };

    private static IReadOnlyList<string> CreateUnicodeRanges(IEnumerable<int> scalars)
        => scalars.Select(value => $"U+{value:X}").ToArray();

    private static bool IsCollection(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".ttc" or ".otc";

    private static void RejectUnverifiedFontTechnologies(string path, int faceIndex)
    {
        IReadOnlySet<string> tags = ReadSfntTableTags(path, faceIndex);
        if (tags.Contains("CFF2")
            || tags.Contains("COLR")
            || tags.Contains("CPAL")
            || tags.Contains("CBDT")
            || tags.Contains("CBLC")
            || tags.Contains("sbix")
            || tags.Contains("SVG "))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }
    }

    private static IReadOnlySet<string> ReadSfntTableTags(string path, int faceIndex)
    {
        using FileStream stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        byte[] signature = reader.ReadBytes(4);
        long faceOffset = 0;
        if (signature.AsSpan().SequenceEqual("ttcf"u8))
        {
            _ = ReadUInt32BigEndian(reader);
            uint faceCount = ReadUInt32BigEndian(reader);
            if (faceIndex < 0 || (uint)faceIndex >= faceCount)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            stream.Position = 12L + (faceIndex * 4L);
            faceOffset = ReadUInt32BigEndian(reader);
            stream.Position = faceOffset;
            signature = reader.ReadBytes(4);
        }

        if (signature.Length != 4
            || !(signature.AsSpan().SequenceEqual("OTTO"u8)
                || signature.AsSpan().SequenceEqual("true"u8)
                || signature.AsSpan().SequenceEqual(new byte[] { 0, 1, 0, 0 })))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        ushort tableCount = ReadUInt16BigEndian(reader);
        stream.Position = faceOffset + 12;
        var tags = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < tableCount; index++)
        {
            byte[] tag = reader.ReadBytes(4);
            if (tag.Length != 4)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            tags.Add(System.Text.Encoding.ASCII.GetString(tag));
            stream.Position += 12;
        }

        return tags;
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[2];
        if (reader.Read(bytes) != bytes.Length)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt32BigEndian(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (reader.Read(bytes) != bytes.Length)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static string Truncate(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];
}

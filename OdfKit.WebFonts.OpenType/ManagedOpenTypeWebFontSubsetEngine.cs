using System.Security.Cryptography;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Produces deterministic TrueType, WOFF, and supported WOFF2 subsets using only .NET APIs.
/// 僅使用 .NET API 產生確定性的 TrueType、WOFF 與受支援的 WOFF2 子集。
/// </summary>
public sealed class ManagedOpenTypeWebFontSubsetEngine : IWebFontSubsetEngine
{
    private readonly ManagedOpenTypeWebFontEngineOptions _options;
    private readonly object _sourceCacheGate = new();
    private readonly Dictionary<string, byte[]> _sourceCache = new(StringComparer.Ordinal);
    private readonly Queue<string> _sourceCacheOrder = new();
    private long _cachedSourceBytes;

    /// <summary>
    /// Initializes the bounded managed engine.
    /// 初始化有界的受控引擎。
    /// </summary>
    /// <param name="options">The trusted engine options. / 受信任的引擎設定。</param>
    public ManagedOpenTypeWebFontSubsetEngine(ManagedOpenTypeWebFontEngineOptions options)
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
        byte[] sourceBytes = await GetVerifiedSourceAsync(
            sourcePath,
            request.Face.SourceSha256,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        SfntFont source = SfntFont.Parse(
            sourceBytes,
            request.Face.FaceIndex,
            _options.MaxTableCount,
            _options.ValidateSourceChecksums);
        IReadOnlyList<int> scalars = request.Sequences
            .SelectMany(sequence => sequence.UnicodeScalars)
            .Where(RequiresGlyph)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (scalars.Count == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        IReadOnlyList<UnicodeVariationSequence> variationSequences = CreateVariationSequences(request.Sequences);
        SfntSubset subset = source.CreateTrueTypeSubset(scalars, variationSequences, _options.MaxCompositeDepth);
        Directory.CreateDirectory(destinationDirectory);
        var assets = new List<WebFontAsset>(request.Formats.Count);
        foreach (WebFontFormat format in request.Formats.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] output = WebFontWriters.Write(subset, format);
            if (output.LongLength > _options.MaxOutputBytes)
            {
                throw DataInvalid("output-size");
            }

            using (var verificationStream = new MemoryStream(output, writable: false))
            {
                ManagedOpenTypeWebFontVerifier.VerifyContainsScalars(
                    verificationStream,
                    format,
                    scalars);
            }

            string sha256 = ComputeSha256(output);
            string extension = GetExtension(format);
            string fileName = $"{SanitizeFamily(request.FontFamily)}.{sha256.Substring(0, 16)}.{extension}";
            string hashDirectory = Path.Combine(destinationDirectory, sha256);
            Directory.CreateDirectory(hashDirectory);
            string outputPath = Path.Combine(hashDirectory, fileName);
            await WriteImmutableAsync(outputPath, output, sha256, cancellationToken).ConfigureAwait(false);
            assets.Add(new WebFontAsset
            {
                FileName = fileName,
                Sha256 = sha256,
                ByteLength = output.LongLength,
                Format = format,
                FontFamily = request.FontFamily,
                UnicodeRanges = UnicodeRangeFormatter.Create(scalars)
            });
        }

        return new WebFontManifest
        {
            ProfileId = request.ProfileId,
            Assets = assets
        };
    }

    private static IReadOnlyList<UnicodeVariationSequence> CreateVariationSequences(
        IEnumerable<WebFontTextSequence> sequences)
    {
        var result = new HashSet<UnicodeVariationSequence>();
        foreach (WebFontTextSequence sequence in sequences)
        {
            for (int index = 1; index < sequence.UnicodeScalars.Count; index++)
            {
                int selector = sequence.UnicodeScalars[index];
                if (selector is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF)
                {
                    result.Add(new UnicodeVariationSequence(sequence.UnicodeScalars[index - 1], selector));
                }
            }
        }

        return result.OrderBy(item => item.Selector).ThenBy(item => item.BaseScalar).ToArray();
    }

    private static bool RequiresGlyph(int scalar)
        => scalar != 0xFEFF
            && scalar is not (>= 0xFE00 and <= 0xFE0F)
            && scalar is not (>= 0xE0100 and <= 0xE01EF)
            && scalar is not (>= 0x0000 and <= 0x001F)
            && scalar is not (>= 0x007F and <= 0x009F);

    private void ValidateOptions()
    {
        if (_options.FontSources.Count == 0
            || _options.MaxSourceBytes <= 0
            || _options.MaxOutputBytes <= 0
            || _options.MaxCachedSourceBytes < 0
            || _options.MaxCachedSourceEntries < 0
            || (_options.MaxCachedSourceBytes == 0) != (_options.MaxCachedSourceEntries == 0)
            || _options.MaxUnicodeScalars <= 0
            || _options.MaxTableCount <= 0
            || _options.MaxCompositeDepth <= 0)
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
            || request.Formats.Any(format => !Enum.IsDefined(typeof(WebFontFormat), format)))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

#if !NET10_0_OR_GREATER
        if (request.Formats.Contains(WebFontFormat.Woff2))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }
#endif
        if (request.Formats.Contains(WebFontFormat.OpenType))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
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
            throw DataInvalid("source-size");
        }

        return path;
    }

    private static async Task<byte[]> ReadSourceAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        if (stream.Length <= 0 || stream.Length > maximumBytes || stream.Length > int.MaxValue)
        {
            throw DataInvalid("source-size");
        }

        var bytes = new byte[(int)stream.Length];
        int read = 0;
        while (read < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = await stream.ReadAsync(bytes, read, bytes.Length - read, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                throw DataInvalid("source-truncated");
            }

            read += count;
        }

        var trailing = new byte[1];
        if (await stream.ReadAsync(trailing, 0, 1, cancellationToken).ConfigureAwait(false) != 0)
        {
            throw DataInvalid("source-size");
        }

        return bytes;
    }

    private async Task<byte[]> GetVerifiedSourceAsync(
        string path,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        string normalizedSha256 = expectedSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        string cacheKey = string.IsNullOrEmpty(normalizedSha256)
            ? string.Empty
            : string.Concat(path, "|", normalizedSha256);
        if (cacheKey.Length != 0)
        {
            lock (_sourceCacheGate)
            {
                if (_sourceCache.TryGetValue(cacheKey, out byte[]? cached))
                {
                    return cached;
                }
            }
        }

        byte[] bytes = await ReadSourceAsync(path, _options.MaxSourceBytes, cancellationToken).ConfigureAwait(false);
        string actualSha256 = ComputeSha256(bytes);
        if (normalizedSha256.Length != 0
            && !string.Equals(actualSha256, normalizedSha256, StringComparison.Ordinal))
        {
            throw DataInvalid("source-sha256");
        }

        if (cacheKey.Length != 0)
        {
            CacheVerifiedSource(cacheKey, bytes);
        }

        return bytes;
    }

    private void CacheVerifiedSource(string cacheKey, byte[] bytes)
    {
        if (_options.MaxCachedSourceEntries == 0 || bytes.LongLength > _options.MaxCachedSourceBytes)
        {
            return;
        }

        lock (_sourceCacheGate)
        {
            if (_sourceCache.ContainsKey(cacheKey))
            {
                return;
            }

            while (_sourceCache.Count >= _options.MaxCachedSourceEntries
                   || _cachedSourceBytes + bytes.LongLength > _options.MaxCachedSourceBytes)
            {
                if (_sourceCacheOrder.Count == 0)
                {
                    return;
                }

                string removedKey = _sourceCacheOrder.Dequeue();
                if (_sourceCache.TryGetValue(removedKey, out byte[]? removed))
                {
                    _sourceCache.Remove(removedKey);
                    _cachedSourceBytes -= removed.LongLength;
                }
            }

            _sourceCache.Add(cacheKey, bytes);
            _sourceCacheOrder.Enqueue(cacheKey);
            _cachedSourceBytes += bytes.LongLength;
        }
    }

    private static async Task WriteImmutableAsync(
        string path,
        byte[] bytes,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            if (!string.Equals(ComputeFileSha256(path), expectedSha256, StringComparison.Ordinal))
            {
                throw DataInvalid("destination-sha256");
            }

            return;
        }

        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
#if NET10_0_OR_GREATER
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken).ConfigureAwait(false);
#else
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            }
#endif
            if (File.Exists(path))
            {
                File.Delete(temporaryPath);
                if (!string.Equals(ComputeFileSha256(path), expectedSha256, StringComparison.Ordinal))
                {
                    throw DataInvalid("destination-sha256");
                }
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string GetExtension(WebFontFormat format)
        => format switch
        {
            WebFontFormat.Woff2 => "woff2",
            WebFontFormat.Woff => "woff",
            WebFontFormat.TrueType => "ttf",
            _ => throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"))
        };

    private static string SanitizeFamily(string family)
    {
        string value = new(family
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .Take(64)
            .ToArray());
        return value.Length == 0 ? "webfont" : value;
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 algorithm = SHA256.Create();
        return ToLowerHex(algorithm.ComputeHash(bytes));
    }

    private static string ComputeFileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 algorithm = SHA256.Create();
        return ToLowerHex(algorithm.ComputeHash(stream));
    }

    private static string ToLowerHex(byte[] bytes)
        => string.Concat(bytes.Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));

    private static InvalidDataException DataInvalid(string detail)
        => new($"{OdfLocalizer.GetMessage("Err_WebFont_DataInvalid")} [{detail}]");
}

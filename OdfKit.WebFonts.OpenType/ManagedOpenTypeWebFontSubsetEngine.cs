using System.Security.Cryptography;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Produces deterministic supported OpenType, WOFF, and WOFF2 subsets using only .NET APIs.
/// 僅使用 .NET API 產生確定性的受支援 OpenType、WOFF 與 WOFF2 子集。
/// </summary>
public sealed class ManagedOpenTypeWebFontSubsetEngine : IWebFontSubsetEngine, IWebFontTextCoverageFilter
{
    private readonly ManagedOpenTypeWebFontEngineOptions _options;
    private readonly object _sourceCacheGate = new();
    private readonly Dictionary<string, CachedSource> _sourceCache = new(StringComparer.Ordinal);
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

    /// <summary>
    /// Generates deterministic, bounded WebFont subset assets.
    /// 產生確定且有界的 WebFont 子集資產。
    /// </summary>
    /// <param name="request">The validated subset request. / 已驗證的子集要求。</param>
    /// <param name="destinationDirectory">The trusted destination directory. / 受信任的目的目錄。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The generated content-addressed manifest. / 產生的內容定址 manifest。</returns>
    public async Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, destinationDirectory);
        string sourcePath = ResolveSource(request.Face);
        CachedSource cachedSource = await GetVerifiedSourceAsync(
            sourcePath,
            request.Face.SourceSha256,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        SfntFont source = cachedSource.GetFont(
            request.Face.FaceIndex,
            _options.MaxTableCount,
            _options.ValidateSourceChecksums,
            cancellationToken);
        if (source.HasColorTables && string.IsNullOrWhiteSpace(request.Face.SourceSha256))
        {
            throw DataInvalid("color-source-sha256");
        }

        source.ValidateOutputFormats(request.Formats);
        source.ValidateBrowserTargets(request.RequiredBrowserTargets);
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
        SfntSubset subset = source.CreateSubset(
            scalars,
            variationSequences,
            _options.MaxCompositeDepth,
            cancellationToken);
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
                    scalars,
                    _options.MaxOutputBytes,
                    _options.VerifyEveryOutputCharString,
                    cancellationToken);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
        WebFontFaceIdentity face,
        IReadOnlyList<WebFontTextSequence> sequences,
        CancellationToken cancellationToken = default)
    {
        if (face is null || sequences is null || sequences.Any(sequence => sequence is null))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        string sourcePath = ResolveSource(face);
        CachedSource cachedSource = await GetVerifiedSourceAsync(
            sourcePath,
            face.SourceSha256,
            cancellationToken).ConfigureAwait(false);
        SfntFont source = cachedSource.GetFont(
            face.FaceIndex,
            _options.MaxTableCount,
            _options.ValidateSourceChecksums,
            cancellationToken);

        return WebFontSequenceCoverage.Filter(
            sequences,
            source.ContainsUnicodeScalar,
            source.ContainsVariationSequence,
            cancellationToken);
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

    private static bool RequiresGlyph(int scalar) => WebFontSequenceCoverage.RequiresGlyph(scalar);

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
            || request.RequiredBrowserTargets is null
            || request.Sequences.Sum(item => (long)item.UnicodeScalars.Count) > _options.MaxUnicodeScalars
            || request.Formats.Any(format => !Enum.IsDefined(typeof(WebFontFormat), format))
            || request.RequiredBrowserTargets.Any(
                target => !Enum.IsDefined(typeof(WebFontBrowserTarget), target)))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

#if !NET10_0_OR_GREATER
        if (request.Formats.Contains(WebFontFormat.Woff2))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }
#endif
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

    private async Task<CachedSource> GetVerifiedSourceAsync(
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
                if (_sourceCache.TryGetValue(cacheKey, out CachedSource? cached))
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

        var source = new CachedSource(bytes, checked((int)_options.MaxSourceBytes));
        return cacheKey.Length == 0 ? source : CacheVerifiedSource(cacheKey, source);
    }

    private CachedSource CacheVerifiedSource(string cacheKey, CachedSource source)
    {
        if (_options.MaxCachedSourceEntries == 0 || source.Bytes.LongLength > _options.MaxCachedSourceBytes)
        {
            return source;
        }

        lock (_sourceCacheGate)
        {
            if (_sourceCache.TryGetValue(cacheKey, out CachedSource? cached))
            {
                return cached;
            }

            while (_sourceCache.Count >= _options.MaxCachedSourceEntries
                   || _cachedSourceBytes + source.Bytes.LongLength > _options.MaxCachedSourceBytes)
            {
                if (_sourceCacheOrder.Count == 0)
                {
                    return source;
                }

                string removedKey = _sourceCacheOrder.Dequeue();
                if (_sourceCache.TryGetValue(removedKey, out CachedSource? removed))
                {
                    _sourceCache.Remove(removedKey);
                    _cachedSourceBytes -= removed.Bytes.LongLength;
                }
            }

            _sourceCache.Add(cacheKey, source);
            _sourceCacheOrder.Enqueue(cacheKey);
            _cachedSourceBytes += source.Bytes.LongLength;
            return source;
        }
    }

    private sealed class CachedSource(byte[] bytes, int maximumExpandedBytes)
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, SfntFont> _fonts = [];

        internal byte[] Bytes { get; } = bytes;

        // 每個已解析 face 會保留一份完整表格副本，其記憶體不計入來源位元組上限，
        // 因此對單一來源保留的 face 數另設界限，避免多 face TTC 無限累積。
        private const int MaximumCachedFaces = 8;

        internal SfntFont GetFont(
            int faceIndex,
            int maxTableCount,
            bool validateChecksums,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (!_fonts.TryGetValue(faceIndex, out SfntFont? font))
                {
                    byte[] decoded = Bytes;
                    int decodedFaceIndex = faceIndex;
                    if (Bytes.Length >= 4 && Bytes.AsSpan(0, 4).SequenceEqual("wOFF"u8))
                    {
                        decoded = ManagedOpenTypeWebFontVerifier.DecodeWoff(
                            Bytes,
                            maximumExpandedBytes,
                            cancellationToken);
                        decodedFaceIndex = faceIndex;
                    }
#if NET10_0_OR_GREATER
                    else if (Bytes.Length >= 4 && Bytes.AsSpan(0, 4).SequenceEqual("wOF2"u8))
                    {
                        decoded = ManagedOpenTypeWebFontVerifier.DecodeWoff2(
                            Bytes,
                            maximumExpandedBytes,
                            faceIndex,
                            cancellationToken);
                        decodedFaceIndex = 0;
                    }
#else
                    else if (Bytes.Length >= 4 && Bytes.AsSpan(0, 4).SequenceEqual("wOF2"u8))
                    {
                        throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
                    }
#endif

                    font = SfntFont.Parse(
                        decoded,
                        decodedFaceIndex,
                        maxTableCount,
                        validateChecksums,
                        cancellationToken);
                    if (_fonts.Count >= MaximumCachedFaces)
                    {
                        _fonts.Clear();
                    }

                    _fonts.Add(faceIndex, font);
                }

                return font;
            }
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
                try
                {
                    File.Move(temporaryPath, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    if (!string.Equals(ComputeFileSha256(path), expectedSha256, StringComparison.Ordinal))
                    {
                        throw DataInvalid("destination-sha256");
                    }
                }
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
            WebFontFormat.OpenType => "otf",
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
#if NET10_0_OR_GREATER
        return ToLowerHex(SHA256.HashData(bytes));
#else
        using SHA256 algorithm = SHA256.Create();
        return ToLowerHex(algorithm.ComputeHash(bytes));
#endif
    }

    private static string ComputeFileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
#if NET10_0_OR_GREATER
        return ToLowerHex(SHA256.HashData(stream));
#else
        using SHA256 algorithm = SHA256.Create();
        return ToLowerHex(algorithm.ComputeHash(stream));
#endif
    }

    private static string ToLowerHex(byte[] bytes)
    {
#if NET10_0_OR_GREATER
        return Convert.ToHexStringLower(bytes);
#else
        // netstandard2.0 沒有 Convert.ToHexString；以預配置緩衝區避免 LINQ 逐位元組配置。
        var characters = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            characters[index * 2] = GetHexDigit(value >> 4);
            characters[(index * 2) + 1] = GetHexDigit(value & 0x0F);
        }

        return new string(characters);
#endif
    }

#if !NET10_0_OR_GREATER
    private static char GetHexDigit(int value)
        => (char)(value < 10 ? '0' + value : 'a' + (value - 10));
#endif

    private static InvalidDataException DataInvalid(string detail)
        => new($"{OdfLocalizer.GetMessage("Err_WebFont_DataInvalid")} [{detail}]");
}

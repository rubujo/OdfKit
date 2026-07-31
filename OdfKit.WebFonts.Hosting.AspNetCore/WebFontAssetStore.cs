using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Hosting.AspNetCore;

internal sealed class WebFontAssetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = false,
        MaxDepth = 32,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ConcurrentDictionary<string, StoredWebFontAsset> _assets;

    // 動態產生的資產與 manifest 資產分開儲存：manifest 條目在處理程序存活期間必須恆定，
    // 動態條目則會隨每次產生累積，需獨立設界限，否則長時間執行的伺服器記憶體單調成長。
    private readonly ConcurrentDictionary<string, StoredWebFontAsset> _generatedAssets =
        new(StringComparer.Ordinal);
    private readonly string _rootPath;
    private readonly OdfWebFontOptions _options;

    public WebFontAssetStore(IOptions<OdfWebFontOptions> optionsAccessor)
    {
        OdfWebFontOptions options = optionsAccessor.Value;
        string rootPath = ResolveRootPath(options);
        _rootPath = rootPath;
        _options = options;
        string? manifestPath = ResolveManifestPath(rootPath, options);
        LoadedManifest loadedManifest = manifestPath is null
            ? CreateEmptyDynamicManifest()
            : LoadManifest(manifestPath, options);
        Manifest = loadedManifest.Manifest;
        ManifestBytes = loadedManifest.Bytes;
        ManifestSha256 = ComputeSha256(ManifestBytes);
        _assets = new ConcurrentDictionary<string, StoredWebFontAsset>(
            IndexAssets(rootPath, Manifest, options),
            StringComparer.Ordinal);
        LoadedStylesheet? stylesheet = LoadOptionalCss(rootPath, Manifest, options);
        CssBytes = stylesheet?.Bytes;
        CssSha256 = stylesheet?.Sha256;
    }

    public WebFontManifest Manifest { get; }

    public byte[] ManifestBytes { get; }

    public string ManifestSha256 { get; }

    public byte[]? CssBytes { get; }

    public string? CssSha256 { get; }

    public string? StylesheetFileName => Manifest.StylesheetFileName;

    public bool IsStylesheet(string fileName)
        => CssBytes is not null
            && string.Equals(fileName, StylesheetFileName, StringComparison.Ordinal)
            && IsPlainFileName(fileName);

    public bool TryGetAsset(string sha256, string fileName, out StoredWebFontAsset? asset)
    {
        if (!IsSha256(sha256) || !IsPlainFileName(fileName))
        {
            asset = null;
            return false;
        }

        string key = CreateKey(sha256, fileName);
        if ((!_assets.TryGetValue(key, out asset) && !_generatedAssets.TryGetValue(key, out asset))
            || asset is null)
        {
            asset = TryDiscoverGeneratedAsset(sha256, fileName);
            if (asset is null)
            {
                return false;
            }

            if (_generatedAssets.Count >= _options.MaxGeneratedAssetCount)
            {
                _generatedAssets.Clear();
            }
            _generatedAssets.TryAdd(key, asset);
        }

        var info = new FileInfo(asset.FullPath);
        if (!info.Exists
            || info.LinkTarget is not null
            || new DirectoryInfo(info.DirectoryName!).LinkTarget is not null
            || info.Length != asset.Descriptor.ByteLength
            || info.LastWriteTimeUtc != asset.LastModified.UtcDateTime)
        {
            asset = null;
            return false;
        }

        return true;
    }

    private StoredWebFontAsset? TryDiscoverGeneratedAsset(string sha256, string fileName)
    {
        if (!TryResolveFormat(fileName, out WebFontFormat format))
        {
            return null;
        }

        string normalizedHash = sha256.ToLowerInvariant();
        string fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedHash, fileName));
        if (!IsContainedPath(_rootPath, fullPath))
        {
            return null;
        }

        var info = new FileInfo(fullPath);
        if (!info.Exists
            || info.Length <= 0
            || info.Length > _options.MaxAssetBytes
            || info.LinkTarget is not null
            || info.Directory is null
            || info.Directory.LinkTarget is not null
            || !string.Equals(ComputeSha256(fullPath), normalizedHash, StringComparison.Ordinal))
        {
            return null;
        }

        return new StoredWebFontAsset(
            new WebFontAsset
            {
                FileName = fileName,
                Sha256 = normalizedHash,
                ByteLength = info.Length,
                Format = format,
                FontFamily = "discovered-shared-asset",
                UnicodeRanges = []
            },
            fullPath,
            ResolveContentType(format),
            new DateTimeOffset(info.LastWriteTimeUtc));
    }

    public void RegisterGeneratedAssets(WebFontManifest manifest)
    {
        if (manifest is null
            || manifest.SchemaVersion != 1
            || string.IsNullOrWhiteSpace(manifest.ProfileId)
            || manifest.ProfileId.Length > 256
            || manifest.Assets.Count == 0
            || manifest.Assets.Count > _options.MaxAssetCount
            || manifest.StylesheetFileName is not null
            || manifest.StylesheetSha256 is not null)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        Dictionary<string, StoredWebFontAsset> generated = IndexAssets(_rootPath, manifest, _options);

        // 條目數達上限時整批清空動態索引。資產本身是內容定址且留在磁碟上，
        // 後續請求會在下一次產生時重新索引，因此清空只影響快取命中率，不影響正確性。
        if (_generatedAssets.Count + generated.Count > _options.MaxGeneratedAssetCount)
        {
            _generatedAssets.Clear();
        }

        foreach ((string key, StoredWebFontAsset asset) in generated)
        {
            _generatedAssets.TryAdd(key, asset);
        }
    }

    private static string ResolveRootPath(OdfWebFontOptions options)
    {
        OdfWebFontOptionValidator.Validate(options);

        string rootPath = Path.GetFullPath(options.AssetRootPath);
        if (!Directory.Exists(rootPath))
        {
            throw new InvalidOperationException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ConfigurationInvalid"));
        }

        return rootPath;
    }

    private static string? ResolveManifestPath(string rootPath, OdfWebFontOptions options)
    {
        if (!IsPlainFileName(options.ManifestFileName))
        {
            throw new InvalidOperationException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ConfigurationInvalid"));
        }

        string manifestPath = Path.Combine(rootPath, options.ManifestFileName);
        var manifestInfo = new FileInfo(manifestPath);
        if (!manifestInfo.Exists && options.AllowMissingManifestForGeneration)
        {
            return null;
        }

        if (!manifestInfo.Exists || manifestInfo.Length <= 0 || manifestInfo.Length > options.MaxManifestBytes)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        return manifestPath;
    }

    private static LoadedManifest CreateEmptyDynamicManifest()
    {
        var manifest = new WebFontManifest
        {
            ProfileId = "dynamic-uninitialized-v1"
        };
        return new LoadedManifest(
            manifest,
            JsonSerializer.SerializeToUtf8Bytes(manifest, SerializerOptions));
    }

    private static LoadedManifest LoadManifest(string manifestPath, OdfWebFontOptions options)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(manifestPath);
            WebFontManifest? manifest = JsonSerializer.Deserialize<WebFontManifest>(
                bytes,
                SerializerOptions);
            if (manifest is null
                || manifest.SchemaVersion != 1
                || string.IsNullOrWhiteSpace(manifest.ProfileId)
                || manifest.ProfileId.Length > 256
                || manifest.Assets.Count == 0
                || manifest.Assets.Count > options.MaxAssetCount)
            {
                throw new InvalidDataException(
                    OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
            }

            return new LoadedManifest(manifest, bytes);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"),
                exception);
        }
    }

    private static LoadedStylesheet? LoadOptionalCss(
        string rootPath,
        WebFontManifest manifest,
        OdfWebFontOptions options)
    {
        string? fileName = manifest.StylesheetFileName;
        string? expectedSha256 = manifest.StylesheetSha256;
        if (fileName is null && expectedSha256 is null)
        {
            fileName = "webfonts.css";
        }
        else if (!IsPlainFileName(fileName ?? string.Empty) || !IsSha256(expectedSha256 ?? string.Empty))
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        string path = Path.Combine(rootPath, fileName!);
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            if (manifest.StylesheetFileName is not null || expectedSha256 is not null)
            {
                throw new InvalidDataException(
                    OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
            }

            return null;
        }

        if (info.Length <= 0 || info.Length > options.MaxManifestBytes)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        byte[] bytes = File.ReadAllBytes(path);
        string actualSha256 = ComputeSha256(bytes);
        if (expectedSha256 is not null
            && !string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        try
        {
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetCharCount(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"),
                exception);
        }

        return new LoadedStylesheet(bytes, actualSha256);
    }

    private static Dictionary<string, StoredWebFontAsset> IndexAssets(
        string rootPath,
        WebFontManifest manifest,
        OdfWebFontOptions options)
    {
        var assets = new Dictionary<string, StoredWebFontAsset>(StringComparer.Ordinal);
        foreach (WebFontAsset descriptor in manifest.Assets)
        {
            if (!IsValidDescriptor(descriptor, options))
            {
                throw new InvalidDataException(
                    OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_AssetInvalid"));
            }

            string fullPath = Path.GetFullPath(Path.Combine(
                rootPath,
                descriptor.Sha256.ToLowerInvariant(),
                descriptor.FileName));
            if (!IsContainedPath(rootPath, fullPath))
            {
                throw new InvalidDataException(
                    OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_AssetInvalid"));
            }

            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists
                || fileInfo.LinkTarget is not null
                || new DirectoryInfo(fileInfo.DirectoryName!).LinkTarget is not null
                || fileInfo.Length != descriptor.ByteLength
                || fileInfo.Length > options.MaxAssetBytes
                || !string.Equals(ComputeSha256(fullPath), descriptor.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_AssetInvalid"));
            }

            string key = CreateKey(descriptor.Sha256, descriptor.FileName);
            if (!assets.TryAdd(
                    key,
                    new StoredWebFontAsset(
                        descriptor,
                        fullPath,
                        ResolveContentType(descriptor.Format),
                        new DateTimeOffset(fileInfo.LastWriteTimeUtc))))
            {
                throw new InvalidDataException(
                    OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
            }
        }

        return assets;
    }

    private static bool IsValidDescriptor(WebFontAsset descriptor, OdfWebFontOptions options)
        => IsPlainFileName(descriptor.FileName)
            && IsSha256(descriptor.Sha256)
            && descriptor.ByteLength > 0
            && descriptor.ByteLength <= options.MaxAssetBytes
            && !string.IsNullOrWhiteSpace(descriptor.FontFamily)
            && descriptor.FontFamily.Length <= 256
            && descriptor.UnicodeRanges.Count <= 4096
            && descriptor.UnicodeRanges.All(range => range.Length is > 2 and <= 64);

    private static bool IsPlainFileName(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 255
            && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');

    private static bool IsContainedPath(string rootPath, string candidatePath)
    {
        string rootWithSeparator = rootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return candidatePath.StartsWith(rootWithSeparator, comparison);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string CreateKey(string sha256, string fileName)
        => string.Concat(sha256.ToLowerInvariant(), ":", fileName);

    private static string ResolveContentType(WebFontFormat format)
        => format switch
        {
            WebFontFormat.Woff2 => "font/woff2",
            WebFontFormat.Woff => "font/woff",
            WebFontFormat.TrueType => "font/ttf",
            WebFontFormat.OpenType => "font/otf",
            _ => "application/octet-stream"
        };

    private static bool TryResolveFormat(string fileName, out WebFontFormat format)
    {
        string extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".woff2", StringComparison.OrdinalIgnoreCase))
        {
            format = WebFontFormat.Woff2;
            return true;
        }
        if (string.Equals(extension, ".woff", StringComparison.OrdinalIgnoreCase))
        {
            format = WebFontFormat.Woff;
            return true;
        }
        if (string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase))
        {
            format = WebFontFormat.TrueType;
            return true;
        }
        if (string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase))
        {
            format = WebFontFormat.OpenType;
            return true;
        }

        format = default;
        return false;
    }
}

internal sealed record LoadedManifest(WebFontManifest Manifest, byte[] Bytes);

internal sealed record LoadedStylesheet(byte[] Bytes, string Sha256);

internal sealed record StoredWebFontAsset(
    WebFontAsset Descriptor,
    string FullPath,
    string ContentType,
    DateTimeOffset LastModified);

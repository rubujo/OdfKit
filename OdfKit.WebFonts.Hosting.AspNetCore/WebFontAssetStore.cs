using System.Collections.Concurrent;
using System.Security.Cryptography;
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
    private readonly string _rootPath;
    private readonly OdfWebFontOptions _options;

    public WebFontAssetStore(IOptions<OdfWebFontOptions> optionsAccessor)
    {
        OdfWebFontOptions options = optionsAccessor.Value;
        string rootPath = ResolveRootPath(options);
        _rootPath = rootPath;
        _options = options;
        string manifestPath = ResolveManifestPath(rootPath, options);
        Manifest = LoadManifest(manifestPath, options);
        _assets = new ConcurrentDictionary<string, StoredWebFontAsset>(
            IndexAssets(rootPath, Manifest, options),
            StringComparer.Ordinal);
        Css = LoadOptionalCss(rootPath, Manifest, options);
    }

    public WebFontManifest Manifest { get; }

    public string? Css { get; }

    public string? StylesheetFileName => Manifest.StylesheetFileName;

    public string? StylesheetSha256 => Manifest.StylesheetSha256;

    public bool IsStylesheet(string fileName)
        => Css is not null
            && string.Equals(fileName, StylesheetFileName, StringComparison.Ordinal)
            && IsPlainFileName(fileName);

    public bool TryGetAsset(string sha256, string fileName, out StoredWebFontAsset? asset)
    {
        if (!IsSha256(sha256) || !IsPlainFileName(fileName))
        {
            asset = null;
            return false;
        }

        if (!_assets.TryGetValue(CreateKey(sha256, fileName), out asset)
            || asset is null)
        {
            return false;
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
        foreach ((string key, StoredWebFontAsset asset) in generated)
        {
            _assets.TryAdd(key, asset);
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

    private static string ResolveManifestPath(string rootPath, OdfWebFontOptions options)
    {
        if (!IsPlainFileName(options.ManifestFileName))
        {
            throw new InvalidOperationException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ConfigurationInvalid"));
        }

        string manifestPath = Path.Combine(rootPath, options.ManifestFileName);
        var manifestInfo = new FileInfo(manifestPath);
        if (!manifestInfo.Exists || manifestInfo.Length <= 0 || manifestInfo.Length > options.MaxManifestBytes)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        return manifestPath;
    }

    private static WebFontManifest LoadManifest(string manifestPath, OdfWebFontOptions options)
    {
        try
        {
            WebFontManifest? manifest = JsonSerializer.Deserialize<WebFontManifest>(
                File.ReadAllBytes(manifestPath),
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

            return manifest;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"),
                exception);
        }
    }

    private static string? LoadOptionalCss(
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
            return null;
        }

        if (info.Length <= 0 || info.Length > options.MaxManifestBytes)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        if (expectedSha256 is not null
            && !string.Equals(ComputeSha256(path), expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ManifestInvalid"));
        }

        return File.ReadAllText(path);
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
}

internal sealed record StoredWebFontAsset(
    WebFontAsset Descriptor,
    string FullPath,
    string ContentType,
    DateTimeOffset LastModified);

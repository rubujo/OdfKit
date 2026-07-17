using System.Collections.Concurrent;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using System.Web.Hosting;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Hosting.SystemWeb;

/// <summary>
/// Serves only manifest-allowlisted, content-addressed WebFont assets from a read-only directory.
/// 僅從唯讀目錄提供 manifest allowlist 中以內容定址的 WebFont 資產。
/// </summary>
public sealed class OdfWebFontHandler : IHttpHandler
{
    private static readonly Lazy<AssetCatalog> Catalog = new(LoadCatalog, isThreadSafe: true);
    private static readonly Lazy<bool> AllowPublicCrossOriginAssets = new(
        LoadAllowPublicCrossOriginAssets,
        isThreadSafe: true);
    private readonly AssetCatalog? _catalog;
    private readonly bool? _allowPublicCrossOriginAssets;

    /// <summary>
    /// Creates a handler that reads its trusted asset root from Web.config.
    /// 建立從 Web.config 讀取受信任資產根目錄的 Handler。
    /// </summary>
    public OdfWebFontHandler()
    {
    }

    internal OdfWebFontHandler(string assetRootPath, bool allowPublicCrossOriginAssets)
    {
        _catalog = AssetCatalog.Load(assetRootPath, 1_048_576, 512, 32L * 1024 * 1024);
        _allowPublicCrossOriginAssets = allowPublicCrossOriginAssets;
    }

    /// <inheritdoc />
    public bool IsReusable => true;

    /// <inheritdoc />
    public void ProcessRequest(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(
                nameof(context),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        AssetCatalog catalog = _catalog ?? Catalog.Value;
        bool allowPublicCrossOriginAssets = _allowPublicCrossOriginAssets ?? AllowPublicCrossOriginAssets.Value;
        string path = context.Request.Path.TrimEnd('/');
        if (path.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            WriteManifest(context, catalog);
            return;
        }

        if (path.EndsWith("/webfonts.css", StringComparison.OrdinalIgnoreCase))
        {
            WriteCss(context, catalog, immutable: false);
            return;
        }

        if (catalog.IsStylesheetPath(path))
        {
            WriteCss(context, catalog, immutable: true);
            return;
        }

        string[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            context.Response.StatusCode = 404;
            return;
        }

        string hash = segments[segments.Length - 2];
        string fileName = segments[segments.Length - 1];
        if (!catalog.TryGet(hash, fileName, out CatalogAsset? asset) || asset is null)
        {
            context.Response.StatusCode = 404;
            return;
        }

        string etag = $"\"{asset.Descriptor.Sha256}\"";
        HttpResponse response = context.Response;
        response.ContentType = asset.ContentType;
        response.Cache.SetCacheability(HttpCacheability.Public);
        response.Cache.SetMaxAge(TimeSpan.FromDays(365));
        response.Cache.SetExpires(DateTime.UtcNow.AddYears(1));
        response.Cache.AppendCacheExtension("immutable");
        response.Cache.SetETag(etag);
        response.AddHeader("X-Content-Type-Options", "nosniff");
        WriteCrossOriginHeaders(response, allowPublicCrossOriginAssets);
        if (IsNotModified(context.Request, etag))
        {
            response.StatusCode = 304;
            response.SuppressContent = true;
            return;
        }

        var info = new FileInfo(asset.FullPath);
        response.AddHeader(
            "Content-Length",
            info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            response.SuppressContent = true;
        }
        else
        {
            response.TransmitFile(asset.FullPath);
        }
    }

    private static bool LoadAllowPublicCrossOriginAssets()
    {
        string? configured = ConfigurationManager.AppSettings["OdfKit.WebFonts.AllowPublicCrossOriginAssets"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        if (!bool.TryParse(configured, out bool result))
        {
            throw new ConfigurationErrorsException(
                OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }

        return result;
    }

    private static void WriteCrossOriginHeaders(HttpResponse response, bool allowPublicCrossOriginAssets)
    {
        if (allowPublicCrossOriginAssets)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Cross-Origin-Resource-Policy", "cross-origin");
        }
        else
        {
            response.AddHeader("Cross-Origin-Resource-Policy", "same-origin");
        }
    }

    private static AssetCatalog LoadCatalog()
    {
        string? configuredRoot = ConfigurationManager.AppSettings["OdfKit.WebFonts.AssetRootPath"];
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new ConfigurationErrorsException(
                OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }

        string mappedRoot = configuredRoot.StartsWith("~", StringComparison.Ordinal)
            ? HostingEnvironment.MapPath(configuredRoot)
            : configuredRoot;
        return AssetCatalog.Load(mappedRoot, 1_048_576, 512, 32L * 1024 * 1024);
    }

    private static bool IsNotModified(HttpRequest request, string etag)
    {
        string? value = request.Headers["If-None-Match"];
        if (string.IsNullOrWhiteSpace(value) || value.Length > 8192)
        {
            return false;
        }

        foreach (string item in value.Split(','))
        {
            string candidate = item.Trim();
            if (candidate.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(2).TrimStart();
            }

            if (candidate == "*" || string.Equals(candidate, etag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteManifest(HttpContext context, AssetCatalog catalog)
    {
        HttpResponse response = context.Response;
        string etag = $"\"{catalog.ManifestSha256}\"";
        response.ContentType = "application/json; charset=utf-8";
        response.Cache.SetCacheability(HttpCacheability.Public);
        response.Cache.AppendCacheExtension("no-cache");
        response.Cache.SetETag(etag);
        response.AddHeader("X-Content-Type-Options", "nosniff");
        WriteBytes(context, catalog.ManifestBytes, etag);
    }

    private static void WriteCss(HttpContext context, AssetCatalog catalog, bool immutable)
    {
        HttpResponse response = context.Response;
        if (catalog.CssBytes is null || catalog.CssSha256 is null)
        {
            response.StatusCode = 404;
            return;
        }

        string etag = $"\"{catalog.CssSha256}\"";
        response.ContentType = "text/css; charset=utf-8";
        if (immutable)
        {
            response.Cache.SetCacheability(HttpCacheability.Public);
            response.Cache.SetMaxAge(TimeSpan.FromDays(365));
            response.Cache.AppendCacheExtension("immutable");
        }
        else
        {
            response.Cache.SetCacheability(HttpCacheability.Public);
            response.Cache.AppendCacheExtension("no-cache");
        }

        response.Cache.SetETag(etag);
        response.AddHeader("X-Content-Type-Options", "nosniff");
        WriteBytes(context, catalog.CssBytes, etag);
    }

    private static void WriteBytes(HttpContext context, byte[] bytes, string etag)
    {
        HttpResponse response = context.Response;
        if (IsNotModified(context.Request, etag))
        {
            response.StatusCode = 304;
            response.SuppressContent = true;
            return;
        }

        response.AddHeader(
            "Content-Length",
            bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            response.SuppressContent = true;
        }
        else
        {
            response.BinaryWrite(bytes);
        }
    }

    private sealed class AssetCatalog
    {
        private readonly ConcurrentDictionary<string, CatalogAsset> _assets;

        private AssetCatalog(
            byte[] manifestBytes,
            string manifestSha256,
            byte[]? cssBytes,
            string? cssSha256,
            string? stylesheetFileName,
            ConcurrentDictionary<string, CatalogAsset> assets)
        {
            ManifestBytes = manifestBytes;
            ManifestSha256 = manifestSha256;
            CssBytes = cssBytes;
            CssSha256 = cssSha256;
            StylesheetFileName = stylesheetFileName;
            _assets = assets;
        }

        public byte[] ManifestBytes { get; }

        public string ManifestSha256 { get; }

        public byte[]? CssBytes { get; }

        public string? CssSha256 { get; }

        public string? StylesheetFileName { get; }

        public bool IsStylesheetPath(string path)
            => StylesheetFileName is not null
                && path.EndsWith($"/{StylesheetFileName}", StringComparison.Ordinal);

        public bool TryGet(string hash, string fileName, out CatalogAsset? asset)
        {
            if (!IsHash(hash) || !IsPlainFileName(fileName))
            {
                asset = null;
                return false;
            }

            return _assets.TryGetValue($"{hash.ToLowerInvariant()}:{fileName}", out asset);
        }

        public static AssetCatalog Load(string root, long maxManifestBytes, int maxAssets, long maxAssetBytes)
        {
            string fullRoot = Path.GetFullPath(root);
            string manifestPath = Path.Combine(fullRoot, "webfonts.json");
            var manifestInfo = new FileInfo(manifestPath);
            if (!Directory.Exists(fullRoot)
                || !manifestInfo.Exists
                || manifestInfo.Length <= 0
                || manifestInfo.Length > maxManifestBytes)
            {
                throw new ConfigurationErrorsException(
                    OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
            }

            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            var jsonOptions = new JsonSerializerOptions
            {
                MaxDepth = 32,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            WebFontManifest? manifest = JsonSerializer.Deserialize<WebFontManifest>(manifestBytes, jsonOptions);
            if (manifest is null || manifest.SchemaVersion != 1 || manifest.Assets.Count is 0 || manifest.Assets.Count > maxAssets)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            var assets = new ConcurrentDictionary<string, CatalogAsset>(StringComparer.Ordinal);
            foreach (WebFontAsset descriptor in manifest.Assets)
            {
                string fullPath = Path.GetFullPath(Path.Combine(
                    fullRoot,
                    descriptor.Sha256.ToLowerInvariant(),
                    descriptor.FileName));
                var info = new FileInfo(fullPath);
                if (!IsPlainFileName(descriptor.FileName)
                    || !IsHash(descriptor.Sha256)
                    || !IsContained(fullRoot, fullPath)
                    || !info.Exists
                    || info.Length != descriptor.ByteLength
                    || info.Length <= 0
                    || info.Length > maxAssetBytes
                    || !string.Equals(ComputeHash(fullPath), descriptor.Sha256, StringComparison.OrdinalIgnoreCase)
                    || !assets.TryAdd(
                        $"{descriptor.Sha256.ToLowerInvariant()}:{descriptor.FileName}",
                        new CatalogAsset(descriptor, fullPath, ContentType(descriptor.Format))))
                {
                    throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
                }
            }

            string? stylesheetFileName = manifest.StylesheetFileName;
            string? stylesheetSha256 = manifest.StylesheetSha256;
            if (stylesheetFileName is null && stylesheetSha256 is null)
            {
                stylesheetFileName = "webfonts.css";
            }
            else if (!IsPlainFileName(stylesheetFileName ?? string.Empty)
                     || !IsHash(stylesheetSha256 ?? string.Empty))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            string cssPath = Path.Combine(fullRoot, stylesheetFileName!);
            var cssInfo = new FileInfo(cssPath);
            byte[]? cssBytes = cssInfo.Exists && cssInfo.Length is > 0 && cssInfo.Length <= maxManifestBytes
                ? File.ReadAllBytes(cssPath)
                : null;
            string? cssSha256 = cssBytes is null ? null : ComputeHash(cssBytes);
            if (cssBytes is not null
                && stylesheetSha256 is not null
                && !string.Equals(cssSha256, stylesheetSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            if (cssBytes is not null)
            {
                try
                {
                    _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                        .GetCharCount(cssBytes);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException(
                        OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"),
                        exception);
                }
            }

            return new AssetCatalog(
                manifestBytes,
                ComputeHash(manifestBytes),
                cssBytes,
                cssSha256,
                stylesheetFileName,
                assets);
        }

        private static bool IsHash(string value)
            => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

        private static bool IsPlainFileName(string value)
            => !string.IsNullOrWhiteSpace(value)
                && value.Length <= 255
                && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        private static bool IsContained(string root, string path)
            => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static string ComputeHash(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ComputeHash(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ContentType(WebFontFormat format)
            => format switch
            {
                WebFontFormat.Woff2 => "font/woff2",
                WebFontFormat.Woff => "font/woff",
                WebFontFormat.TrueType => "font/ttf",
                WebFontFormat.OpenType => "font/otf",
                _ => "application/octet-stream"
            };
    }

    private sealed record CatalogAsset(WebFontAsset Descriptor, string FullPath, string ContentType);
}

using System.Collections.Concurrent;
using System.Configuration;
using System.Security.Cryptography;
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

        string path = context.Request.Path.TrimEnd('/');
        if (path.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            WriteManifest(context.Response, Catalog.Value);
            return;
        }

        if (path.EndsWith("/webfonts.css", StringComparison.OrdinalIgnoreCase))
        {
            WriteCss(context.Response, Catalog.Value, immutable: false);
            return;
        }

        if (Catalog.Value.IsStylesheetPath(path))
        {
            WriteCss(context.Response, Catalog.Value, immutable: true);
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
        if (!Catalog.Value.TryGet(hash, fileName, out CatalogAsset? asset) || asset is null)
        {
            context.Response.StatusCode = 404;
            return;
        }

        string etag = $"\"{asset.Descriptor.Sha256}\"";
        if (string.Equals(context.Request.Headers["If-None-Match"], etag, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 304;
            context.Response.SuppressContent = true;
            return;
        }

        HttpResponse response = context.Response;
        response.ContentType = asset.ContentType;
        response.Cache.SetCacheability(HttpCacheability.Public);
        response.Cache.SetMaxAge(TimeSpan.FromDays(365));
        response.Cache.SetExpires(DateTime.UtcNow.AddYears(1));
        response.Headers["Cache-Control"] = "public,max-age=31536000,immutable";
        response.Headers["ETag"] = etag;
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.TransmitFile(asset.FullPath);
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

    private static void WriteManifest(HttpResponse response, AssetCatalog catalog)
    {
        response.ContentType = "application/json; charset=utf-8";
        response.Cache.SetCacheability(HttpCacheability.NoCache);
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Write(catalog.ManifestJson);
    }

    private static void WriteCss(HttpResponse response, AssetCatalog catalog, bool immutable)
    {
        if (catalog.Css is null)
        {
            response.StatusCode = 404;
            return;
        }

        response.ContentType = "text/css; charset=utf-8";
        if (immutable && catalog.StylesheetSha256 is not null)
        {
            response.Cache.SetCacheability(HttpCacheability.Public);
            response.Cache.SetMaxAge(TimeSpan.FromDays(365));
            response.Headers["Cache-Control"] = "public,max-age=31536000,immutable";
            response.Headers["ETag"] = $"\"{catalog.StylesheetSha256}\"";
        }
        else
        {
            response.Cache.SetCacheability(HttpCacheability.NoCache);
        }

        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Write(catalog.Css);
    }

    private sealed class AssetCatalog
    {
        private readonly ConcurrentDictionary<string, CatalogAsset> _assets;

        private AssetCatalog(
            string manifestJson,
            string? css,
            string? stylesheetFileName,
            string? stylesheetSha256,
            ConcurrentDictionary<string, CatalogAsset> assets)
        {
            ManifestJson = manifestJson;
            Css = css;
            StylesheetFileName = stylesheetFileName;
            StylesheetSha256 = stylesheetSha256;
            _assets = assets;
        }

        public string ManifestJson { get; }

        public string? Css { get; }

        public string? StylesheetFileName { get; }

        public string? StylesheetSha256 { get; }

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

            string json = File.ReadAllText(manifestPath);
            var jsonOptions = new JsonSerializerOptions
            {
                MaxDepth = 32,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            WebFontManifest? manifest = JsonSerializer.Deserialize<WebFontManifest>(json, jsonOptions);
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
            string? css = cssInfo.Exists && cssInfo.Length is > 0 && cssInfo.Length <= maxManifestBytes
                ? File.ReadAllText(cssPath)
                : null;
            if (css is not null
                && stylesheetSha256 is not null
                && !string.Equals(ComputeHash(cssPath), stylesheetSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            return new AssetCatalog(json, css, stylesheetFileName, stylesheetSha256, assets);
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

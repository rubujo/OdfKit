using System.Collections.Concurrent;
using System.Configuration;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using System.Web.Hosting;
using OdfKit.Compliance;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Hosting.SystemWeb;

/// <summary>
/// Generates bounded WebFont subsets through an authenticated endpoint and serves immutable results.
/// 透過須經授權的 endpoint 產生有界 WebFont 子集，並提供不可變結果。
/// </summary>
/// <remarks>
/// The parameterless IIS path requires an explicit JSON configuration and an API key environment variable.
/// IIS 無參數路徑必須明確提供 JSON 設定與 API key 環境變數。
/// </remarks>
public sealed class OdfWebFontDynamicHandler : IHttpHandler
{
    private const string ApiKeyHeader = "X-OdfKit-WebFont-Key";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = false,
        MaxDepth = 16,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Lazy<DynamicRuntime> DefaultRuntime = new(
        CreateDefaultRuntime,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly DynamicRuntime? _runtime;

    /// <summary>
    /// Initializes a handler that loads its trusted configuration from the application settings.
    /// 初始化從應用程式設定載入受信任設定的 Handler。
    /// </summary>
    public OdfWebFontDynamicHandler()
    {
    }

    /// <summary>
    /// Initializes a handler with an application-supplied managed engine and validated options.
    /// 使用應用程式提供的受控引擎與已驗證設定初始化 Handler。
    /// </summary>
    /// <param name="engine">The bounded subset engine. / 有界的子集引擎。</param>
    /// <param name="options">The trusted hosting options. / 受信任的託管設定。</param>
    public OdfWebFontDynamicHandler(
        IWebFontSubsetEngine engine,
        OdfWebFontSystemWebGenerationOptions options)
    {
        _runtime = new DynamicRuntime(engine, options);
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

        DynamicRuntime runtime;
        try
        {
            runtime = _runtime ?? DefaultRuntime.Value;
        }
        catch (Exception exception) when (exception is ConfigurationErrorsException
                                          or InvalidDataException
                                          or ArgumentException
                                          or IOException
                                          or UnauthorizedAccessException
                                          or SecurityException)
        {
            context.Response.StatusCode = 503;
            context.Response.TrySkipIisCustomErrors = true;
            return;
        }

        string method = context.Request.HttpMethod;
        string path = context.Request.Path.TrimEnd('/');
        if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/generate", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Generate(context, runtime);
            }
            finally
            {
                ApplyGenerationResponseHeaders(context.Response);
            }

            return;
        }

        if ((string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
             || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            && TryServeGeneratedAsset(context, runtime))
        {
            return;
        }

        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            string manifestPath = Path.Combine(runtime.AssetRootPath, "webfonts.json");
            if (File.Exists(manifestPath))
            {
                new OdfWebFontHandler().ProcessRequest(context);
                return;
            }
        }

        context.Response.StatusCode = 404;
        context.Response.TrySkipIisCustomErrors = true;
    }

    private static void Generate(HttpContext context, DynamicRuntime runtime)
    {
        HttpRequest request = context.Request;
        HttpResponse response = context.Response;
        response.TrySkipIisCustomErrors = true;
        if (!IsAuthorized(request.Headers[ApiKeyHeader], runtime.ApiKey))
        {
            response.StatusCode = 401;
            response.AddHeader("WWW-Authenticate", "OdfKitWebFont");
            return;
        }

        if (request.ContentType is null
            || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 415;
            return;
        }

        if (request.ContentLength > runtime.Options.MaxRequestBodyBytes)
        {
            response.StatusCode = 413;
            return;
        }

        OdfWebFontSystemWebGenerationRequest? generationRequest;
        try
        {
            byte[] body = ReadBoundedBody(request.InputStream, runtime.Options.MaxRequestBodyBytes);
            generationRequest = JsonSerializer.Deserialize<OdfWebFontSystemWebGenerationRequest>(
                body,
                SerializerOptions);
        }
        catch (InvalidDataException)
        {
            response.StatusCode = 413;
            return;
        }
        catch (JsonException)
        {
            response.StatusCode = 400;
            return;
        }

        if (!runtime.TryCreateSubsetRequest(generationRequest, out WebFontSubsetRequest subsetRequest))
        {
            response.StatusCode = 400;
            return;
        }

        if (!runtime.GenerationSlots.Wait(0))
        {
            response.StatusCode = 429;
            response.AddHeader("Retry-After", "1");
            return;
        }

        try
        {
            WebFontManifest manifest = runtime.Engine.GenerateAsync(
                    subsetRequest,
                    runtime.AssetRootPath,
                    GetClientDisconnectedToken(response))
                .GetAwaiter()
                .GetResult();
            response.ContentType = "application/json; charset=utf-8";
            response.Write(JsonSerializer.Serialize(manifest, SerializerOptions));
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or NotSupportedException)
        {
            response.StatusCode = 400;
        }
        catch (Exception exception) when (exception is IOException
                                          or InvalidDataException
                                          or InvalidOperationException
                                          or OperationCanceledException
                                          or UnauthorizedAccessException
                                          or CryptographicException
                                          or SecurityException)
        {
            response.StatusCode = 503;
        }
        finally
        {
            runtime.GenerationSlots.Release();
        }
    }

    private static void ApplyGenerationResponseHeaders(HttpResponse response)
    {
        response.Cache.SetCacheability(HttpCacheability.NoCache);
        response.Cache.SetNoStore();
        response.Cache.SetNoServerCaching();
        response.AddHeader("Pragma", "no-cache");
        response.AddHeader("X-Content-Type-Options", "nosniff");
    }

    private static bool TryServeGeneratedAsset(HttpContext context, DynamicRuntime runtime)
    {
        string[] segments = context.Request.Path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        string hash = segments[segments.Length - 2];
        string fileName = segments[segments.Length - 1];
        if (!IsHash(hash) || !IsPlainFileName(fileName) || !TryGetContentType(fileName, out string contentType))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(Path.Combine(runtime.AssetRootPath, hash.ToLowerInvariant(), fileName));
        if (!IsContained(runtime.AssetRootPath, fullPath))
        {
            return false;
        }

        var info = new FileInfo(fullPath);
        if (!info.Exists || info.Length <= 0 || info.Length > runtime.Options.MaxAssetBytes)
        {
            return false;
        }

        if (!runtime.TryVerifyAsset(info, hash, out string actualHash))
        {
            return false;
        }

        HttpResponse response = context.Response;
        string etag = $"\"{actualHash}\"";
        if (string.Equals(context.Request.Headers["If-None-Match"], etag, StringComparison.Ordinal))
        {
            response.StatusCode = 304;
            response.SuppressContent = true;
            return true;
        }

        response.ContentType = contentType;
        response.Cache.SetCacheability(HttpCacheability.Public);
        response.Cache.SetMaxAge(TimeSpan.FromDays(365));
        response.Cache.SetExpires(DateTime.UtcNow.AddYears(1));
        response.Cache.AppendCacheExtension("immutable");
        response.Cache.SetETag(etag);
        response.AddHeader("X-Content-Type-Options", "nosniff");
        if (runtime.Options.AllowPublicCrossOriginAssets)
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Cross-Origin-Resource-Policy", "cross-origin");
        }
        else
        {
            response.AddHeader("Cross-Origin-Resource-Policy", "same-origin");
        }
        response.AddHeader("Content-Length", info.Length.ToString(CultureInfo.InvariantCulture));
        if (string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            response.SuppressContent = true;
        }
        else
        {
            response.TransmitFile(fullPath);
        }

        return true;
    }

    private static byte[] ReadBoundedBody(Stream stream, int maximum)
    {
        using var buffer = new MemoryStream(Math.Min(maximum, 16 * 1024));
        byte[] chunk = new byte[Math.Min(maximum + 1, 8192)];
        while (true)
        {
            int read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximum)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static CancellationToken GetClientDisconnectedToken(HttpResponse response)
    {
        try
        {
            return response.ClientDisconnectedToken;
        }
        catch (PlatformNotSupportedException)
        {
            return CancellationToken.None;
        }
    }

    private static DynamicRuntime CreateDefaultRuntime()
    {
        string? configuredPath = ConfigurationManager.AppSettings["OdfKit.WebFonts.DynamicConfigurationPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw ConfigurationInvalid();
        }

        string configurationPath = MapTrustedPath(configuredPath, AppDomain.CurrentDomain.BaseDirectory);
        var info = new FileInfo(configurationPath);
        if (!info.Exists || info.Length <= 0 || info.Length > 1024 * 1024)
        {
            throw ConfigurationInvalid();
        }

        DynamicConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize<DynamicConfiguration>(
                File.ReadAllBytes(configurationPath),
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new ConfigurationErrorsException(
                OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"),
                exception);
        }

        if (configuration is null
            || configuration.SchemaVersion != 1
            || (string.IsNullOrWhiteSpace(configuration.ApiKeyEnvironmentVariable)
                && string.IsNullOrWhiteSpace(configuration.ApiKeyAppSettingName))
            || configuration.FontSources is not { Count: > 0 }
            || configuration.AllowedProfileIds is not { Count: > 0 }
            || configuration.AllowedFormats is not { Count: > 0 })
        {
            throw ConfigurationInvalid();
        }

        string? apiKey = string.IsNullOrWhiteSpace(configuration.ApiKeyEnvironmentVariable)
            ? null
            : Environment.GetEnvironmentVariable(configuration.ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(configuration.ApiKeyAppSettingName))
        {
            apiKey = ConfigurationManager.AppSettings[configuration.ApiKeyAppSettingName];
        }
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw ConfigurationInvalid();
        }

        string baseDirectory = Path.GetDirectoryName(configurationPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        var options = new OdfWebFontSystemWebGenerationOptions
        {
            AssetRootPath = MapTrustedPath(configuration.AssetRootPath, baseDirectory),
            ApiKey = apiKey!,
            MaxRequestBodyBytes = configuration.MaxRequestBodyBytes,
            MaxConcurrentGenerations = configuration.MaxConcurrentGenerations,
            MaxSequenceCount = configuration.MaxSequenceCount,
            MaxUnicodeScalarCount = configuration.MaxUnicodeScalarCount,
            MaxAssetBytes = configuration.MaxAssetBytes,
            AllowPublicCrossOriginAssets = configuration.AllowPublicCrossOriginAssets
        };
        var engineOptions = new ManagedOpenTypeWebFontEngineOptions
        {
            MaxOutputBytes = configuration.MaxAssetBytes,
            MaxUnicodeScalars = configuration.MaxUnicodeScalarCount
        };
        foreach (DynamicFontSource source in configuration.FontSources)
        {
            string sourcePath = MapTrustedPath(source.Path, baseDirectory);
            options.FontSources.Add(source.Id, sourcePath);
            options.AllowedFaces.Add(new WebFontFaceIdentity
            {
                FontSourceId = source.Id,
                SourceSha256 = source.Sha256,
                FaceIndex = source.FaceIndex
            });
            options.AllowedFontFamilies.Add(source.FontFamily);
            engineOptions.FontSources.Add(source.Id, sourcePath);
        }

        foreach (string profileId in configuration.AllowedProfileIds)
        {
            options.AllowedProfileIds.Add(profileId);
        }

        options.AllowedFormats.Clear();
        foreach (WebFontFormat format in configuration.AllowedFormats)
        {
            options.AllowedFormats.Add(format);
        }

        return new DynamicRuntime(new ManagedOpenTypeWebFontSubsetEngine(engineOptions), options);
    }

    private static string MapTrustedPath(string value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ConfigurationInvalid();
        }

        string? mapped = value.StartsWith("~", StringComparison.Ordinal)
            ? HostingEnvironment.MapPath(value)
            : value;
        if (string.IsNullOrWhiteSpace(mapped))
        {
            throw ConfigurationInvalid();
        }

        return Path.GetFullPath(Path.IsPathRooted(mapped) ? mapped : Path.Combine(baseDirectory, mapped));
    }

    private static bool IsAuthorized(string? supplied, string expected)
    {
        if (supplied is null || supplied.Length is 0 or > 512)
        {
            return false;
        }

        byte[] suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        int difference = suppliedBytes.Length ^ expectedBytes.Length;
        int maximum = Math.Max(suppliedBytes.Length, expectedBytes.Length);
        for (int index = 0; index < maximum; index++)
        {
            byte left = index < suppliedBytes.Length ? suppliedBytes[index] : (byte)0;
            byte right = index < expectedBytes.Length ? expectedBytes[index] : (byte)0;
            difference |= left ^ right;
        }

        return difference == 0;
    }

    private static bool IsHash(string value)
        => value.Length == 64 && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');

    private static bool IsPlainFileName(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 255
            && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool TryGetContentType(string fileName, out string contentType)
    {
        string extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".woff", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "font/woff";
            return true;
        }

        if (string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "font/ttf";
            return true;
        }

        if (string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "font/otf";
            return true;
        }

        contentType = string.Empty;
        return false;
    }

    private static bool IsContained(string root, string path)
        => path.StartsWith(
            root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static string ComputeHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static ConfigurationErrorsException ConfigurationInvalid()
        => new(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));

    private sealed class DynamicRuntime
    {
        private const int MaximumVerifiedAssetCacheEntries = 4096;
        private readonly ConcurrentDictionary<string, VerifiedAsset> _verifiedAssets =
            new(StringComparer.OrdinalIgnoreCase);

        public DynamicRuntime(IWebFontSubsetEngine engine, OdfWebFontSystemWebGenerationOptions options)
        {
            Engine = engine ?? throw new ArgumentNullException(
                nameof(engine),
                OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
            Options = options ?? throw new ArgumentNullException(
                nameof(options),
                OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
            ValidateOptions(options);
            AssetRootPath = Path.GetFullPath(options.AssetRootPath);
            Directory.CreateDirectory(AssetRootPath);
            ApiKey = options.ApiKey;
            GenerationSlots = new SemaphoreSlim(options.MaxConcurrentGenerations, options.MaxConcurrentGenerations);
        }

        public IWebFontSubsetEngine Engine { get; }

        public OdfWebFontSystemWebGenerationOptions Options { get; }

        public string AssetRootPath { get; }

        public string ApiKey { get; }

        public SemaphoreSlim GenerationSlots { get; }

        public bool TryVerifyAsset(FileInfo info, string expectedHash, out string actualHash)
        {
            long lastWriteTicks = info.LastWriteTimeUtc.Ticks;
            if (_verifiedAssets.TryGetValue(info.FullName, out VerifiedAsset? cached)
                && cached.ByteLength == info.Length
                && cached.LastWriteUtcTicks == lastWriteTicks
                && string.Equals(cached.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                actualHash = cached.Sha256;
                return true;
            }

            actualHash = ComputeHash(info.FullName);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_verifiedAssets.Count >= MaximumVerifiedAssetCacheEntries)
            {
                _verifiedAssets.Clear();
            }

            _verifiedAssets[info.FullName] = new VerifiedAsset(info.Length, lastWriteTicks, actualHash);
            return true;
        }

        public bool TryCreateSubsetRequest(
            OdfWebFontSystemWebGenerationRequest? request,
            out WebFontSubsetRequest subsetRequest)
        {
            subsetRequest = null!;
            if (request is null
                || string.IsNullOrWhiteSpace(request.FontSourceId)
                || request.FaceIndex < 0
                || string.IsNullOrWhiteSpace(request.ProfileId)
                || string.IsNullOrWhiteSpace(request.FontFamily)
                || request.FontFamily.Length > 256
                || request.Sequences is not { Count: > 0 }
                || request.Sequences.Count > Options.MaxSequenceCount
                || request.Sequences.Any(string.IsNullOrEmpty)
                || request.Formats is not { Count: > 0 }
                || request.Formats.Distinct().Count() != request.Formats.Count
                || request.Formats.Any(format => !Options.AllowedFormats.Contains(format))
                || !Options.AllowedProfileIds.Contains(request.ProfileId, StringComparer.Ordinal)
                || !Options.AllowedFontFamilies.Contains(request.FontFamily, StringComparer.Ordinal))
            {
                return false;
            }

            WebFontFaceIdentity? face = Options.AllowedFaces.SingleOrDefault(candidate =>
                string.Equals(candidate.FontSourceId, request.FontSourceId, StringComparison.Ordinal)
                && candidate.FaceIndex == request.FaceIndex);
            if (face is null)
            {
                return false;
            }

            try
            {
                WebFontTextSequence[] sequences = request.Sequences.Select(WebFontTextSequence.Create).ToArray();
                int scalarCount = sequences.Sum(sequence => sequence.UnicodeScalars.Count);
                if (scalarCount <= 0 || scalarCount > Options.MaxUnicodeScalarCount)
                {
                    return false;
                }

                subsetRequest = new WebFontSubsetRequest
                {
                    Face = new WebFontFaceIdentity
                    {
                        FontSourceId = face.FontSourceId,
                        SourceSha256 = face.SourceSha256,
                        FaceIndex = face.FaceIndex
                    },
                    ProfileId = request.ProfileId,
                    FontFamily = request.FontFamily,
                    Sequences = sequences,
                    Formats = request.Formats.ToArray()
                };
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void ValidateOptions(OdfWebFontSystemWebGenerationOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.AssetRootPath)
                || string.IsNullOrWhiteSpace(options.ApiKey)
                || Encoding.UTF8.GetByteCount(options.ApiKey) < 32
                || options.ApiKey.Length > 512
                || options.MaxRequestBodyBytes is <= 0 or > 1024 * 1024
                || options.MaxConcurrentGenerations is <= 0 or > 64
                || options.MaxSequenceCount is <= 0 or > 4096
                || options.MaxUnicodeScalarCount is <= 0 or > 65536
                || options.MaxAssetBytes is <= 0 or > 256L * 1024 * 1024
                || options.FontSources.Count is <= 0 or > 256
                || options.AllowedFaces.Count is <= 0 or > 256
                || options.AllowedProfileIds.Count is <= 0 or > 256
                || options.AllowedFontFamilies.Count is <= 0 or > 256
                || options.AllowedFormats.Count is <= 0 or > 2
                || options.AllowedFormats.Distinct().Count() != options.AllowedFormats.Count
                || options.AllowedFormats.Any(format => format is not WebFontFormat.Woff
                    and not WebFontFormat.TrueType
                    and not WebFontFormat.OpenType)
                || options.AllowedProfileIds.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256)
                || options.AllowedFontFamilies.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256)
                || options.AllowedFaces.Any(face => !IsValidFace(face, options.FontSources)))
            {
                throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
            }
        }

        private static bool IsValidFace(
            WebFontFaceIdentity? face,
            IDictionary<string, string> fontSources)
            => face is not null
                && !string.IsNullOrWhiteSpace(face.FontSourceId)
                && fontSources.ContainsKey(face.FontSourceId)
                && face.FaceIndex >= 0
                && IsHash(face.SourceSha256);
    }

    private sealed record VerifiedAsset(long ByteLength, long LastWriteUtcTicks, string Sha256);

    private sealed class DynamicConfiguration
    {
        public int SchemaVersion { get; set; }

        public string AssetRootPath { get; set; } = string.Empty;

        public string ApiKeyEnvironmentVariable { get; set; } = "ODFKIT_WEBFONT_API_KEY";

        public string ApiKeyAppSettingName { get; set; } = "OdfKit.WebFonts.ApiKey";

        public int MaxRequestBodyBytes { get; set; } = 64 * 1024;

        public int MaxConcurrentGenerations { get; set; } = 2;

        public int MaxSequenceCount { get; set; } = 256;

        public int MaxUnicodeScalarCount { get; set; } = 4096;

        public long MaxAssetBytes { get; set; } = 32L * 1024 * 1024;

        public bool AllowPublicCrossOriginAssets { get; set; }

        public List<DynamicFontSource> FontSources { get; set; } = new();

        public List<string> AllowedProfileIds { get; set; } = new();

        public List<WebFontFormat> AllowedFormats { get; set; } = new();
    }

    private sealed class DynamicFontSource
    {
        public string Id { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public int FaceIndex { get; set; }

        public string FontFamily { get; set; } = string.Empty;
    }
}

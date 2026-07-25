using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using OdfKit.WebFonts;
using OdfKit.WebFonts.Hosting.SystemWeb;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Sidecar;

string? fontPath = GetArgument(args, "--font");
string? sourceSha256 = GetArgument(args, "--sha256");
string? sidecarPipeName = GetArgument(args, "--sidecar-pipe");
string text = GetArgument(args, "--text") ?? "𪚥 𩙡 𦚡 𨏿 𠆩 𡘙 𡌂 𠀀一二三丨ㄩ幹";
bool usePostScriptOutline = args.Contains("--postscript", StringComparer.Ordinal);
bool sidecarOnly = args.Contains("--sidecar-only", StringComparer.Ordinal);
string root = Path.GetFullPath(
    GetArgument(args, "--asset-root")
        ?? Path.Combine(Path.GetTempPath(), "odfkit-systemweb-smoke-" + Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);

try
{
    IWebFontSubsetEngine engine;
    var options = new OdfWebFontSystemWebGenerationOptions
    {
        AssetRootPath = root,
        ApiKey = "system-web-smoke-key-32-bytes-minimum",
        AllowPublicCrossOriginAssets = true,
        MaxConcurrentGenerations = 1,
        MaxSequenceCount = 8,
        MaxUnicodeScalarCount = 4096
    };
    options.AllowedProfileIds.Add("smoke-profile@1");
    options.AllowedFontFamilies.Add("OdfKit SystemWeb Smoke");
    options.AllowedFormats.Clear();
    options.AllowedFormats.Add(WebFontFormat.Woff);
    options.AllowedFormats.Add(usePostScriptOutline ? WebFontFormat.OpenType : WebFontFormat.TrueType);

    if (fontPath is not null || sourceSha256 is not null)
    {
        if (fontPath is null
            || sourceSha256 is null
            || !File.Exists(fontPath)
            || !string.Equals(ComputeHash(fontPath), sourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The real System.Web smoke font contract is invalid.");
        }

        options.FontSources.Add("smoke-source", Path.GetFullPath(fontPath));
        options.AllowedFaces.Add(new WebFontFaceIdentity
        {
            FontSourceId = "smoke-source",
            SourceSha256 = sourceSha256,
            FaceIndex = 0
        });
        var engineOptions = new ManagedOpenTypeWebFontEngineOptions
        {
            MaxUnicodeScalars = 4096,
            MaxOutputBytes = 32L * 1024 * 1024
        };
        engineOptions.FontSources.Add("smoke-source", Path.GetFullPath(fontPath));
        engine = new ManagedOpenTypeWebFontSubsetEngine(engineOptions);
    }
    else
    {
        options.FontSources.Add("smoke-source", "programmatic-smoke-source.ttf");
        options.AllowedFaces.Add(new WebFontFaceIdentity
        {
            FontSourceId = "smoke-source",
            SourceSha256 = new string('a', 64),
            FaceIndex = 0
        });
        engine = new DeterministicSmokeEngine();
    }

    var handler = new OdfWebFontDynamicHandler(engine, options);
    options.AllowedFormats.Add(WebFontFormat.Woff2);
    var managedFallbackHandler = new OdfWebFontDynamicHandler(
        new FailIfCalledSmokeEngine(),
        engine,
        options);
    foreach ((WebFontFormat requested, WebFontFormat expected) in new[]
             {
                 (WebFontFormat.Woff2, WebFontFormat.Woff),
                 (WebFontFormat.Woff, WebFontFormat.Woff),
                 (usePostScriptOutline ? WebFontFormat.OpenType : WebFontFormat.TrueType,
                     usePostScriptOutline ? WebFontFormat.OpenType : WebFontFormat.TrueType)
             })
    {
        var fallbackRequest = new RecordingWorkerRequest(
            "POST",
            "/_odf-fonts/generate",
            JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
            {
                FontSourceId = "smoke-source",
                FaceIndex = 0,
                ProfileId = "smoke-profile@1",
                FontFamily = "OdfKit SystemWeb Smoke",
                Sequences = new[] { "A𠆩" },
                Formats = new[] { requested }
            }),
            options.ApiKey,
            backend: "managed");
        var fallbackContext = new HttpContext(fallbackRequest);
        managedFallbackHandler.ProcessRequest(fallbackContext);
        fallbackContext.Response.Flush();
        Require(
            fallbackContext.Response.StatusCode == 200,
            $"Managed fallback did not generate {requested}.");
        WebFontManifest fallbackManifest = JsonSerializer.Deserialize<WebFontManifest>(
            fallbackRequest.ResponseText,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            })!;
        Require(
            fallbackManifest.Assets.Count > 0
            && fallbackManifest.Assets.All(asset => asset.Format == expected),
            $"Managed fallback returned the wrong format for {requested}.");
    }

    var unavailableFallbackRequest = new RecordingWorkerRequest(
        "POST",
        "/_odf-fonts/generate",
        JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
        {
            FontSourceId = "smoke-source",
            FaceIndex = 0,
            ProfileId = "smoke-profile@1",
            FontFamily = "OdfKit SystemWeb Smoke",
            Sequences = new[] { "A" },
            Formats = new[] { WebFontFormat.Woff }
        }),
        options.ApiKey,
        backend: "managed");
    var unavailableFallbackContext = new HttpContext(unavailableFallbackRequest);
    handler.ProcessRequest(unavailableFallbackContext);
    unavailableFallbackContext.Response.Flush();
    Require(
        unavailableFallbackContext.Response.StatusCode == 400,
        "Managed fallback was accepted when it was not configured.");
    options.AllowedFormats.Remove(WebFontFormat.Woff2);

    if (sidecarPipeName is not null)
    {
        string sidecarToken = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_SIDECAR_TOKEN")
            ?? throw new InvalidOperationException("The sidecar token environment variable is missing.");
        options.AllowedFormats.Add(WebFontFormat.Woff2);
        var sidecarHandler = new OdfWebFontDynamicHandler(
            new OdfWebFontSidecarClient(new WebFontSidecarClientOptions
            {
                PipeName = sidecarPipeName,
                AuthenticationToken = sidecarToken,
                AssetRootPath = root,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                RequestTimeout = TimeSpan.FromMinutes(3)
            }),
            options);
        var sidecarRequest = new RecordingWorkerRequest(
            "POST",
            "/_odf-fonts/generate",
            JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
            {
                FontSourceId = "smoke-source",
                FaceIndex = 0,
                ProfileId = "smoke-profile@1",
                FontFamily = "OdfKit SystemWeb Smoke",
                Sequences = new[] { "OdfKit" },
                Formats = new[] { WebFontFormat.Woff2 }
            }),
            options.ApiKey);
        var sidecarContext = new HttpContext(sidecarRequest);
        sidecarHandler.ProcessRequest(sidecarContext);
        sidecarContext.Response.Flush();
        Require(sidecarContext.Response.StatusCode == 200, "System.Web did not generate WOFF2 through the sidecar.");
        Require(
            Directory.GetFiles(root, "*.woff2", SearchOption.AllDirectories).Length == 1,
            "System.Web sidecar generation returned no WOFF2 asset.");
        options.AllowedFormats.Remove(WebFontFormat.Woff2);
        Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);
        if (sidecarOnly)
        {
            Console.WriteLine("PASS: System.Web generated WOFF2 through the NativeAOT sidecar.");
            return 0;
        }
    }

    string json = JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
    {
        FontSourceId = "smoke-source",
        FaceIndex = 0,
        ProfileId = "smoke-profile@1",
        FontFamily = "OdfKit SystemWeb Smoke",
        Sequences = new[] { text },
        Formats = new[]
        {
            WebFontFormat.Woff,
            usePostScriptOutline ? WebFontFormat.OpenType : WebFontFormat.TrueType
        }
    });

    var unauthorized = new RecordingWorkerRequest("POST", "/_odf-fonts/generate", json, null);
    var unauthorizedContext = new HttpContext(unauthorized);
    handler.ProcessRequest(unauthorizedContext);
    unauthorizedContext.Response.Flush();
    Require(unauthorizedContext.Response.StatusCode == 401, "System.Web dynamic endpoint did not reject a missing API key.");
    RequireNoStore(unauthorized, "unauthorized generation response");

    var invalidFormat = new RecordingWorkerRequest(
        "POST",
        "/_odf-fonts/generate",
        JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
        {
            FontSourceId = "smoke-source",
            FaceIndex = 0,
            ProfileId = "smoke-profile@1",
            FontFamily = "OdfKit SystemWeb Smoke",
            Sequences = new[] { "A𠆩" },
            Formats = new[] { WebFontFormat.Woff2 }
        }),
        options.ApiKey);
    var invalidFormatContext = new HttpContext(invalidFormat);
    handler.ProcessRequest(invalidFormatContext);
    invalidFormatContext.Response.Flush();
    Require(invalidFormatContext.Response.StatusCode == 400, "System.Web dynamic endpoint did not reject WOFF2 on net48.");
    RequireNoStore(invalidFormat, "invalid generation response");

    var classifiedHandler = new OdfWebFontDynamicHandler(new ClassifiedFailureSmokeEngine(), options);
    foreach ((string scenario, int expectedStatus) in new[]
    {
        ("argument", 204),
        ("unsupported", 422),
        ("invalid-data", 500),
        ("io", 503),
        ("invalid-operation", 500),
        ("queue-full", 429),
        ("cancelled", 499),
        ("timeout", 503),
        ("unexpected", 500)
    })
    {
        string classifiedJson = JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
        {
            FontSourceId = "smoke-source",
            FaceIndex = 0,
            ProfileId = "smoke-profile@1",
            FontFamily = "OdfKit SystemWeb Smoke",
            Sequences = new[] { scenario },
            Formats = new[] { WebFontFormat.Woff }
        });
        var classified = new RecordingWorkerRequest(
            "POST",
            "/_odf-fonts/generate",
            classifiedJson,
            options.ApiKey);
        var classifiedContext = new HttpContext(classified);
        classifiedHandler.ProcessRequest(classifiedContext);
        classifiedContext.Response.Flush();
        Require(
            classifiedContext.Response.StatusCode == expectedStatus,
            $"System.Web classified {scenario} as {classifiedContext.Response.StatusCode}, expected {expectedStatus}.");
        RequireNoStore(classified, $"{scenario} generation response");
    }

    using (var blockingEngine = new BlockingSmokeEngine())
    {
        var limitedHandler = new OdfWebFontDynamicHandler(blockingEngine, options);
        var firstWorker = new RecordingWorkerRequest("POST", "/_odf-fonts/generate", json, options.ApiKey);
        Task firstGeneration = Task.Run(() =>
        {
            var firstContext = new HttpContext(firstWorker);
            limitedHandler.ProcessRequest(firstContext);
            firstContext.Response.Flush();
        });
        Require(blockingEngine.Started.Wait(TimeSpan.FromSeconds(5)), "System.Web bounded generation did not start.");
        var saturatedWorker = new RecordingWorkerRequest("POST", "/_odf-fonts/generate", json, options.ApiKey);
        var saturatedContext = new HttpContext(saturatedWorker);
        limitedHandler.ProcessRequest(saturatedContext);
        saturatedContext.Response.Flush();
        Require(saturatedContext.Response.StatusCode == 429, "System.Web dynamic endpoint did not enforce bounded concurrency.");
        RequireNoStore(saturatedWorker, "rate-limited generation response");
        blockingEngine.Release.Set();
        Require(firstGeneration.Wait(TimeSpan.FromSeconds(5)), "System.Web bounded generation did not finish.");
    }
    Directory.Delete(root, recursive: true);
    Directory.CreateDirectory(root);

    var normalOnlyJson = JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
    {
        FontSourceId = "smoke-source",
        FaceIndex = 0,
        ProfileId = "smoke-profile@1",
        FontFamily = "OdfKit SystemWeb Smoke",
        Sequences = new[] { "一二三丨ㄩ幹" },
        Formats = new[]
        {
            WebFontFormat.Woff,
            usePostScriptOutline ? WebFontFormat.OpenType : WebFontFormat.TrueType
        }
    });
    var normalOnly = new RecordingWorkerRequest(
        "POST",
        "/_odf-fonts/generate",
        normalOnlyJson,
        options.ApiKey);
    var normalOnlyContext = new HttpContext(normalOnly);
    handler.ProcessRequest(normalOnlyContext);
    normalOnlyContext.Response.Flush();
    int expectedNormalOnlyStatus = fontPath is not null && usePostScriptOutline ? 200 : 204;
    Require(
        normalOnlyContext.Response.StatusCode == expectedNormalOnlyStatus,
        $"System.Web normal-only request returned {normalOnlyContext.Response.StatusCode}, expected {expectedNormalOnlyStatus}.");
    RequireNoStore(normalOnly, "normal-only generation response");
    Directory.Delete(root, recursive: true);
    Directory.CreateDirectory(root);

    string largeMixed = string.Concat(
        Enumerable.Range(0, 4080).Select(index => char.ConvertFromUtf32(0x20000 + index)))
        + "一二三丨ㄩ幹";
    string largeJson = JsonSerializer.Serialize(new OdfWebFontSystemWebGenerationRequest
    {
        FontSourceId = "smoke-source",
        FaceIndex = 0,
        ProfileId = "smoke-profile@1",
        FontFamily = "OdfKit SystemWeb Smoke",
        Sequences = new[] { largeMixed },
        Formats = new[]
        {
            WebFontFormat.Woff,
            usePostScriptOutline ? WebFontFormat.OpenType : WebFontFormat.TrueType
        }
    });
    var largeRequest = new RecordingWorkerRequest(
        "POST",
        "/_odf-fonts/generate",
        largeJson,
        options.ApiKey);
    var largeContext = new HttpContext(largeRequest);
    handler.ProcessRequest(largeContext);
    largeContext.Response.Flush();
    Require(largeContext.Response.StatusCode == 200, "System.Web 4,080-scalar mixed request failed.");
    RequireNoStore(largeRequest, "4,080-scalar mixed generation response");
    Directory.Delete(root, recursive: true);
    Directory.CreateDirectory(root);

    var generated = new RecordingWorkerRequest("POST", "/_odf-fonts/generate", json, options.ApiKey);
    var generatedContext = new HttpContext(generated);
    handler.ProcessRequest(generatedContext);
    generatedContext.Response.Flush();
    Require(generatedContext.Response.StatusCode == 200, "System.Web dynamic endpoint did not generate assets.");
    RequireNoStore(generated, "successful generation response");
    Require(generated.ResponseText.Contains("smoke-profile@1"), "System.Web dynamic endpoint returned no manifest body.");
    var manifestOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    WebFontManifest generatedManifest = JsonSerializer.Deserialize<WebFontManifest>(
        generated.ResponseBytes,
        manifestOptions) ?? throw new InvalidDataException("The generated manifest was empty.");
    string[] generatedPaths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
    Require(generatedPaths.Length == 2, "System.Web dynamic endpoint returned an incomplete manifest.");

    foreach (string generatedPath in generatedPaths)
    {
        string hash = Path.GetFileName(Path.GetDirectoryName(generatedPath));
        string fileName = Path.GetFileName(generatedPath);
        WebFontFormat format = string.Equals(Path.GetExtension(fileName), ".woff", StringComparison.OrdinalIgnoreCase)
            ? WebFontFormat.Woff
            : usePostScriptOutline ? WebFontFormat.OpenType : WebFontFormat.TrueType;
        var assetRequest = new RecordingWorkerRequest(
            "GET",
            $"/_odf-fonts/{hash}/{fileName}",
            null,
            null);
        var assetContext = new HttpContext(assetRequest);
        handler.ProcessRequest(assetContext);
        assetContext.Response.Flush();
        Require(assetContext.Response.StatusCode == 200, "System.Web dynamic endpoint did not serve an immutable asset.");
        assetRequest.ResponseHeaders.TryGetValue("ETag", out string? etag);
        Require(
            string.Equals(etag, $"\"{hash}\"", StringComparison.Ordinal),
            "System.Web dynamic endpoint returned an invalid ETag: "
            + string.Join(",", assetRequest.ResponseHeaders.Select(pair => $"{pair.Key}={pair.Value}")));
        Require(
            assetRequest.ResponseHeaders.TryGetValue("Access-Control-Allow-Origin", out string? allowOrigin)
            && string.Equals(allowOrigin, "*", StringComparison.Ordinal),
            "System.Web CDN asset did not emit wildcard CORS.");
        Require(
            assetRequest.ResponseHeaders.TryGetValue("Cross-Origin-Resource-Policy", out string? resourcePolicy)
            && string.Equals(resourcePolicy, "cross-origin", StringComparison.Ordinal),
            "System.Web CDN asset did not emit cross-origin CORP.");

        if (fontPath is not null)
        {
            IWebFontTextCoverageFilter coverageFilter = (IWebFontTextCoverageFilter)engine;
            IReadOnlyList<WebFontTextSequence> supportedSequences = coverageFilter
                .FilterSupportedSequencesAsync(
                    options.AllowedFaces.Single(),
                    new[] { WebFontTextSequence.Create(text) })
                .GetAwaiter()
                .GetResult();
            using FileStream stream = File.OpenRead(generatedPath);
            ManagedOpenTypeWebFontVerifier.VerifyContainsSequences(
                stream,
                format,
                supportedSequences);
        }
    }

    byte[] cssBytes = Encoding.UTF8.GetBytes("\uFEFF@font-face { font-family: 'OdfKit SystemWeb Smoke'; }\n");
    string cssSha256 = ComputeBytesHash(cssBytes);
    string cssFileName = $"webfonts.{cssSha256.Substring(0, 16)}.css";
    File.WriteAllBytes(Path.Combine(root, cssFileName), cssBytes);
    var staticManifest = new WebFontManifest
    {
        ProfileId = generatedManifest.ProfileId,
        Assets = generatedManifest.Assets,
        StylesheetFileName = cssFileName,
        StylesheetSha256 = cssSha256
    };
    byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(staticManifest, manifestOptions);
    File.WriteAllBytes(Path.Combine(root, "webfonts.json"), manifestBytes);
    var staticHandler = new OdfWebFontHandler(root, allowPublicCrossOriginAssets: true);

    var manifestRequest = new RecordingWorkerRequest("GET", "/_odf-fonts/manifest.json", null, null);
    var manifestContext = new HttpContext(manifestRequest);
    staticHandler.ProcessRequest(manifestContext);
    manifestContext.Response.Flush();
    Require(manifestContext.Response.StatusCode == 200, "System.Web static manifest GET failed.");
    Require(manifestRequest.ResponseBytes.SequenceEqual(manifestBytes), "System.Web static manifest changed the source bytes.");
    Require(
        manifestRequest.ResponseHeaders.TryGetValue("ETag", out string? manifestEtag),
        "System.Web static manifest emitted no ETag. Headers: "
        + string.Join(", ", manifestRequest.ResponseHeaders.Select(pair => $"{pair.Key}={pair.Value}")));

    var manifestConditional = new RecordingWorkerRequest(
        "GET",
        "/_odf-fonts/manifest.json",
        null,
        null,
        $"W/{manifestEtag}");
    var manifestConditionalContext = new HttpContext(manifestConditional);
    staticHandler.ProcessRequest(manifestConditionalContext);
    manifestConditionalContext.Response.Flush();
    Require(manifestConditionalContext.Response.StatusCode == 304, "System.Web static manifest did not revalidate to 304.");
    Require(manifestConditional.ResponseBytes.Length == 0, "System.Web static manifest 304 returned a body.");

    var cssRequest = new RecordingWorkerRequest("GET", $"/_odf-fonts/{cssFileName}", null, null);
    var cssContext = new HttpContext(cssRequest);
    staticHandler.ProcessRequest(cssContext);
    cssContext.Response.Flush();
    Require(cssContext.Response.StatusCode == 200, "System.Web fingerprinted CSS GET failed.");
    Require(cssRequest.ResponseBytes.SequenceEqual(cssBytes), "System.Web fingerprinted CSS changed the source bytes.");
    Require(
        cssRequest.ResponseHeaders.TryGetValue("Cache-Control", out string? cssCacheControl)
        && cssCacheControl.Contains("immutable", StringComparison.OrdinalIgnoreCase),
        "System.Web fingerprinted CSS was not immutable.");

    WebFontAsset staticAsset = generatedManifest.Assets[0];
    var assetHead = new RecordingWorkerRequest(
        "HEAD",
        $"/_odf-fonts/{staticAsset.Sha256}/{staticAsset.FileName}",
        null,
        null);
    var assetHeadContext = new HttpContext(assetHead);
    staticHandler.ProcessRequest(assetHeadContext);
    assetHeadContext.Response.Flush();
    Require(assetHeadContext.Response.StatusCode == 200, "System.Web static asset HEAD failed.");
    Require(assetHead.ResponseBytes.Length == 0, "System.Web static asset HEAD returned a body.");

    string invalidCssRoot = Path.Combine(root, "invalid-css-corpus");
    string invalidAssetDirectory = Path.Combine(invalidCssRoot, staticAsset.Sha256);
    Directory.CreateDirectory(invalidAssetDirectory);
    File.Copy(
        Path.Combine(root, staticAsset.Sha256, staticAsset.FileName),
        Path.Combine(invalidAssetDirectory, staticAsset.FileName));
    File.WriteAllBytes(Path.Combine(invalidCssRoot, "webfonts.css"), new byte[] { 0xC3, 0x28 });
    File.WriteAllBytes(
        Path.Combine(invalidCssRoot, "webfonts.json"),
        JsonSerializer.SerializeToUtf8Bytes(new WebFontManifest
        {
            ProfileId = staticManifest.ProfileId,
            Assets = new[] { staticAsset }
        }, manifestOptions));
    bool invalidCssRejected = false;
    try
    {
        _ = new OdfWebFontHandler(invalidCssRoot, allowPublicCrossOriginAssets: false);
    }
    catch (InvalidDataException)
    {
        invalidCssRejected = true;
    }

    Require(invalidCssRejected, "System.Web static handler accepted invalid UTF-8 CSS.");

    IHttpHandler configuredStaticHandler = new OdfWebFontHandler();
    string markup = OdfWebFontHtml.StylesheetLink().ToHtmlString();
    Require(configuredStaticHandler.IsReusable && handler.IsReusable, "System.Web handlers are not reusable.");
    Require(markup.Contains("/_odf-fonts/webfonts.css"), "System.Web stylesheet helper returned an invalid URL.");

    Console.WriteLine(fontPath is null
        ? "PASS: System.Web authenticated dynamic handler contract loaded."
        : usePostScriptOutline
            ? "PASS: System.Web generated and verified real CFF2 OTF/WOFF assets on CLR net48."
            : "PASS: System.Web generated and verified real WOFF/TTF assets on CLR net48.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.GetType().FullName);
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(exception.StackTrace);
    return 1;
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

static string? GetArgument(string[] values, string name)
{
    int index = Array.IndexOf(values, name);
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}

static string ComputeHash(string path)
{
    using FileStream stream = File.OpenRead(path);
    using SHA256 sha256 = SHA256.Create();
    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
}

static string ComputeBytesHash(byte[] bytes)
{
    using SHA256 sha256 = SHA256.Create();
    return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
}

static void RequireNoStore(RecordingWorkerRequest request, string scenario)
{
    Require(
        request.ResponseHeaders.TryGetValue("Cache-Control", out string? cacheControl)
        && cacheControl.Contains("no-store", StringComparison.OrdinalIgnoreCase),
        $"System.Web {scenario} did not emit Cache-Control: no-store. Headers: "
        + string.Join(", ", request.ResponseHeaders.Select(pair => $"{pair.Key}={pair.Value}")));
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class DeterministicSmokeEngine : IWebFontSubsetEngine, IWebFontTextCoverageFilter
{
    public Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
        WebFontFaceIdentity face,
        IReadOnlyList<WebFontTextSequence> sequences,
        CancellationToken cancellationToken = default)
    {
        string supported = string.Concat(sequences
            .SelectMany(sequence => sequence.UnicodeScalars)
            .Where(scalar => scalar is >= 0x20000 and <= 0x2FFFF)
            .Select(char.ConvertFromUtf32));
        IReadOnlyList<WebFontTextSequence> result = supported.Length == 0
            ? Array.Empty<WebFontTextSequence>()
            : new[] { WebFontTextSequence.Create(supported) };
        return Task.FromResult(result);
    }

    public Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        var assets = new List<WebFontAsset>();
        foreach (WebFontFormat format in request.Formats)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes = Encoding.UTF8.GetBytes($"{request.ProfileId}:{request.FontFamily}:{format}:A𠆩");
            string hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }

            string extension = format == WebFontFormat.Woff ? "woff" : "ttf";
            string fileName = $"systemweb.{hash.Substring(0, 16)}.{extension}";
            string directory = Path.Combine(destinationDirectory, hash);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
            assets.Add(new WebFontAsset
            {
                FileName = fileName,
                Sha256 = hash,
                ByteLength = bytes.LongLength,
                Format = format,
                FontFamily = request.FontFamily,
                UnicodeRanges = new[] { "U+41", "U+201A9" }
            });
        }

        return Task.FromResult(new WebFontManifest
        {
            ProfileId = request.ProfileId,
            Assets = assets
        });
    }
}

internal sealed class FailIfCalledSmokeEngine : IWebFontSubsetEngine
{
    public Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string outputDirectory,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The primary engine must not run for a managed fallback request.");
}

internal sealed class ClassifiedFailureSmokeEngine : IWebFontSubsetEngine
{
    public Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
        => Task.FromException<WebFontManifest>(request.Sequences[0].Text switch
        {
            "argument" => new ArgumentException(),
            "unsupported" => new NotSupportedException(),
            "invalid-data" => new InvalidDataException(),
            "io" => new IOException(),
            "invalid-operation" => new InvalidOperationException(),
            "queue-full" => new WebFontSidecarQueueFullException(),
            "cancelled" => new OperationCanceledException(),
            "timeout" => new TimeoutException(),
            "unexpected" => new NullReferenceException(),
            _ => new InvalidOperationException()
        });
}

internal sealed class BlockingSmokeEngine : IWebFontSubsetEngine, IDisposable
{
    private readonly DeterministicSmokeEngine _inner = new();

    public ManualResetEventSlim Started { get; } = new(initialState: false);

    public ManualResetEventSlim Release { get; } = new(initialState: false);

    public Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        Started.Set();
        Release.Wait(cancellationToken);
        return _inner.GenerateAsync(request, destinationDirectory, cancellationToken);
    }

    public void Dispose()
    {
        Started.Dispose();
        Release.Dispose();
    }
}

internal sealed class RecordingWorkerRequest : HttpWorkerRequest
{
    private readonly byte[] _body;
    private readonly string _method;
    private readonly string _path;
    private readonly string? _apiKey;
    private readonly string? _backend;
    private readonly string? _ifNoneMatch;
    private readonly MemoryStream _responseBody = new();

    public RecordingWorkerRequest(
        string method,
        string path,
        string? body,
        string? apiKey,
        string? ifNoneMatch = null,
        string? backend = null)
    {
        _method = method;
        _path = path;
        _body = body is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
        _apiKey = apiKey;
        _ifNoneMatch = ifNoneMatch;
        _backend = backend;
    }

    public int StatusCode { get; private set; } = 200;

    public IDictionary<string, string> ResponseHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public byte[] ResponseBytes => _responseBody.ToArray();

    public string ResponseText => Encoding.UTF8.GetString(ResponseBytes);

    public override string GetHttpVerbName() => _method;

    public override string GetHttpVersion() => "HTTP/1.0";

    public override string GetLocalAddress() => "127.0.0.1";

    public override int GetLocalPort() => 80;

    public override string GetQueryString() => string.Empty;

    public override string GetRawUrl() => _path;

    public override string GetRemoteAddress() => "127.0.0.1";

    public override int GetRemotePort() => 49152;

    public override string GetUriPath() => _path;

    public override byte[] GetPreloadedEntityBody() => _body;

    public override int GetPreloadedEntityBodyLength() => _body.Length;

    public override int GetTotalEntityBodyLength() => _body.Length;

    public override bool IsEntireEntityBodyIsPreloaded() => true;

    public override string? GetKnownRequestHeader(int index)
        => index switch
        {
            HeaderContentLength => _body.Length.ToString(CultureInfo.InvariantCulture),
            HeaderContentType when _body.Length > 0 => "application/json; charset=utf-8",
            _ => null
        };

    public override string[][] GetUnknownRequestHeaders()
    {
        var headers = new List<string[]>(2);
        if (_apiKey is not null)
        {
            headers.Add(new[] { "X-OdfKit-WebFont-Key", _apiKey });
        }

        if (_ifNoneMatch is not null)
        {
            headers.Add(new[] { "If-None-Match", _ifNoneMatch });
        }

        if (_backend is not null)
        {
            headers.Add(new[] { "X-OdfKit-WebFont-Backend", _backend });
        }

        return headers.ToArray();
    }

    public override void SendStatus(int statusCode, string statusDescription)
        => StatusCode = statusCode;

    public override void SendKnownResponseHeader(int index, string value)
        => ResponseHeaders[GetKnownResponseHeaderName(index)] = value;

    public override void SendUnknownResponseHeader(string name, string value)
        => ResponseHeaders[name] = value;

    public override void SendResponseFromMemory(byte[] data, int length)
        => _responseBody.Write(data, 0, length);

    public override void SendResponseFromFile(string filename, long offset, long length)
    {
    }

    public override void SendResponseFromFile(IntPtr handle, long offset, long length)
    {
    }

    public override void FlushResponse(bool finalFlush)
    {
    }

    public override void EndOfRequest()
    {
    }
}

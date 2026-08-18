using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using OdfKit.WebFonts;
using OdfKit.WebFonts.AspNetCore.Sample;
using OdfKit.WebFonts.Hosting.AspNetCore;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Worker;

const string AuthorizationPolicy = "odf-webfont-generation";
const string RateLimiterPolicy = "odf-webfont-generation";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
IConfigurationSection settings = builder.Configuration.GetSection("OdfKit:WebFonts");
string assetRoot = ResolvePath(
    builder.Environment.ContentRootPath,
    settings["AssetRoot"] ?? "wwwroot/_odf-fonts");
string profileId = settings["ProfileId"] ?? "cns11643-euc-tw-2026-08-05";
string? publicBaseUrl = settings["PublicBaseUrl"];
string? apiKey = settings["ApiKey"];

// 固定窗口限流門檻可由設定／環境變數（OdfKit__WebFonts__RateLimitPermitLimit）覆寫，
// 讓 smoke 測試能以確定性的低門檻執行，而不必調降此範例的正式預設值。
// Fixed-window rate-limit threshold is overridable via configuration/environment
// (OdfKit__WebFonts__RateLimitPermitLimit) so smoke tests can run against a
// deterministic low threshold without lowering this sample's production default.
int rateLimitPermitLimit = settings.GetValue("RateLimitPermitLimit", 32);
if (rateLimitPermitLimit < 1)
{
    Console.Error.WriteLine("OdfKit:WebFonts:RateLimitPermitLimit must be a positive integer.");
    return;
}
if (string.IsNullOrWhiteSpace(apiKey))
{
    apiKey = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_API_KEY");
}

List<SampleFontSourceOptions> configuredSources =
    settings.GetSection("FontSources").Get<List<SampleFontSourceOptions>>() ?? [];
if (configuredSources.Count == 0)
{
    configuredSources.Add(new SampleFontSourceOptions
    {
        Id = settings["FontSourceId"] ?? "cns-sung-plus",
        Path = settings["FontPath"] ?? string.Empty,
        SourceSha256 = settings["SourceSha256"] ?? string.Empty,
        FaceIndex = settings.GetValue("FaceIndex", 0),
        FontFamily = settings["FontFamily"] ?? "OdfKit CNS Sung Plus"
    });
}

SampleFontSource[] fontSources = configuredSources.Select(source => new SampleFontSource(
    source.Id,
    ResolvePath(builder.Environment.ContentRootPath, source.Path),
    source.SourceSha256.ToLowerInvariant(),
    source.FaceIndex,
    source.FontFamily)).ToArray();
if (fontSources.Length is < 1 or > 16
    || fontSources.Select(source => source.Id).Distinct(StringComparer.Ordinal).Count()
        != fontSources.Length
    || fontSources.Any(source =>
        !File.Exists(source.Path)
        || source.Id.Length is < 1 or > 256
        || source.FaceIndex < 0
        || source.SourceSha256.Length != 64
        || source.SourceSha256.Any(character => !Uri.IsHexDigit(character))
        || string.IsNullOrWhiteSpace(source.FontFamily))
    || profileId.Length is < 1 or > 256
    || string.IsNullOrWhiteSpace(apiKey)
    || System.Text.Encoding.UTF8.GetByteCount(apiKey) < 32)
{
    Console.Error.WriteLine(
        "Configure a licensed font, its SHA-256, and a 32-byte OdfKit:WebFonts:ApiKey before starting the sample.");
    return;
}

foreach (SampleFontSource source in fontSources)
{
    string actualSourceSha256 = ComputeSha256(source.Path);
    if (!string.Equals(source.SourceSha256, actualSourceSha256, StringComparison.Ordinal))
    {
        Console.Error.WriteLine(
            $"The configured source font SHA-256 does not match the file for '{source.Id}'.");
        return;
    }
}

Dictionary<string, SampleFontSource> fontSourceMap = fontSources.ToDictionary(
    source => source.Id,
    StringComparer.Ordinal);
var sampleJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
sampleJsonOptions.Converters.Add(new JsonStringEnumConverter());
Directory.CreateDirectory(assetRoot);
builder.Services.AddOdfWebFonts(options =>
{
    options.AssetRootPath = assetRoot;
    options.PublicBaseUrl = publicBaseUrl;
    string? allowedOrigin = settings["AllowedOrigin"];
    if (!string.IsNullOrWhiteSpace(allowedOrigin))
    {
        options.AllowedOrigins.Add(allowedOrigin);
        options.CrossOriginResourcePolicy = OdfWebFontCrossOriginPolicy.CrossOrigin;
    }
});
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        options => options.ClaimsIssuer = apiKey);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicy, policy => policy.RequireAuthenticatedUser());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(
        RateLimiterPolicy,
        limiter =>
        {
            limiter.PermitLimit = rateLimitPermitLimit;
            limiter.QueueLimit = 0;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiter.Window = TimeSpan.FromMinutes(1);
        });
});

var engineOptions = new ManagedOpenTypeWebFontEngineOptions
{
    MaxOutputBytes = 32L * 1024 * 1024,
    MaxSourceBytes = 256L * 1024 * 1024,
    MaxUnicodeScalars = 4096
};
foreach (SampleFontSource source in fontSources)
{
    engineOptions.FontSources[source.Id] = source.Path;
}
builder.Services.AddOdfWebFontGeneration(
    _ => new ManagedOpenTypeWebFontSubsetEngine(engineOptions),
    options =>
    {
        options.AuthorizationPolicyName = AuthorizationPolicy;
        options.RateLimiterPolicyName = RateLimiterPolicy;
        foreach (SampleFontSource source in fontSources)
        {
            options.AllowedFaces.Add(new WebFontFaceIdentity
            {
                FontSourceId = source.Id,
                SourceSha256 = source.SourceSha256,
                FaceIndex = source.FaceIndex
            });
        }
        options.AllowedProfileIds.Add(profileId);
        options.AllowedFormats.Clear();
        options.AllowedFormats.Add(WebFontFormat.Woff2);
        options.AllowedFormats.Add(WebFontFormat.Woff);
        options.AllowedFormats.Add(WebFontFormat.TrueType);
    },
    options =>
    {
        options.DurableCacheDirectory = Path.Combine(assetRoot, ".worker-cache");
        options.QueueCapacity = 16;
        options.MaxConcurrency = 1;
        options.JobTimeout = TimeSpan.FromMinutes(3);
    });

WebApplication app = builder.Build();
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/_odf-fonts"),
    staticFilesApp => staticFilesApp.UseStaticFiles());
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.Equals("/_odf-fonts/generate", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    await next();
});
app.Use(async (context, next) =>
{
    OdfWebFontResourceProvider resources = context.RequestServices
        .GetRequiredService<OdfWebFontResourceProvider>();
    string source = resources.ContentSecurityPolicySource;
    string additionalSource = source == "'self'" ? string.Empty : $" {source}";
    context.Response.Headers.ContentSecurityPolicy =
        $"default-src 'none'; script-src 'self'; connect-src 'self'; "
        + $"style-src 'self'{additionalSource}; font-src 'self'{additionalSource}; "
        + "img-src 'none'; media-src 'none'; object-src 'none'; base-uri 'none'; "
        + "frame-ancestors 'none'; form-action 'none'; manifest-src 'none'; worker-src 'none'; "
        + "require-trusted-types-for 'script'";
    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOdfWebFonts();
app.MapPost(
    "/sample/generate",
    async Task<IResult> (
        SampleGenerationRequest request,
        WebFontGenerationWorker worker,
        CancellationToken cancellationToken) =>
    {
        if (!fontSourceMap.TryGetValue(request.FontSourceId, out SampleFontSource? source)
            || !Enum.TryParse(request.Format, ignoreCase: false, out WebFontFormat format)
            || format is not (WebFontFormat.Woff2 or WebFontFormat.Woff or WebFontFormat.TrueType)
            || request.Sequences.Count is < 1 or > 256
            || request.Sequences.Sum(sequence => (long)sequence.EnumerateRunes().Count()) > 4096)
        {
            return Results.BadRequest();
        }

        var face = new WebFontFaceIdentity
        {
            FontSourceId = source.Id,
            SourceSha256 = source.SourceSha256,
            FaceIndex = source.FaceIndex
        };
        IReadOnlyList<WebFontTextSequence> supportedSequences = await worker
            .FilterSupportedSequencesAsync(
                face,
                request.Sequences.Select(WebFontTextSequence.Create).ToArray(),
                cancellationToken);
        if (supportedSequences.Count == 0)
        {
            return Results.NoContent();
        }

        WebFontManifest manifest = await worker.GenerateAsync(
            new WebFontSubsetRequest
            {
                Face = face,
                ProfileId = profileId,
                FontFamily = source.FontFamily,
                Sequences = supportedSequences,
                Formats = [format],
                RequiredBrowserTargets = [WebFontBrowserTarget.Chromium]
            },
            assetRoot,
            cancellationToken);
        return manifest.Assets.Count == 0
            ? Results.NoContent()
            : Results.Json(manifest, sampleJsonOptions);
    })
    .RequireRateLimiting(RateLimiterPolicy);
app.MapGet(
    "/health",
    () => Results.Json(new
    {
        status = "ok",
        dynamicGeneration = true,
        fontSources = fontSources.Select(source => new
        {
            id = source.Id,
            fontFamily = source.FontFamily,
            faceIndex = source.FaceIndex
        }),
        profileId,
    }));
app.MapGet(
    "/",
    () => Results.Content(
        "<!doctype html><html lang=\"zh-Hant-TW\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>OdfKit ASP.NET Core 動態 WebFont</title><link rel=\"stylesheet\" href=\"/site.css\"></head><body data-international-ready=\"pending\"><main><h1>系統字型優先的全字庫 Plus 動態 WebFont</h1><label for=\"formatSelect\">切換格式</label><select id=\"formatSelect\"><option value=\"Woff2\" selected>WOFF2</option><option value=\"Woff\">WOFF</option><option value=\"TrueType\">TrueType／TTF</option></select><label for=\"fontSelect\">切換缺字字型</label><select id=\"fontSelect\"><option value=\"cns-sung-plus\" selected>全字庫宋體 Plus</option><option value=\"cns-kai-plus\">全字庫楷體 Plus</option></select><p>一般文字與系統已有的 Ext-B 維持系統字型；只有缺字才載入全字庫 Plus。範例另驗證 CNS 17-2174／U+FFAE0 與 800 個 PUA。</p><label for=\"rareInput\">造字與難字即時輸入框</label><textarea id=\"rareInput\" rows=\"7\">【指定 CNS 造字】U+FFAE0：&#xFFAE0;\n【系統字型覆蓋】一般文字 ABC 一二三；Ext-B：𠀀𠆩𪚥。\n【自由輸入】可在此貼入需要驗證的完整內容。</textarea><div id=\"previewBox\" class=\"preview font-cns-sung-plus\"></div><p id=\"status\">正在產生 WOFF2…</p></main><script src=\"/webfont-autosubset.js?v=5\"></script><script src=\"/webfont-sample.js?v=4\"></script></body></html>",
        "text/html; charset=utf-8"));

await app.RunAsync();

static string ResolvePath(string contentRoot, string configuredPath)
    => Path.GetFullPath(Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.Combine(contentRoot, configuredPath));

static string ComputeSha256(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

internal sealed class SampleFontSourceOptions
{
    public string Id { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string SourceSha256 { get; init; } = string.Empty;

    public int FaceIndex { get; init; }

    public string FontFamily { get; init; } = string.Empty;
}

internal sealed record SampleFontSource(
    string Id,
    string Path,
    string SourceSha256,
    int FaceIndex,
    string FontFamily);

internal sealed record SampleGenerationRequest(
    string FontSourceId,
    string Format,
    IReadOnlyList<string> Sequences);

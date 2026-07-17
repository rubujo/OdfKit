using System.Security.Cryptography;
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
IConfigurationSection settings = builder.Configuration.GetSection("OdfKit:WebFonts");
string assetRoot = ResolvePath(
    builder.Environment.ContentRootPath,
    settings["AssetRoot"] ?? "wwwroot/_odf-fonts");
string fontPath = ResolvePath(builder.Environment.ContentRootPath, settings["FontPath"] ?? string.Empty);
string fontSourceId = settings["FontSourceId"] ?? "cns-ext-b";
string sourceSha256 = (settings["SourceSha256"] ?? string.Empty).ToLowerInvariant();
string profileId = settings["ProfileId"] ?? "cns11643-euc-tw-2026-05-05";
int faceIndex = settings.GetValue("FaceIndex", 0);
string? publicBaseUrl = settings["PublicBaseUrl"];
string? apiKey = settings["ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    apiKey = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_API_KEY");
}

if (!File.Exists(fontPath)
    || fontSourceId.Length is < 1 or > 256
    || profileId.Length is < 1 or > 256
    || faceIndex < 0
    || sourceSha256.Length != 64
    || sourceSha256.Any(character => !Uri.IsHexDigit(character))
    || string.IsNullOrWhiteSpace(apiKey)
    || System.Text.Encoding.UTF8.GetByteCount(apiKey) < 32)
{
    Console.Error.WriteLine(
        "Configure a licensed font, its SHA-256, and a 32-byte OdfKit:WebFonts:ApiKey before starting the sample.");
    return;
}

string actualSourceSha256 = ComputeSha256(fontPath);
if (!string.Equals(sourceSha256, actualSourceSha256, StringComparison.Ordinal))
{
    Console.Error.WriteLine("The configured source font SHA-256 does not match the file.");
    return;
}

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
            limiter.PermitLimit = 10;
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
engineOptions.FontSources[fontSourceId] = fontPath;
builder.Services.AddOdfWebFontGeneration(
    _ => new ManagedOpenTypeWebFontSubsetEngine(engineOptions),
    options =>
    {
        options.AuthorizationPolicyName = AuthorizationPolicy;
        options.RateLimiterPolicyName = RateLimiterPolicy;
        options.AllowedFaces.Add(new WebFontFaceIdentity
        {
            FontSourceId = fontSourceId,
            SourceSha256 = sourceSha256,
            FaceIndex = faceIndex
        });
        options.AllowedProfileIds.Add(profileId);
        options.AllowedFormats.Clear();
        options.AllowedFormats.Add(WebFontFormat.Woff2);
    },
    options =>
    {
        options.DurableCacheDirectory = Path.Combine(assetRoot, ".worker-cache");
        options.QueueCapacity = 16;
        options.MaxConcurrency = 1;
        options.JobTimeout = TimeSpan.FromMinutes(3);
    });

WebApplication app = builder.Build();
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
        $"default-src 'self'; script-src 'self'; style-src 'self'{additionalSource}; font-src 'self'{additionalSource}; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOdfWebFonts();
app.MapGet(
    "/health",
    () => Results.Json(new
    {
        status = "ok",
        dynamicGeneration = true,
        fontSourceId,
        profileId,
        faceIndex
    }));
app.MapGet(
    "/",
    () => Results.Content(
        "<!doctype html><html lang=\"zh-Hant-TW\"><head><meta charset=\"utf-8\"><title>OdfKit WebFonts</title></head><body><h1>OdfKit WebFonts dynamic generation service</h1><p>Health: GET /health</p><p>Trusted backend: POST /_odf-fonts/generate</p><p>Browsers load only content-addressed assets from the returned manifest.</p></body></html>",
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

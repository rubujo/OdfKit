using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OdfKit.WebFonts;
using OdfKit.WebFonts.Hosting.AspNetCore;
using OdfKit.WebFonts.OpenType;

string? assetDirectory = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_SMOKE_ASSETS");
string? sourcePath = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_SMOKE_DYNAMIC_SOURCE");
string? sourceSha256 = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_SMOKE_DYNAMIC_SOURCE_SHA256");
string? apiKey = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_SMOKE_DYNAMIC_API_KEY");
if (string.IsNullOrWhiteSpace(assetDirectory)
    || string.IsNullOrWhiteSpace(sourcePath)
    || string.IsNullOrWhiteSpace(sourceSha256)
    || string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Managed WebFont smoke environment is incomplete.");
    return;
}

string manifestPath = Path.Combine(assetDirectory, "webfonts.json");
using JsonDocument manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
JsonElement[] assets = manifestDocument.RootElement.GetProperty("Assets").EnumerateArray().ToArray();
if (assets.Length < 3)
{
    Console.Error.WriteLine("The managed manifest does not contain the expected formats.");
    return;
}

string family = assets[0].GetProperty("FontFamily").GetString() ?? "OdfKit Product Smoke";
const string sampleText = "A𠆩";
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddOdfWebFonts(options => options.AssetRootPath = assetDirectory);
builder.Services.AddAuthentication(SmokeAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SmokeAuthenticationHandler>(
        SmokeAuthenticationHandler.SchemeName,
        options => options.ClaimsIssuer = apiKey);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("webfont-smoke-generation", policy => policy.RequireAuthenticatedUser());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(
        "webfont-smoke-generation",
        limiter =>
        {
            limiter.PermitLimit = 5;
            limiter.QueueLimit = 0;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiter.Window = TimeSpan.FromMinutes(1);
        });
});

var engineOptions = new ManagedOpenTypeWebFontEngineOptions
{
    MaxOutputBytes = 32L * 1024 * 1024,
    MaxSourceBytes = 256L * 1024 * 1024,
    MaxUnicodeScalars = 1024
};
engineOptions.FontSources["dynamic-smoke"] = sourcePath;
builder.Services.AddOdfWebFontGeneration(
    _ => new ManagedOpenTypeWebFontSubsetEngine(engineOptions),
    options =>
    {
        options.AuthorizationPolicyName = "webfont-smoke-generation";
        options.RateLimiterPolicyName = "webfont-smoke-generation";
        options.AllowedFaces.Add(new WebFontFaceIdentity
        {
            FontSourceId = "dynamic-smoke",
            SourceSha256 = sourceSha256
        });
        options.AllowedProfileIds.Add("dynamic-smoke-v1");
    },
    options =>
    {
        options.DurableCacheDirectory = Path.Combine(assetDirectory, "dynamic-cache");
        options.QueueCapacity = 2;
        options.MaxConcurrency = 1;
        options.JobTimeout = TimeSpan.FromSeconds(25);
    });

WebApplication app = builder.Build();
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
        assetCount = assets.Length,
        formats = assets.Select(asset => asset.GetProperty("Format").GetString()).ToArray(),
        sampleText
    }));
app.MapGet(
    "/",
    () => Results.Content(CreatePage(family, sampleText), "text/html; charset=utf-8"));
await app.RunAsync();

static string CreatePage(string family, string text)
{
    string cssFamily = family.Replace("\"", "\\\"", StringComparison.Ordinal);
    return $$"""
        <!doctype html>
        <html lang="zh-Hant-TW">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>OdfKit managed WebFont 真實驗證</title>
          <link rel="stylesheet" href="/_odf-fonts/webfonts.css">
          <style>
            :root { color-scheme: dark; font-family: system-ui, sans-serif; }
            body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #08131d; color: #edf7ff; }
            main { width: min(820px, calc(100vw - 48px)); padding: 38px; border: 1px solid #37617b; border-radius: 24px; background: #0c1c28; }
            .glyphs { margin: 24px 0; padding: 24px; border-radius: 14px; background: #f7f4ed; color: #17232b; font: 56px/1.4 "{{cssFamily}}", sans-serif; }
            .pass { color: #7fffc2; }
          </style>
        </head>
        <body data-international-ready="pending">
          <main>
            <h1>純 .NET WebFont 真實驗證</h1>
            <p>managed TTF／WOFF／WOFF2 writer → ASP.NET Core → 瀏覽器 FontFaceSet</p>
            <div class="glyphs" id="glyphs">{{text}}</div>
            <p id="status">正在載入…</p>
          </main>
          <script>
            (async () => {
              const proof = { loadedCases: [], widths: {} };
              window.__odfKitInternationalProof = proof;
              try {
                await document.fonts.load('56px "{{cssFamily}}"', '{{text}}');
                await document.fonts.ready;
                if (!document.fonts.check('56px "{{cssFamily}}"', '{{text}}')) throw new Error('FontFaceSet check failed.');
                proof.widths.managed = document.getElementById('glyphs').getBoundingClientRect().width;
                proof.loadedCases.push('managed-cns');
                document.getElementById('status').textContent = 'PASS：真實 managed WebFont 已載入';
                document.getElementById('status').className = 'pass';
                document.body.dataset.internationalReady = 'true';
              } catch (error) {
                document.getElementById('status').textContent = String(error);
                document.body.dataset.internationalReady = 'false';
                console.error(error);
              }
            })();
          </script>
        </body>
        </html>
        """;
}

internal sealed class SmokeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal const string SchemeName = "OdfKitWebFontSmoke";

    public SmokeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? expected = Options.ClaimsIssuer;
        string? supplied = Request.Headers["X-OdfKit-WebFont-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected)
            || !string.Equals(expected, supplied, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "webfont-smoke")],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

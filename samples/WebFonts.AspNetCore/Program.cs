using OdfKit.WebFonts.Hosting.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string assetRoot = builder.Configuration["OdfKit:WebFonts:AssetRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "_odf-fonts");
string? publicBaseUrl = builder.Configuration["OdfKit:WebFonts:PublicBaseUrl"];

if (!Directory.Exists(assetRoot))
{
    Console.Error.WriteLine($"Generate WebFont assets before starting the sample: {assetRoot}");
    return;
}

builder.Services.AddOdfWebFonts(options =>
{
    options.AssetRootPath = assetRoot;
    options.PublicBaseUrl = publicBaseUrl;
    string? allowedOrigin = builder.Configuration["OdfKit:WebFonts:AllowedOrigin"];
    if (!string.IsNullOrWhiteSpace(allowedOrigin))
    {
        options.AllowedOrigins.Add(allowedOrigin);
        options.CrossOriginResourcePolicy = OdfWebFontCrossOriginPolicy.CrossOrigin;
    }
});

WebApplication app = builder.Build();
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

app.MapOdfWebFonts();
app.MapGet(
    "/",
    (OdfWebFontResourceProvider resources) => Results.Content(
        $"<!doctype html><html lang=\"zh-Hant-TW\"><head><meta charset=\"utf-8\">{resources.CreateStylesheetLink()}<title>OdfKit WebFonts</title></head><body><h1>多國罕用字</h1><p>邉󠄐 𠀀 󰀀 العربية हिन्दी</p></body></html>",
        "text/html; charset=utf-8"));

await app.RunAsync();

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using OdfKit.WebFonts.Hosting.AspNetCore;

namespace OdfKit.WebFonts.Tests;

public sealed class WebFontHostingTests
{
    [Fact]
    public async Task Endpoints_ServeOnlyManifestAddressedImmutableAsset()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-secure-smoke"u8.ToArray();
        string fileName = "smoke.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256);

        await using WebApplication application = await StartApplicationAsync(rootPath);
        using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };

        using HttpResponseMessage manifestResponse = await client.GetAsync(
            "/_odf-fonts/manifest.json",
            TestContext.Current.CancellationToken);
        using HttpResponseMessage assetResponse = await client.GetAsync(
            $"/_odf-fonts/{sha256}/{fileName}",
            TestContext.Current.CancellationToken);
        using HttpResponseMessage unknownResponse = await client.GetAsync(
            $"/_odf-fonts/{new string('0', 64)}/{fileName}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);
        Assert.Equal("no-cache", manifestResponse.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
        Assert.Equal("font/woff2", assetResponse.Content.Headers.ContentType?.MediaType);
        System.Net.Http.Headers.CacheControlHeaderValue cacheControl = Assert.IsType<System.Net.Http.Headers.CacheControlHeaderValue>(
            assetResponse.Headers.CacheControl);
        Assert.True(cacheControl.Public);
        Assert.Equal(TimeSpan.FromDays(365), cacheControl.MaxAge);
        Assert.Contains(cacheControl.Extensions, item => item.Name == "immutable");
        Assert.Equal("nosniff", assetResponse.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal($"\"{sha256}\"", assetResponse.Headers.ETag?.Tag);
        Assert.Equal(
            fontBytes,
            await assetResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);

        await application.StopAsync(TestContext.Current.CancellationToken);
        DeleteTemporaryRoot(rootPath);
    }

    [Fact]
    public async Task Endpoints_EmitOnlyAllowlistedCorsOrigin()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-cors-smoke"u8.ToArray();
        string fileName = "cors.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256);

        await using WebApplication application = await StartApplicationAsync(
            rootPath,
            options =>
            {
                options.AllowedOrigins.Add("https://app.example.com");
                options.CrossOriginResourcePolicy = OdfWebFontCrossOriginPolicy.CrossOrigin;
            });
        using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };

        using var allowedRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/_odf-fonts/{sha256}/{fileName}");
        allowedRequest.Headers.Add("Origin", "https://app.example.com");
        using HttpResponseMessage allowedResponse = await client.SendAsync(
            allowedRequest,
            TestContext.Current.CancellationToken);
        using var deniedRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/_odf-fonts/{sha256}/{fileName}");
        deniedRequest.Headers.Add("Origin", "https://attacker.example");
        using HttpResponseMessage deniedResponse = await client.SendAsync(
            deniedRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal("https://app.example.com", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("cross-origin", allowedResponse.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        Assert.Contains("Origin", allowedResponse.Headers.Vary);
        Assert.DoesNotContain("Access-Control-Allow-Origin", deniedResponse.Headers.Select(header => header.Key));

        await application.StopAsync(TestContext.Current.CancellationToken);
        DeleteTemporaryRoot(rootPath);
    }

    [Fact]
    public async Task ResourceProvider_UsesCdnWithoutInlineContent()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-resource-smoke"u8.ToArray();
        string fileName = "resource.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256);

        await using WebApplication application = await StartApplicationAsync(
            rootPath,
            options => options.PublicBaseUrl = "https://fonts.example.com/assets/");
        OdfWebFontResourceProvider provider = application.Services
            .GetRequiredService<OdfWebFontResourceProvider>();

        Assert.Equal("https://fonts.example.com/assets/webfonts.css", provider.StylesheetUrl);
        Assert.Equal("https://fonts.example.com", provider.ContentSecurityPolicySource);
        Assert.Equal(
            "<link rel=\"stylesheet\" href=\"https://fonts.example.com/assets/webfonts.css\" />",
            provider.CreateStylesheetLink());

        await application.StopAsync(TestContext.Current.CancellationToken);
        DeleteTemporaryRoot(rootPath);
    }

    [Fact]
    public async Task ResourceProvider_UsesImmutableFingerprintedStylesheet()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-stylesheet-smoke"u8.ToArray();
        string fileName = "stylesheet.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        const string css = "@font-face { font-family: 'Smoke'; }\n";
        string cssSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(css)))
            .ToLowerInvariant();
        string cssFileName = $"webfonts.{cssSha256[..16]}.css";
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256, css);

        await using WebApplication application = await StartApplicationAsync(rootPath);
        OdfWebFontResourceProvider provider = application.Services
            .GetRequiredService<OdfWebFontResourceProvider>();
        using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
        using HttpResponseMessage response = await client.GetAsync(
            provider.StylesheetUrl,
            TestContext.Current.CancellationToken);

        Assert.Equal($"/_odf-fonts/{cssFileName}", provider.StylesheetUrl);
        Assert.Equal("'self'", provider.ContentSecurityPolicySource);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        System.Net.Http.Headers.CacheControlHeaderValue cacheControl = Assert.IsType<System.Net.Http.Headers.CacheControlHeaderValue>(
            response.Headers.CacheControl);
        Assert.Contains(cacheControl.Extensions, value => value.Name == "immutable");
        Assert.Equal($"\"{cssSha256}\"", response.Headers.ETag?.Tag);

        await application.StopAsync(TestContext.Current.CancellationToken);
        DeleteTemporaryRoot(rootPath);
    }

    [Fact]
    public async Task Endpoints_ServeConcurrentImmutableRequests()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-concurrent-smoke"u8.ToArray();
        const string fileName = "concurrent.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256);

        await using WebApplication application = await StartApplicationAsync(rootPath);
        using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
        Task<HttpResponseMessage>[] requests = Enumerable.Range(0, 256)
            .Select(_ => client.GetAsync(
                $"/_odf-fonts/{sha256}/{fileName}",
                TestContext.Current.CancellationToken))
            .ToArray();
        HttpResponseMessage[] responses = await Task.WhenAll(requests)
            .WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.All(responses, response =>
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal($"\"{sha256}\"", response.Headers.ETag?.Tag);
            });
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        await application.StopAsync(TestContext.Current.CancellationToken);
        DeleteTemporaryRoot(rootPath);
    }

    [Fact]
    public async Task Startup_RejectsAssetWhoseContentDoesNotMatchManifestHash()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-original"u8.ToArray();
        const string fileName = "tampered.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256);
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, sha256, fileName),
            "wOF2-tampered"u8.ToArray(),
            TestContext.Current.CancellationToken);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => StartApplicationAsync(rootPath));
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    private static Task<WebApplication> StartApplicationAsync(string rootPath)
        => StartApplicationAsync(rootPath, _ => { });

    private static async Task<WebApplication> StartApplicationAsync(
        string rootPath,
        Action<OdfWebFontOptions> configure)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddOdfWebFonts(options =>
        {
            options.AssetRootPath = rootPath;
            configure(options);
        });
        WebApplication application = builder.Build();
        application.MapOdfWebFonts();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
    }

    private static string GetAddress(WebApplication application)
    {
        IServer server = application.Services.GetRequiredService<IServer>();
        return server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    private static async Task WriteManifestAsync(
        string rootPath,
        string fileName,
        long byteLength,
        string sha256,
        string? css = null)
    {
        string hashDirectory = Path.Combine(rootPath, sha256);
        Directory.CreateDirectory(hashDirectory);
        File.Move(
            Path.Combine(rootPath, fileName),
            Path.Combine(hashDirectory, fileName));
        string? cssSha256 = css is null
            ? null
            : Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(css))).ToLowerInvariant();
        string? cssFileName = cssSha256 is null ? null : $"webfonts.{cssSha256[..16]}.css";
        if (cssFileName is not null)
        {
            await File.WriteAllTextAsync(
                Path.Combine(rootPath, cssFileName),
                css,
                TestContext.Current.CancellationToken);
        }

        var manifest = new WebFontManifest
        {
            ProfileId = "test-v1",
            StylesheetFileName = cssFileName,
            StylesheetSha256 = cssSha256,
            Assets =
            [
                new WebFontAsset
                {
                    FileName = fileName,
                    Sha256 = sha256,
                    ByteLength = byteLength,
                    Format = WebFontFormat.Woff2,
                    FontFamily = "OdfKit Test",
                    UnicodeRanges = ["U+9089", "U+E0110"]
                }
            ]
        };
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        await File.WriteAllTextAsync(
            Path.Combine(rootPath, "webfonts.json"),
            JsonSerializer.Serialize(manifest, serializerOptions),
            TestContext.Current.CancellationToken);
    }

    private static string CreateTemporaryRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"OdfKit.WebFonts.{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return Path.GetFullPath(rootPath);
    }

    private static void DeleteTemporaryRoot(string rootPath)
    {
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string resolvedRoot = Path.GetFullPath(rootPath);
        if (!resolvedRoot.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolvedRoot).StartsWith("OdfKit.WebFonts.", StringComparison.Ordinal))
        {
            return;
        }

        Directory.Delete(resolvedRoot, recursive: true);
    }
}

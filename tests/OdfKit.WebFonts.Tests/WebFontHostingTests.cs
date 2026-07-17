using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        using var manifestValidationRequest = new HttpRequestMessage(HttpMethod.Get, "/_odf-fonts/manifest.json");
        manifestValidationRequest.Headers.IfNoneMatch.Add(manifestResponse.Headers.ETag!);
        using HttpResponseMessage manifestValidationResponse = await client.SendAsync(
            manifestValidationRequest,
            TestContext.Current.CancellationToken);
        using var manifestHeadRequest = new HttpRequestMessage(HttpMethod.Head, "/_odf-fonts/manifest.json");
        using HttpResponseMessage manifestHeadResponse = await client.SendAsync(
            manifestHeadRequest,
            TestContext.Current.CancellationToken);
        using var assetValidationRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/_odf-fonts/{sha256}/{fileName}");
        assetValidationRequest.Headers.IfNoneMatch.Add(assetResponse.Headers.ETag!);
        using HttpResponseMessage assetValidationResponse = await client.SendAsync(
            assetValidationRequest,
            TestContext.Current.CancellationToken);
        using var assetHeadRequest = new HttpRequestMessage(
            HttpMethod.Head,
            $"/_odf-fonts/{sha256}/{fileName}");
        using HttpResponseMessage assetHeadResponse = await client.SendAsync(
            assetHeadRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);
        Assert.Equal("no-cache", manifestResponse.Headers.CacheControl?.ToString());
        Assert.NotNull(manifestResponse.Headers.ETag);
        Assert.Equal(HttpStatusCode.NotModified, manifestValidationResponse.StatusCode);
        Assert.Empty(await manifestValidationResponse.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, manifestHeadResponse.StatusCode);
        Assert.Equal(manifestResponse.Headers.ETag, manifestHeadResponse.Headers.ETag);
        Assert.Empty(await manifestHeadResponse.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken));
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
        Assert.Equal(HttpStatusCode.NotModified, assetValidationResponse.StatusCode);
        Assert.Empty(await assetValidationResponse.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, assetHeadResponse.StatusCode);
        Assert.Empty(await assetHeadResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
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
        Assert.Equal(
            $"<link rel=\"preload\" href=\"https://fonts.example.com/assets/{sha256}/{fileName}\" as=\"font\" type=\"font/woff2\" crossorigin=\"anonymous\" />",
            provider.CreateFontPreloadLink(
                new WebFontAsset
                {
                    FileName = fileName,
                    Sha256 = sha256,
                    ByteLength = fontBytes.Length,
                    Format = WebFontFormat.Woff2,
                    FontFamily = "OdfKit Test",
                    UnicodeRanges = ["U+9089", "U+E0110"]
                }));

        await application.StopAsync(TestContext.Current.CancellationToken);
        DeleteTemporaryRoot(rootPath);
    }

    [Fact]
    public async Task Endpoints_RevalidateLegacyStylesheetWithoutChangingBytes()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-legacy-css-smoke"u8.ToArray();
        const string fileName = "legacy-css.woff2";
        byte[] cssBytes = System.Text.Encoding.UTF8.GetBytes(
            "\uFEFF@font-face { font-family: 'Legacy'; }\n");
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, "webfonts.css"),
            cssBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256);

        await using WebApplication application = await StartApplicationAsync(rootPath);
        using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
        using HttpResponseMessage response = await client.GetAsync(
            "/_odf-fonts/webfonts.css",
            TestContext.Current.CancellationToken);
        using var validationRequest = new HttpRequestMessage(HttpMethod.Get, "/_odf-fonts/webfonts.css");
        validationRequest.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        using HttpResponseMessage validationResponse = await client.SendAsync(
            validationRequest,
            TestContext.Current.CancellationToken);
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, "/_odf-fonts/webfonts.css");
        using HttpResponseMessage headResponse = await client.SendAsync(
            headRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal(
            cssBytes,
            await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotModified, validationResponse.StatusCode);
        Assert.Empty(await validationResponse.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, headResponse.StatusCode);
        Assert.Equal(response.Headers.ETag, headResponse.Headers.ETag);
        Assert.Empty(await headResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

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
        const string css = "\uFEFF@font-face { font-family: 'Smoke'; }\n";
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
        using var validationRequest = new HttpRequestMessage(HttpMethod.Get, provider.StylesheetUrl);
        validationRequest.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        using HttpResponseMessage validationResponse = await client.SendAsync(
            validationRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal($"/_odf-fonts/{cssFileName}", provider.StylesheetUrl);
        Assert.Equal("'self'", provider.ContentSecurityPolicySource);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        System.Net.Http.Headers.CacheControlHeaderValue cacheControl = Assert.IsType<System.Net.Http.Headers.CacheControlHeaderValue>(
            response.Headers.CacheControl);
        Assert.Contains(cacheControl.Extensions, value => value.Name == "immutable");
        Assert.Equal($"\"{cssSha256}\"", response.Headers.ETag?.Tag);
        Assert.Equal(
            System.Text.Encoding.UTF8.GetBytes(css),
            await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotModified, validationResponse.StatusCode);
        Assert.Empty(await validationResponse.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken));

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
    public async Task GenerationEndpoint_RequiresAuthorizationAllowlistAndPublishesImmutableAsset()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            var engine = new DynamicAssetEngine();
            await using WebApplication application = await StartGenerationApplicationAsync(
                rootPath,
                engine,
                permitLimit: 10);
            using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
            OdfWebFontGenerationRequest request = CreateGenerationRequest();

            using HttpResponseMessage unauthorized = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                request,
                TestContext.Current.CancellationToken);
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");
            using HttpResponseMessage disallowed = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                new OdfWebFontGenerationRequest
                {
                    FontSourceId = request.FontSourceId,
                    FaceIndex = request.FaceIndex,
                    ProfileId = "untrusted-profile",
                    FontFamily = request.FontFamily,
                    Sequences = request.Sequences,
                    Formats = request.Formats
                },
                TestContext.Current.CancellationToken);
            using HttpResponseMessage generated = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, disallowed.StatusCode);
            Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
            Assert.True(disallowed.Headers.CacheControl is { NoStore: true, NoCache: true });
            Assert.True(generated.Headers.CacheControl is { NoStore: true, NoCache: true });
            Assert.Contains(
                generated.Headers.Pragma,
                value => string.Equals(value.Name, "no-cache", StringComparison.OrdinalIgnoreCase));
            WebFontManifest manifest = await generated.Content.ReadFromJsonAsync<WebFontManifest>(
                cancellationToken: TestContext.Current.CancellationToken)
                ?? throw new InvalidDataException();
            WebFontAsset asset = Assert.Single(manifest.Assets);
            using HttpResponseMessage assetResponse = await client.GetAsync(
                $"/_odf-fonts/{asset.Sha256}/{asset.FileName}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
            Assert.Equal("public, max-age=31536000, immutable", assetResponse.Headers.CacheControl?.ToString());
            await File.WriteAllBytesAsync(
                Path.Combine(rootPath, asset.Sha256, asset.FileName),
                "tampered"u8.ToArray(),
                TestContext.Current.CancellationToken);
            using HttpResponseMessage tamperedResponse = await client.GetAsync(
                $"/_odf-fonts/{asset.Sha256}/{asset.FileName}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, tamperedResponse.StatusCode);
            Assert.Equal(1, engine.CallCount);

            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task GenerationEndpoint_ColdStartsWithoutPrebuiltManifest()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            var engine = new DynamicAssetEngine();
            await using WebApplication application = await StartGenerationApplicationAsync(
                rootPath,
                engine,
                permitLimit: 10,
                seedInitialManifest: false);
            using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };

            using HttpResponseMessage initialManifest = await client.GetAsync(
                "/_odf-fonts/manifest.json",
                TestContext.Current.CancellationToken);
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");
            using HttpResponseMessage generated = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                CreateGenerationRequest(),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, initialManifest.StatusCode);
            WebFontManifest emptyManifest = await initialManifest.Content.ReadFromJsonAsync<WebFontManifest>(
                cancellationToken: TestContext.Current.CancellationToken)
                ?? throw new InvalidDataException();
            Assert.Empty(emptyManifest.Assets);
            Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
            WebFontManifest manifest = await generated.Content.ReadFromJsonAsync<WebFontManifest>(
                cancellationToken: TestContext.Current.CancellationToken)
                ?? throw new InvalidDataException();
            WebFontAsset asset = Assert.Single(manifest.Assets);
            using HttpResponseMessage assetResponse = await client.GetAsync(
                $"/_odf-fonts/{asset.Sha256}/{asset.FileName}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
            Assert.Equal(1, engine.CallCount);

            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task StaticHosting_RejectsMissingManifest()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => StartApplicationAsync(rootPath));
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task GenerationEndpoint_EnforcesNamedRateLimiter()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            var engine = new DynamicAssetEngine();
            await using WebApplication application = await StartGenerationApplicationAsync(
                rootPath,
                engine,
                permitLimit: 1);
            using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");

            using HttpResponseMessage first = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                CreateGenerationRequest(),
                TestContext.Current.CancellationToken);
            using HttpResponseMessage rejected = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                CreateGenerationRequest(),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal(1, engine.CallCount);

            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task GenerationEndpoint_RejectsOversizedChunkedJsonBodyBeforeEngine()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            var engine = new DynamicAssetEngine();
            await using WebApplication application = await StartGenerationApplicationAsync(
                rootPath,
                engine,
                permitLimit: 10,
                maxRequestBodyBytes: 128);
            using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");
            using var content = new StreamContent(new MemoryStream(new byte[1024]));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            content.Headers.ContentLength = null;

            using HttpResponseMessage response = await client.PostAsync(
                "/_odf-fonts/generate",
                content,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            Assert.True(response.Headers.CacheControl is { NoStore: true, NoCache: true });
            Assert.Equal(0, engine.CallCount);

            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
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

    [Fact]
    public async Task Startup_RejectsInvalidUtf8Stylesheet()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-invalid-css"u8.ToArray();
        const string fileName = "invalid-css.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, "webfonts.css"),
            [0xC3, 0x28],
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(rootPath, fileName, fontBytes.Length, sha256);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => StartApplicationAsync(rootPath));
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    private static OdfWebFontGenerationRequest CreateGenerationRequest()
        => new()
        {
            FontSourceId = "trusted-dynamic-face",
            ProfileId = "dynamic-test-v1",
            FontFamily = "OdfKit Dynamic Test",
            Sequences = ["A𠆩"],
            Formats = [WebFontFormat.Woff2]
        };

    private static async Task<WebApplication> StartGenerationApplicationAsync(
        string rootPath,
        DynamicAssetEngine engine,
        int permitLimit,
        int maxRequestBodyBytes = 64 * 1024,
        bool seedInitialManifest = true)
    {
        if (seedInitialManifest)
        {
            byte[] initialBytes = "wOF2-initial"u8.ToArray();
            const string initialFileName = "initial.woff2";
            string initialSha256 = Convert.ToHexString(SHA256.HashData(initialBytes)).ToLowerInvariant();
            await File.WriteAllBytesAsync(
                Path.Combine(rootPath, initialFileName),
                initialBytes,
                TestContext.Current.CancellationToken);
            await WriteManifestAsync(
                rootPath,
                initialFileName,
                initialBytes.Length,
                initialSha256);
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("webfont-generation", policy => policy.RequireAuthenticatedUser());
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;
            options.AddFixedWindowLimiter(
                "webfont-generation",
                limiterOptions =>
                {
                    limiterOptions.PermitLimit = permitLimit;
                    limiterOptions.QueueLimit = 0;
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                });
        });
        builder.Services.AddOdfWebFonts(rootPath);
        builder.Services.AddOdfWebFontGeneration(
            _ => engine,
            options =>
            {
                options.AuthorizationPolicyName = "webfont-generation";
                options.RateLimiterPolicyName = "webfont-generation";
                options.MaxRequestBodyBytes = maxRequestBodyBytes;
                options.AllowedFaces.Add(new WebFontFaceIdentity
                {
                    FontSourceId = "trusted-dynamic-face",
                    SourceSha256 = new string('a', 64)
                });
                options.AllowedProfileIds.Add("dynamic-test-v1");
            },
            options =>
            {
                options.MaxConcurrency = 1;
                options.QueueCapacity = 2;
                options.JobTimeout = TimeSpan.FromSeconds(10);
            });
        WebApplication application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.UseRateLimiter();
        application.MapOdfWebFonts();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
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

    private sealed class DynamicAssetEngine : IWebFontSubsetEngine
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            byte[] bytes = "wOF2-real-http-generation"u8.ToArray();
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            const string fileName = "dynamic.woff2";
            string hashDirectory = Path.Combine(destinationDirectory, sha256);
            Directory.CreateDirectory(hashDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(hashDirectory, fileName),
                bytes,
                cancellationToken);
            return new WebFontManifest
            {
                ProfileId = request.ProfileId,
                Assets =
                [
                    new WebFontAsset
                    {
                        FileName = fileName,
                        Sha256 = sha256,
                        ByteLength = bytes.Length,
                        Format = WebFontFormat.Woff2,
                        FontFamily = request.FontFamily,
                        UnicodeRanges = ["U+41", "U+201A9"]
                    }
                ]
            };
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "WebFontTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "webfont-test")],
                SchemeName);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
        }
    }
}

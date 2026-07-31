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
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task EndpointsServeOnlyManifestAddressedImmutableAsset()
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
    public async Task EndpointsEmitOnlyAllowlistedCorsOrigin()
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
    public async Task ResourceProviderUsesCdnWithoutInlineContent()
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
    public async Task EndpointsRevalidateLegacyStylesheetWithoutChangingBytes()
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
    public async Task ResourceProviderUsesImmutableFingerprintedStylesheet()
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
    public async Task EndpointsServeConcurrentImmutableRequests()
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
    public async Task GenerationEndpointRequiresAuthorizationAllowlistAndPublishesImmutableAsset()
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
    public async Task GenerationEndpointColdStartsWithoutPrebuiltManifest()
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
    public async Task StaticHostingRejectsMissingManifest()
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
    public async Task GenerationEndpointEnforcesNamedRateLimiter()
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
    public async Task GenerationEndpointReportsFullWorkerQueueAsTooManyRequests()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            var engine = new BlockingHostingEngine();
            await using WebApplication application = await StartGenerationApplicationAsync(
                rootPath,
                engine,
                permitLimit: 10,
                queueCapacity: 1);
            using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");

            Task<HttpResponseMessage> running = client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                CreateGenerationRequest("𠆩"),
                TestContext.Current.CancellationToken);
            await engine.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            Task<HttpResponseMessage> second = client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                CreateGenerationRequest("𡘙"),
                TestContext.Current.CancellationToken);
            Task<HttpResponseMessage> third = client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                CreateGenerationRequest("𡌂"),
                TestContext.Current.CancellationToken);
            Task<HttpResponseMessage> rejectedTask = await Task.WhenAny(second, third)
                .WaitAsync(TestContext.Current.CancellationToken);
            using HttpResponseMessage rejected = await rejectedTask.WaitAsync(
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(1), rejected.Headers.RetryAfter?.Delta);

            engine.Release.TrySetResult();
            using HttpResponseMessage runningResponse = await running.WaitAsync(
                TestContext.Current.CancellationToken);
            Task<HttpResponseMessage> queuedTask = ReferenceEquals(rejectedTask, second) ? third : second;
            using HttpResponseMessage queuedResponse = await queuedTask.WaitAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, runningResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, queuedResponse.StatusCode);

            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task GenerationEndpointFiltersMixedTextAndRecoversAfterNoSupportedGlyphs()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            var engine = new CoverageAwareDynamicAssetEngine();
            await using WebApplication application = await StartGenerationApplicationAsync(
                rootPath,
                engine,
                permitLimit: 10);
            using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");
            OdfWebFontGenerationRequest template = CreateGenerationRequest();

            using HttpResponseMessage normalOnly = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                new OdfWebFontGenerationRequest
                {
                    FontSourceId = template.FontSourceId,
                    FaceIndex = template.FaceIndex,
                    ProfileId = template.ProfileId,
                    FontFamily = template.FontFamily,
                    Sequences = ["一二三丨ㄩ幹"],
                    Formats = template.Formats
                },
                TestContext.Current.CancellationToken);
            using HttpResponseMessage mixed = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                new OdfWebFontGenerationRequest
                {
                    FontSourceId = template.FontSourceId,
                    FaceIndex = template.FaceIndex,
                    ProfileId = template.ProfileId,
                    FontFamily = template.FontFamily,
                    Sequences = ["𪚥 𩙡 𦚡 𨏿 𠆩 𡘙 𡌂 𠀀一二三丨ㄩ幹"],
                    Formats = template.Formats
                },
                TestContext.Current.CancellationToken);
            string largeMixed = string.Concat(
                Enumerable.Range(0, 4080).Select(index => char.ConvertFromUtf32(0x20000 + index)))
                + "一二三丨ㄩ幹";
            using HttpResponseMessage large = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                new OdfWebFontGenerationRequest
                {
                    FontSourceId = template.FontSourceId,
                    FaceIndex = template.FaceIndex,
                    ProfileId = template.ProfileId,
                    FontFamily = template.FontFamily,
                    Sequences = [largeMixed],
                    Formats = template.Formats
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, normalOnly.StatusCode);
            Assert.True(normalOnly.Headers.CacheControl is { NoStore: true, NoCache: true });
            Assert.Equal(HttpStatusCode.OK, mixed.StatusCode);
            Assert.Equal(HttpStatusCode.OK, large.StatusCode);
            Assert.Equal(2, engine.CallCount);
            Assert.Equal("𪚥𩙡𦚡𨏿𠆩𡘙𡌂𠀀", engine.GeneratedSequences[0]);
            Assert.Equal(4080, engine.GeneratedSequences[1].EnumerateRunes().Count());

            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task AssetEndpointDiscoversSharedGeneratedAssetAcrossApplicationInstances()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            WebFontAsset generatedAsset;
            var firstEngine = new DynamicAssetEngine();
            await using (WebApplication first = await StartGenerationApplicationAsync(
                             rootPath,
                             firstEngine,
                             permitLimit: 10,
                             seedInitialManifest: false))
            {
                using var client = new HttpClient { BaseAddress = new Uri(GetAddress(first)) };
                client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");
                using HttpResponseMessage generated = await client.PostAsJsonAsync(
                    "/_odf-fonts/generate",
                    CreateGenerationRequest(),
                    TestContext.Current.CancellationToken);
                WebFontManifest manifest = await generated.Content.ReadFromJsonAsync<WebFontManifest>(
                    cancellationToken: TestContext.Current.CancellationToken)
                    ?? throw new InvalidDataException();
                generatedAsset = Assert.Single(manifest.Assets);
                await first.StopAsync(TestContext.Current.CancellationToken);
            }

            var secondEngine = new DynamicAssetEngine();
            await using WebApplication second = await StartGenerationApplicationAsync(
                rootPath,
                secondEngine,
                permitLimit: 10,
                seedInitialManifest: false);
            using var secondClient = new HttpClient { BaseAddress = new Uri(GetAddress(second)) };
            using HttpResponseMessage assetResponse = await secondClient.GetAsync(
                $"/_odf-fonts/{generatedAsset.Sha256}/{generatedAsset.FileName}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
            Assert.Equal(0, secondEngine.CallCount);
            await second.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task GenerationEndpointClassifiesClientIntegrityAndTransientFailures()
    {
        string rootPath = CreateTemporaryRoot();
        try
        {
            var engine = new ClassifiedFailureEngine();
            await using WebApplication application = await StartGenerationApplicationAsync(
                rootPath,
                engine,
                permitLimit: 10);
            using var client = new HttpClient { BaseAddress = new Uri(GetAddress(application)) };
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "allowed");

            await AssertStatusAsync(client, "argument", HttpStatusCode.InternalServerError);
            await AssertStatusAsync(client, "unsupported", HttpStatusCode.UnprocessableEntity);
            await AssertStatusAsync(client, "invalid-data", HttpStatusCode.InternalServerError);
            await AssertStatusAsync(client, "io", HttpStatusCode.ServiceUnavailable);
            await AssertStatusAsync(client, "invalid-operation", HttpStatusCode.InternalServerError);
            await AssertStatusAsync(client, "timeout", HttpStatusCode.ServiceUnavailable);
            await AssertStatusAsync(client, "unexpected", HttpStatusCode.InternalServerError);

            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    [Fact]
    public async Task GenerationEndpointRejectsOversizedChunkedJsonBodyBeforeEngine()
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
    public async Task StartupRejectsAssetWhoseContentDoesNotMatchManifestHash()
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
    public async Task StartupRejectsInvalidUtf8Stylesheet()
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

    [Fact]
    public async Task StartupRejectsMissingFingerprintStylesheet()
    {
        string rootPath = CreateTemporaryRoot();
        byte[] fontBytes = "wOF2-missing-css"u8.ToArray();
        const string fileName = "missing-css.woff2";
        string sha256 = Convert.ToHexString(SHA256.HashData(fontBytes)).ToLowerInvariant();
        const string css = "body { font-family: OdfKit Test; }";
        string cssSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(css)))
            .ToLowerInvariant();
        string cssFileName = $"webfonts.{cssSha256[..16]}.css";
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, fileName),
            fontBytes,
            TestContext.Current.CancellationToken);
        await WriteManifestAsync(
            rootPath,
            fileName,
            fontBytes.Length,
            sha256,
            css);
        File.Delete(Path.Combine(rootPath, cssFileName));

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => StartApplicationAsync(rootPath));
        }
        finally
        {
            DeleteTemporaryRoot(rootPath);
        }
    }

    private static OdfWebFontGenerationRequest CreateGenerationRequest(string text = "A𠆩")
        => new()
        {
            FontSourceId = "trusted-dynamic-face",
            ProfileId = "dynamic-test-v1",
            FontFamily = "OdfKit Dynamic Test",
            Sequences = [text],
            Formats = [WebFontFormat.Woff2]
        };

    private static async Task AssertStatusAsync(
        HttpClient client,
        string text,
        HttpStatusCode expected)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/_odf-fonts/generate",
            CreateGenerationRequest(text),
            TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.CacheControl is { NoStore: true, NoCache: true });
    }

    private static async Task<WebApplication> StartGenerationApplicationAsync(
        string rootPath,
        IWebFontSubsetEngine engine,
        int permitLimit,
        int maxRequestBodyBytes = 64 * 1024,
        bool seedInitialManifest = true,
        int queueCapacity = 2)
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
                options.QueueCapacity = queueCapacity;
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
        await File.WriteAllTextAsync(
            Path.Combine(rootPath, "webfonts.json"),
            JsonSerializer.Serialize(manifest, ManifestJsonOptions),
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

    private sealed class CoverageAwareDynamicAssetEngine : IWebFontSubsetEngine, IWebFontTextCoverageFilter
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public string[] GeneratedSequences { get; private set; } = [];

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
                ? []
                : [WebFontTextSequence.Create(supported)];
            return Task.FromResult(result);
        }

        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            GeneratedSequences = GeneratedSequences
                .Concat(request.Sequences.Select(sequence => sequence.Text))
                .ToArray();
            byte[] bytes = "wOF2-mixed-text-generation"u8.ToArray();
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            const string fileName = "mixed.woff2";
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
                        UnicodeRanges = ["U+20000-2FFFF"]
                    }
                ]
            };
        }
    }

    private sealed class BlockingHostingEngine : IWebFontSubsetEngine
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(request.Sequences[0].Text);
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string fileName = $"queue-{sha256[..16]}.woff2";
            string hashDirectory = Path.Combine(destinationDirectory, sha256);
            Directory.CreateDirectory(hashDirectory);
            await File.WriteAllBytesAsync(Path.Combine(hashDirectory, fileName), bytes, cancellationToken);
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
                        UnicodeRanges = ["U+20000-2FFFF"]
                    }
                ]
            };
        }
    }

    private sealed class ClassifiedFailureEngine : IWebFontSubsetEngine
    {
        public Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
            => Task.FromException<WebFontManifest>(request.Sequences[0].Text switch
            {
                "argument" => new ArgumentException("Invalid test argument.", nameof(request)),
                "unsupported" => new NotSupportedException(),
                "invalid-data" => new InvalidDataException(),
                "io" => new IOException(),
                "invalid-operation" => new InvalidOperationException(),
                "timeout" => new OperationCanceledException(),
                "unexpected" => new InvalidOperationException("Unexpected test failure."),
                _ => new InvalidOperationException()
            });
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

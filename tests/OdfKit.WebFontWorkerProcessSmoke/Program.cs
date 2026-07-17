using System.Security.Cryptography;
using System.Net;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Buffers.Binary;
using System.Security.Claims;
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
using OdfKit.WebFonts;
using OdfKit.WebFonts.Hosting.AspNetCore;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Worker;

namespace OdfKit.WebFontWorkerProcessSmoke;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length is not (7 or 8))
        {
            return 2;
        }

        string cacheDirectory = Path.GetFullPath(args[0]);
        string assetDirectory = Path.GetFullPath(args[1]);
        string counterPath = Path.GetFullPath(args[2]);
        string gatePath = Path.GetFullPath(args[3]);
        string readyPath = Path.GetFullPath(args[4]);
        string fontPath = Path.GetFullPath(args[5]);
        string sourceSha256 = args[6];
        string? mode = args.Length == 8 ? args[7] : null;
        bool holdUntilKilled = string.Equals(mode, "hold-until-killed", StringComparison.Ordinal);
        bool runBoundedLoad = string.Equals(mode, "bounded-load", StringComparison.Ordinal);
        if (mode is not null && !holdUntilKilled && !runBoundedLoad)
        {
            return 2;
        }

        using var timeout = new CancellationTokenSource(
            runBoundedLoad ? TimeSpan.FromMinutes(3) : TimeSpan.FromSeconds(30));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(readyPath)!);
            await File.WriteAllTextAsync(readyPath, "ready", timeout.Token).ConfigureAwait(false);
            while (!File.Exists(gatePath))
            {
                await Task.Delay(25, timeout.Token).ConfigureAwait(false);
            }

            var engineOptions = new ManagedOpenTypeWebFontEngineOptions
            {
                MaxOutputBytes = 32L * 1024 * 1024,
                MaxSourceBytes = 256L * 1024 * 1024,
                MaxUnicodeScalars = 1024
            };
            engineOptions.FontSources["process-smoke"] = fontPath;
            var fontEngine = new ManagedOpenTypeWebFontSubsetEngine(engineOptions);
            if (runBoundedLoad)
            {
                await RunBoundedLoadAsync(
                    fontEngine,
                    cacheDirectory,
                    assetDirectory,
                    counterPath,
                    sourceSha256,
                    timeout.Token).ConfigureAwait(false);
                return 0;
            }

            var engine = new CrossProcessCountingEngine(
                counterPath,
                string.Concat(readyPath, ".engine-started"),
                holdUntilKilled,
                TimeSpan.FromMilliseconds(500),
                fontEngine);
            await using var worker = new WebFontGenerationWorker(
                engine,
                new WebFontWorkerOptions
                {
                    DurableCacheDirectory = cacheDirectory,
                    QueueCapacity = 2,
                    MaxConcurrency = 1,
                    MaxMemoryCacheEntries = 1,
                    JobTimeout = TimeSpan.FromSeconds(15)
                });
            WebFontManifest manifest = await worker.GenerateAsync(
                CreateRequest(sourceSha256, "process-smoke-v1"),
                assetDirectory,
                timeout.Token).ConfigureAwait(false);
            WebFontAsset asset = manifest.Assets.Single();
            string assetPath = Path.Combine(assetDirectory, asset.Sha256, asset.FileName);
            byte[] signature = new byte[4];
            await using (FileStream stream = File.OpenRead(assetPath))
            {
                await stream.ReadExactlyAsync(signature, timeout.Token).ConfigureAwait(false);
            }
            string actualSha256 = Convert.ToHexString(SHA256.HashData(
                await File.ReadAllBytesAsync(assetPath, timeout.Token).ConfigureAwait(false))).ToLowerInvariant();
            if (!signature.AsSpan().SequenceEqual("wOF2"u8)
                || !string.Equals(actualSha256, asset.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The generated asset failed signature or digest validation.");
            }

            await using (FileStream stream = File.OpenRead(assetPath))
            {
                ManagedOpenTypeWebFontVerifier.VerifyContainsScalars(
                    stream,
                    WebFontFormat.Woff2,
                    [0x41, 0x201A9]);
            }

            await VerifyCorruptOutputRejectedAsync(assetPath, timeout.Token).ConfigureAwait(false);

            if (!holdUntilKilled)
            {
                await VerifyMissingUnicodeRejectedAsync(
                    fontEngine,
                    assetDirectory,
                    sourceSha256,
                    timeout.Token).ConfigureAwait(false);
                await VerifyDynamicHttpAsync(
                    fontEngine,
                    assetDirectory,
                    sourceSha256,
                    timeout.Token).ConfigureAwait(false);
            }
            Console.WriteLine(asset.Sha256);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static WebFontSubsetRequest CreateRequest(string sourceSha256, string profileId)
        => new()
        {
            Face = new WebFontFaceIdentity
            {
                FontSourceId = "process-smoke",
                SourceSha256 = sourceSha256
            },
            ProfileId = profileId,
            FontFamily = "OdfKit Process Smoke",
            Sequences = [WebFontTextSequence.Create("𠆩A\r\n\uFEFF")],
            Formats = [WebFontFormat.Woff2]
        };

    private static async Task RunBoundedLoadAsync(
        IWebFontSubsetEngine fontEngine,
        string cacheDirectory,
        string assetDirectory,
        string counterPath,
        string sourceSha256,
        CancellationToken cancellationToken)
    {
        const int requestCount = 128;
        const int uniqueKeyCount = 16;
        var engine = new CrossProcessCountingEngine(
            counterPath,
            string.Concat(counterPath, ".engine-started"),
            holdUntilKilled: false,
            TimeSpan.Zero,
            fontEngine);
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions
            {
                DurableCacheDirectory = cacheDirectory,
                QueueCapacity = 32,
                MaxConcurrency = 2,
                MaxMemoryCacheEntries = uniqueKeyCount,
                JobTimeout = TimeSpan.FromMinutes(1)
            });

        using Process process = Process.GetCurrentProcess();
        TimeSpan initialCpu = process.TotalProcessorTime;
        long initialAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        Task<WebFontManifest>[] requests = Enumerable.Range(0, requestCount)
            .Select(index => worker.GenerateAsync(
                CreateRequest(sourceSha256, $"bounded-load-{index % uniqueKeyCount}"),
                assetDirectory,
                cancellationToken))
            .ToArray();
        WebFontManifest[] manifests = await Task.WhenAll(requests).ConfigureAwait(false);
        stopwatch.Stop();
        process.Refresh();

        int engineCalls = File.ReadLines(counterPath).Count();
        int uniqueProfiles = manifests.Select(manifest => manifest.ProfileId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (engineCalls != uniqueKeyCount || uniqueProfiles != uniqueKeyCount
            || manifests.Any(manifest => manifest.Assets.Count != 1))
        {
            throw new InvalidDataException("The bounded load did not preserve single-flight or manifest integrity.");
        }

        double cacheHitRatio = (requestCount - engineCalls) / (double)requestCount;
        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - initialAllocatedBytes;
        double cpuSeconds = (process.TotalProcessorTime - initialCpu).TotalSeconds;
        long peakWorkingSetBytes = process.PeakWorkingSet64;
        var evidence = new
        {
            schemaVersion = 1,
            requestCount,
            uniqueKeyCount,
            engineCalls,
            cacheHitRatio,
            elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            cpuMilliseconds = (long)(cpuSeconds * 1000),
            peakWorkingSetBytes,
            allocatedBytes
        };
        await File.WriteAllTextAsync(
            string.Concat(counterPath, ".metrics.json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);

        if (cacheHitRatio < 0.85
            || stopwatch.Elapsed > TimeSpan.FromMinutes(2)
            || cpuSeconds > 240
            || peakWorkingSetBytes > 1024L * 1024 * 1024
            || allocatedBytes > 2L * 1024 * 1024 * 1024)
        {
            throw new InvalidDataException("The bounded load exceeded its reproducible resource budget.");
        }
    }

    private static async Task VerifyDynamicHttpAsync(
        IWebFontSubsetEngine engine,
        string assetDirectory,
        string sourceSha256,
        CancellationToken cancellationToken)
    {
        string rootPath = Path.Combine(assetDirectory, $"http-{Environment.ProcessId}");
        Directory.CreateDirectory(rootPath);
        byte[] initialBytes = "wOF2-initial"u8.ToArray();
        string initialSha256 = Convert.ToHexString(SHA256.HashData(initialBytes)).ToLowerInvariant();
        string initialDirectory = Path.Combine(rootPath, initialSha256);
        Directory.CreateDirectory(initialDirectory);
        await File.WriteAllBytesAsync(
            Path.Combine(initialDirectory, "initial.woff2"),
            initialBytes,
            cancellationToken).ConfigureAwait(false);
        var initialManifest = new WebFontManifest
        {
            ProfileId = "dynamic-http-initial-v1",
            Assets =
            [
                new WebFontAsset
                {
                    FileName = "initial.woff2",
                    Sha256 = initialSha256,
                    ByteLength = initialBytes.Length,
                    Format = WebFontFormat.Woff2,
                    FontFamily = "OdfKit Initial",
                    UnicodeRanges = ["U+41"]
                }
            ]
        };
        await File.WriteAllTextAsync(
            Path.Combine(rootPath, "webfonts.json"),
            JsonSerializer.Serialize(initialManifest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            }),
            cancellationToken).ConfigureAwait(false);

        string apiKey = Guid.NewGuid().ToString("N");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication(SmokeAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SmokeAuthenticationHandler>(
                SmokeAuthenticationHandler.SchemeName,
                options => options.ClaimsIssuer = apiKey);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("dynamic-http-smoke", policy => policy.RequireAuthenticatedUser());
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;
            options.AddFixedWindowLimiter(
                "dynamic-http-smoke",
                limiterOptions =>
                {
                    limiterOptions.PermitLimit = 1;
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
                options.AuthorizationPolicyName = "dynamic-http-smoke";
                options.RateLimiterPolicyName = "dynamic-http-smoke";
                options.AllowedFaces.Add(new WebFontFaceIdentity
                {
                    FontSourceId = "process-smoke",
                    SourceSha256 = sourceSha256
                });
                options.AllowedProfileIds.Add("dynamic-http-smoke-v1");
            },
            options =>
            {
                options.DurableCacheDirectory = Path.Combine(rootPath, "cache");
                options.QueueCapacity = 2;
                options.MaxConcurrency = 1;
                options.JobTimeout = TimeSpan.FromSeconds(20);
            });

        await using WebApplication application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.UseRateLimiter();
        application.MapOdfWebFonts();
        await application.StartAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IServer server = application.Services.GetRequiredService<IServer>();
            string address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var request = new OdfWebFontGenerationRequest
            {
                FontSourceId = "process-smoke",
                ProfileId = "dynamic-http-smoke-v1",
                FontFamily = "OdfKit Dynamic HTTP Smoke",
                Sequences = ["A𠆩"],
                Formats = [WebFontFormat.Woff2]
            };
            using HttpResponseMessage unauthorized = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                request,
                cancellationToken).ConfigureAwait(false);
            if (unauthorized.StatusCode != HttpStatusCode.Unauthorized)
            {
                throw new InvalidDataException("The real generation endpoint did not reject an unauthorized request.");
            }

            client.DefaultRequestHeaders.Add("X-OdfKit-WebFont-Key", apiKey);
            using HttpResponseMessage generated = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                request,
                cancellationToken).ConfigureAwait(false);
            if (generated.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidDataException("The real generation endpoint failed.");
            }

            WebFontManifest manifest = await generated.Content.ReadFromJsonAsync<WebFontManifest>(
                cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The real generation endpoint returned no manifest.");

            using HttpResponseMessage limited = await client.PostAsJsonAsync(
                "/_odf-fonts/generate",
                request,
                cancellationToken).ConfigureAwait(false);
            if (limited.StatusCode != HttpStatusCode.TooManyRequests)
            {
                throw new InvalidDataException("The real generation endpoint did not enforce its rate limiter.");
            }

            WebFontAsset asset = manifest.Assets.Single();
            using HttpResponseMessage response = await client.GetAsync(
                $"/_odf-fonts/{asset.Sha256}/{asset.FileName}",
                cancellationToken).ConfigureAwait(false);
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            string actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (response.StatusCode != HttpStatusCode.OK
                || !bytes.AsSpan(0, 4).SequenceEqual("wOF2"u8)
                || !string.Equals(actualSha256, asset.Sha256, StringComparison.Ordinal)
                || response.Headers.CacheControl?.Extensions.Any(item => item.Name == "immutable") != true)
            {
                throw new InvalidDataException("The real generated asset failed its HTTP integrity checks.");
            }

            Task[] parallelReads = Enumerable.Range(0, 256)
                .Select(_ => VerifyImmutableAssetReadAsync(client, asset, cancellationToken))
                .ToArray();
            await Task.WhenAll(parallelReads).ConfigureAwait(false);
        }
        finally
        {
            await application.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task VerifyImmutableAssetReadAsync(
        HttpClient client,
        WebFontAsset asset,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(
            $"/_odf-fonts/{asset.Sha256}/{asset.FileName}",
            cancellationToken).ConfigureAwait(false);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        string actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (response.StatusCode != HttpStatusCode.OK
            || !string.Equals(actualSha256, asset.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A parallel immutable asset read failed its integrity check.");
        }
    }

    private static async Task VerifyCorruptOutputRejectedAsync(
        string assetPath,
        CancellationToken cancellationToken)
    {
        byte[] validBytes = await File.ReadAllBytesAsync(assetPath, cancellationToken).ConfigureAwait(false);
        byte[] truncatedBytes = validBytes[..^1];
        VerifyInvalidOutputRejected(truncatedBytes);

        byte[] corruptBytes = (byte[])validBytes.Clone();
        int corruptOffset = Math.Max(48, corruptBytes.Length / 2);
        corruptBytes[corruptOffset] ^= 0x5A;
        VerifyInvalidOutputRejected(corruptBytes);

        byte[] expandedBombBytes = (byte[])validBytes.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(expandedBombBytes.AsSpan(16, 4), int.MaxValue);
        VerifyInvalidOutputRejected(expandedBombBytes);
    }

    private static void VerifyInvalidOutputRejected(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            ManagedOpenTypeWebFontVerifier.VerifyContainsScalars(
                stream,
                WebFontFormat.Woff2,
                [0x41, 0x201A9]);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidDataException("The managed verifier silently accepted a damaged WOFF2 asset.");
    }

    private static async Task VerifyMissingUnicodeRejectedAsync(
        IWebFontSubsetEngine engine,
        string destinationDirectory,
        string sourceSha256,
        CancellationToken cancellationToken)
    {
        WebFontSubsetRequest request = CreateRequest(sourceSha256, "process-smoke-v1");
        request = new WebFontSubsetRequest
        {
            Face = new WebFontFaceIdentity
            {
                FontSourceId = request.Face.FontSourceId,
                SourceSha256 = sourceSha256
            },
            ProfileId = "process-smoke-missing-v1",
            FontFamily = request.FontFamily,
            Sequences = [WebFontTextSequence.Create(char.ConvertFromUtf32(0x10FFFF))],
            Formats = request.Formats
        };
        try
        {
            await engine.GenerateAsync(
                request,
                Path.Combine(destinationDirectory, $"missing-{Environment.ProcessId}"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidDataException("The subset engine silently accepted a missing Unicode scalar.");
    }

    private sealed class SmokeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "WebFontProcessSmoke";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string supplied = Request.Headers["X-OdfKit-WebFont-Key"].ToString();
            string expected = Options.ClaimsIssuer ?? string.Empty;
            if (!string.Equals(supplied, expected, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "webfont-process-smoke")],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                SchemeName)));
        }
    }

    private sealed class CrossProcessCountingEngine(
        string counterPath,
        string engineStartedPath,
        bool holdUntilKilled,
        TimeSpan generationDelay,
        IWebFontSubsetEngine inner) : IWebFontSubsetEngine
    {
        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            await RecordCallAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                engineStartedPath,
                "started",
                cancellationToken).ConfigureAwait(false);
            if (holdUntilKilled)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            if (generationDelay > TimeSpan.Zero)
            {
                await Task.Delay(generationDelay, cancellationToken).ConfigureAwait(false);
            }
            return await inner.GenerateAsync(
                request,
                destinationDirectory,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task RecordCallAsync(CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(counterPath)!);
            byte[] line = "generated\n"u8.ToArray();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = new FileStream(
                        counterPath,
                        FileMode.OpenOrCreate,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 128,
                        FileOptions.Asynchronous | FileOptions.WriteThrough);
                    stream.Seek(0, SeekOrigin.End);
                    await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}

using OdfKit.WebFonts.Worker;

using System.Text;
using System.Text.Json.Nodes;

namespace OdfKit.WebFonts.Tests;

public sealed class WebFontWorkerTests
{
    [Fact]
    public async Task Worker_ReusesCompletedManifestWithoutRegeneration()
    {
        var engine = new CountingEngine();
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions { MaxMemoryCacheEntries = 8 });
        WebFontSubsetRequest request = CreateRequest();

        WebFontManifest first = await worker.GenerateAsync(
            request,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        WebFontManifest second = await worker.GenerateAsync(
            request,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(1, engine.CallCount);
    }

    [Fact]
    public async Task Worker_RejectsInvalidNullableStateBeforeCreatingKey()
    {
        var engine = new CountingEngine();
        await using var worker = new WebFontGenerationWorker(engine, new WebFontWorkerOptions());
        WebFontSubsetRequest template = CreateRequest();
        WebFontSubsetRequest request = new()
        {
            Face = template.Face,
            ProfileId = template.ProfileId,
            FontFamily = null!,
            Sequences = template.Sequences,
            Formats = template.Formats
        };

        await Assert.ThrowsAsync<ArgumentException>(() => worker.GenerateAsync(
            request,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken));
        Assert.Equal(0, engine.CallCount);
    }

    [Fact]
    public async Task Worker_RejectsInvalidSourceDigestBeforeQueueing()
    {
        var engine = new CountingEngine();
        await using var worker = new WebFontGenerationWorker(engine, new WebFontWorkerOptions());
        WebFontSubsetRequest template = CreateRequest();
        WebFontSubsetRequest request = new()
        {
            Face = new WebFontFaceIdentity
            {
                FontSourceId = template.Face.FontSourceId,
                SourceSha256 = "not-a-sha256"
            },
            ProfileId = template.ProfileId,
            FontFamily = template.FontFamily,
            Sequences = template.Sequences,
            Formats = template.Formats
        };

        await Assert.ThrowsAsync<ArgumentException>(() => worker.GenerateAsync(
            request,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken));
        Assert.Equal(0, engine.CallCount);
    }

    [Fact]
    public async Task Worker_CoalescesConcurrentIdenticalRequests()
    {
        var engine = new CountingEngine();
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions { QueueCapacity = 8, MaxConcurrency = 2 });
        WebFontSubsetRequest request = CreateRequest();

        Task<WebFontManifest>[] tasks = Enumerable.Range(0, 1000)
            .Select(_ => worker.GenerateAsync(request, Path.GetTempPath(), TestContext.Current.CancellationToken))
            .ToArray();
        await Task.WhenAll(tasks).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, engine.CallCount);
        Assert.All(tasks, task => Assert.Equal("worker-test", task.Result.ProfileId));
    }

    [Fact]
    public async Task Worker_BoundsConcurrencyAcrossTenThousandRequests()
    {
        var engine = new ConcurrencyTrackingEngine();
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions
            {
                QueueCapacity = 128,
                MaxConcurrency = 8,
                MaxMemoryCacheEntries = 128
            });

        Task<WebFontManifest>[] tasks = Enumerable.Range(0, 10_000)
            .Select(index => worker.GenerateAsync(
                CreateRequest($"load-{index % 100}"),
                Path.GetTempPath(),
                TestContext.Current.CancellationToken))
            .ToArray();
        await Task.WhenAll(tasks).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(100, engine.CallCount);
        Assert.InRange(engine.MaximumConcurrency, 2, 8);
    }

    [Fact]
    public async Task Worker_CoalescesSemanticallyEquivalentReorderedRequests()
    {
        var engine = new CountingEngine();
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions { QueueCapacity = 8, MaxConcurrency = 2 });
        WebFontSubsetRequest template = CreateRequest();
        WebFontSubsetRequest first = new()
        {
            Face = template.Face,
            ProfileId = template.ProfileId,
            FontFamily = template.FontFamily,
            Sequences = [WebFontTextSequence.Create("𠀀A"), WebFontTextSequence.Create("A")],
            Formats = template.Formats
        };
        WebFontSubsetRequest second = new()
        {
            Face = first.Face,
            ProfileId = first.ProfileId,
            FontFamily = first.FontFamily,
            Sequences =
            [
                WebFontTextSequence.Create("A"),
                WebFontTextSequence.Create("𠀀A"),
                WebFontTextSequence.Create("A")
            ],
            Formats = [WebFontFormat.Woff2, WebFontFormat.Woff2]
        };

        Task<WebFontManifest> firstTask = worker.GenerateAsync(
            first,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        Task<WebFontManifest> secondTask = worker.GenerateAsync(
            second,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, engine.CallCount);
    }

    [Fact]
    public async Task Worker_DoesNotCoalesceLengthPrefixedFieldsWithDelimiterCollision()
    {
        var engine = new CountingEngine();
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions { QueueCapacity = 8, MaxConcurrency = 2 });
        WebFontSubsetRequest template = CreateRequest();
        WebFontSubsetRequest first = new()
        {
            Face = template.Face,
            ProfileId = "worker|test",
            FontFamily = "WorkerTest",
            Sequences = template.Sequences,
            Formats = template.Formats
        };
        WebFontSubsetRequest second = new()
        {
            Face = template.Face,
            ProfileId = "worker",
            FontFamily = "test|WorkerTest",
            Sequences = template.Sequences,
            Formats = template.Formats
        };

        Task<WebFontManifest> firstTask = worker.GenerateAsync(
            first,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        Task<WebFontManifest> secondTask = worker.GenerateAsync(
            second,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        WebFontManifest[] results = await Task.WhenAll(firstTask, secondTask)
            .WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, engine.CallCount);
        Assert.Equal(first.ProfileId, results[0].ProfileId);
        Assert.Equal(second.ProfileId, results[1].ProfileId);
    }

    [Fact]
    public async Task Worker_DoesNotCoalesceRequestsWithDifferentSequenceOrder()
    {
        var engine = new CountingEngine();
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions { QueueCapacity = 8, MaxConcurrency = 2 });
        WebFontSubsetRequest first = CreateRequest();
        WebFontSubsetRequest second = new()
        {
            Face = first.Face,
            ProfileId = first.ProfileId,
            FontFamily = first.FontFamily,
            Sequences = [WebFontTextSequence.Create("A𠀀")],
            Formats = first.Formats
        };

        Task<WebFontManifest> firstTask = worker.GenerateAsync(
            first,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        Task<WebFontManifest> secondTask = worker.GenerateAsync(
            second,
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, engine.CallCount);
    }

    [Fact]
    public async Task Worker_RejectsWorkBeyondBoundedQueueCapacity()
    {
        var engine = new BlockingEngine();
        await using var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions
            {
                QueueCapacity = 1,
                MaxConcurrency = 1,
                JobTimeout = TimeSpan.FromSeconds(5)
            });

        Task<WebFontManifest> running = worker.GenerateAsync(
            CreateRequest("running"),
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        await engine.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task<WebFontManifest> queued = worker.GenerateAsync(
            CreateRequest("queued"),
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        Task<WebFontManifest> rejected = worker.GenerateAsync(
            CreateRequest("rejected"),
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => rejected.WaitAsync(TestContext.Current.CancellationToken));

        engine.Release.TrySetResult();
        await Task.WhenAll(running, queued).WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, engine.CallCount);
    }

    [Fact]
    public async Task Worker_DisposeCompletesRunningAndQueuedWaiters()
    {
        var engine = new BlockingEngine();
        var worker = new WebFontGenerationWorker(
            engine,
            new WebFontWorkerOptions
            {
                QueueCapacity = 2,
                MaxConcurrency = 1,
                JobTimeout = TimeSpan.FromSeconds(30)
            });
        Task<WebFontManifest> running = worker.GenerateAsync(
            CreateRequest("dispose-running"),
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);
        await engine.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task<WebFontManifest> queued = worker.GenerateAsync(
            CreateRequest("dispose-queued"),
            Path.GetTempPath(),
            TestContext.Current.CancellationToken);

        await worker.DisposeAsync().AsTask().WaitAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => running.WaitAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => queued.WaitAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.GenerateAsync(
            CreateRequest("disposed"),
            Path.GetTempPath(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Worker_DurableCacheCoalescesAcrossWorkerInstances()
    {
        string root = CreateTemporaryRoot();
        string assets = Path.Combine(root, "assets");
        string cache = Path.Combine(root, "cache");
        try
        {
            var engine = new AssetWritingEngine();
            var options = new WebFontWorkerOptions
            {
                DurableCacheDirectory = cache,
                MaxConcurrency = 1,
                QueueCapacity = 4
            };
            await using var firstWorker = new WebFontGenerationWorker(engine, options);
            await using var secondWorker = new WebFontGenerationWorker(engine, options);
            WebFontSubsetRequest request = CreateRequest();

            Task<WebFontManifest> first = firstWorker.GenerateAsync(
                request,
                assets,
                TestContext.Current.CancellationToken);
            Task<WebFontManifest> second = secondWorker.GenerateAsync(
                request,
                assets,
                TestContext.Current.CancellationToken);
            WebFontManifest[] results = await Task.WhenAll(first, second)
                .WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, engine.CallCount);
            Assert.Equal(results[0].Assets[0].Sha256, results[1].Assets[0].Sha256);
            Assert.Single(Directory.EnumerateFiles(cache, "*.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Worker_DurableCacheRejectsTamperedAsset()
    {
        string root = CreateTemporaryRoot();
        string assets = Path.Combine(root, "assets");
        string cache = Path.Combine(root, "cache");
        try
        {
            var engine = new AssetWritingEngine();
            var options = new WebFontWorkerOptions { DurableCacheDirectory = cache };
            WebFontManifest manifest;
            await using (var firstWorker = new WebFontGenerationWorker(engine, options))
            {
                manifest = await firstWorker.GenerateAsync(
                    CreateRequest(),
                    assets,
                    TestContext.Current.CancellationToken);
            }

            WebFontAsset asset = Assert.Single(manifest.Assets);
            string path = Path.Combine(assets, asset.Sha256, asset.FileName);
            await File.WriteAllBytesAsync(path, [0x00], TestContext.Current.CancellationToken);

            await using var secondWorker = new WebFontGenerationWorker(engine, options);
            await Assert.ThrowsAsync<InvalidDataException>(() => secondWorker.GenerateAsync(
                CreateRequest(),
                assets,
                TestContext.Current.CancellationToken));
            Assert.Equal(1, engine.CallCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Worker_DurableCacheRevalidatesAssetWithinSameWorker()
    {
        string root = CreateTemporaryRoot();
        string assets = Path.Combine(root, "assets");
        string cache = Path.Combine(root, "cache");
        try
        {
            var engine = new AssetWritingEngine();
            await using var worker = new WebFontGenerationWorker(
                engine,
                new WebFontWorkerOptions
                {
                    DurableCacheDirectory = cache,
                    MaxMemoryCacheEntries = 16
                });
            WebFontManifest manifest = await worker.GenerateAsync(
                CreateRequest(),
                assets,
                TestContext.Current.CancellationToken);
            WebFontAsset asset = Assert.Single(manifest.Assets);
            string path = Path.Combine(assets, asset.Sha256, asset.FileName);
            await File.WriteAllBytesAsync(path, [0x00], TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidDataException>(() => worker.GenerateAsync(
                CreateRequest(),
                assets,
                TestContext.Current.CancellationToken));
            Assert.Equal(1, engine.CallCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Worker_DurableCacheRejectsAssetBeyondConfiguredLimit()
    {
        string root = CreateTemporaryRoot();
        try
        {
            var engine = new AssetWritingEngine();
            await using var worker = new WebFontGenerationWorker(
                engine,
                new WebFontWorkerOptions
                {
                    DurableCacheDirectory = Path.Combine(root, "cache"),
                    MaxCachedAssetBytes = 4
                });

            await Assert.ThrowsAsync<InvalidDataException>(() => worker.GenerateAsync(
                CreateRequest(),
                Path.Combine(root, "assets"),
                TestContext.Current.CancellationToken));
            Assert.Equal(1, engine.CallCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Worker_DurableCacheRejectsNullDigestWithoutLeakingNullReference()
    {
        string root = CreateTemporaryRoot();
        string assets = Path.Combine(root, "assets");
        string cache = Path.Combine(root, "cache");
        try
        {
            var engine = new AssetWritingEngine();
            var options = new WebFontWorkerOptions { DurableCacheDirectory = cache };
            await PopulateCacheAsync(engine, options, CreateRequest(), assets);
            JsonObject manifest = await ReadCacheManifestAsync(cache);
            manifest["Assets"]!.AsArray()[0]!["Sha256"] = null;
            await WriteCacheManifestAsync(cache, manifest);

            await using var worker = new WebFontGenerationWorker(engine, options);
            await Assert.ThrowsAsync<InvalidDataException>(() => worker.GenerateAsync(
                CreateRequest(),
                assets,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Worker_DurableCacheRejectsMissingRequestedFormat()
    {
        string root = CreateTemporaryRoot();
        string assets = Path.Combine(root, "assets");
        string cache = Path.Combine(root, "cache");
        try
        {
            var engine = new AssetWritingEngine();
            var options = new WebFontWorkerOptions { DurableCacheDirectory = cache };
            WebFontSubsetRequest template = CreateRequest();
            WebFontSubsetRequest request = new()
            {
                Face = template.Face,
                ProfileId = template.ProfileId,
                FontFamily = template.FontFamily,
                Sequences = template.Sequences,
                Formats = [WebFontFormat.Woff2, WebFontFormat.Woff]
            };
            await PopulateCacheAsync(engine, options, request, assets);
            JsonObject manifest = await ReadCacheManifestAsync(cache);
            manifest["Assets"]!.AsArray().RemoveAt(1);
            await WriteCacheManifestAsync(cache, manifest);

            await using var worker = new WebFontGenerationWorker(engine, options);
            await Assert.ThrowsAsync<InvalidDataException>(() => worker.GenerateAsync(
                request,
                assets,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Worker_DurableCacheRejectsUnicodeRangeMismatch()
    {
        string root = CreateTemporaryRoot();
        string assets = Path.Combine(root, "assets");
        string cache = Path.Combine(root, "cache");
        try
        {
            var engine = new AssetWritingEngine();
            var options = new WebFontWorkerOptions { DurableCacheDirectory = cache };
            await PopulateCacheAsync(engine, options, CreateRequest(), assets);
            JsonObject manifest = await ReadCacheManifestAsync(cache);
            manifest["Assets"]!.AsArray()[0]!["UnicodeRanges"] = new JsonArray("U+10FFFF");
            await WriteCacheManifestAsync(cache, manifest);

            await using var worker = new WebFontGenerationWorker(engine, options);
            await Assert.ThrowsAsync<InvalidDataException>(() => worker.GenerateAsync(
                CreateRequest(),
                assets,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Worker_DurableCacheRejectsLinkedAssetDirectory()
    {
        string root = CreateTemporaryRoot();
        string assets = Path.Combine(root, "assets");
        string cache = Path.Combine(root, "cache");
        string? linkedDirectory = null;
        try
        {
            var engine = new AssetWritingEngine();
            var options = new WebFontWorkerOptions { DurableCacheDirectory = cache };
            WebFontManifest manifest = await GenerateAndDisposeAsync(
                engine,
                options,
                CreateRequest(),
                assets);
            WebFontAsset asset = Assert.Single(manifest.Assets);
            linkedDirectory = Path.Combine(assets, asset.Sha256);
            string outsideDirectory = Path.Combine(root, "outside");
            Directory.Move(linkedDirectory, outsideDirectory);
            _ = Directory.CreateSymbolicLink(linkedDirectory, outsideDirectory);

            await using var worker = new WebFontGenerationWorker(engine, options);
            await Assert.ThrowsAsync<InvalidDataException>(() => worker.GenerateAsync(
                CreateRequest(),
                assets,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            if (linkedDirectory is not null && Directory.Exists(linkedDirectory))
            {
                Directory.Delete(linkedDirectory);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private static WebFontSubsetRequest CreateRequest(string fontSourceId = "test")
        => new()
        {
            Face = new WebFontFaceIdentity
            {
                FontSourceId = fontSourceId,
                SourceSha256 = new string('a', 64)
            },
            ProfileId = "worker-test",
            FontFamily = "WorkerTest",
            Sequences = [WebFontTextSequence.Create("𠀀A")],
            Formats = [WebFontFormat.Woff2]
        };

    private static string CreateTemporaryRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"OdfKit.WebFontWorker.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task PopulateCacheAsync(
        IWebFontSubsetEngine engine,
        WebFontWorkerOptions options,
        WebFontSubsetRequest request,
        string assets)
    {
        await using var worker = new WebFontGenerationWorker(engine, options);
        _ = await worker.GenerateAsync(
            request,
            assets,
            TestContext.Current.CancellationToken);
    }

    private static async Task<WebFontManifest> GenerateAndDisposeAsync(
        IWebFontSubsetEngine engine,
        WebFontWorkerOptions options,
        WebFontSubsetRequest request,
        string assets)
    {
        await using var worker = new WebFontGenerationWorker(engine, options);
        return await worker.GenerateAsync(
            request,
            assets,
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonObject> ReadCacheManifestAsync(string cache)
    {
        string path = Assert.Single(Directory.EnumerateFiles(cache, "*.json"));
        string json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        return JsonNode.Parse(json)!.AsObject();
    }

    private static async Task WriteCacheManifestAsync(string cache, JsonObject manifest)
    {
        string path = Assert.Single(Directory.EnumerateFiles(cache, "*.json"));
        await File.WriteAllTextAsync(
            path,
            manifest.ToJsonString(),
            TestContext.Current.CancellationToken);
    }

    private sealed class CountingEngine : IWebFontSubsetEngine
    {
        private int _callCount;

        public int CallCount => _callCount;

        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            await Task.Delay(50, cancellationToken);
            return new WebFontManifest { ProfileId = request.ProfileId };
        }
    }

    private sealed class BlockingEngine : IWebFontSubsetEngine
    {
        private int _callCount;

        public int CallCount => _callCount;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new WebFontManifest { ProfileId = request.ProfileId };
        }
    }

    private sealed class ConcurrencyTrackingEngine : IWebFontSubsetEngine
    {
        private int _active;
        private int _callCount;
        private int _maximumConcurrency;

        public int CallCount => _callCount;

        public int MaximumConcurrency => _maximumConcurrency;

        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            int active = Interlocked.Increment(ref _active);
            int observed;
            do
            {
                observed = Volatile.Read(ref _maximumConcurrency);
            }
            while (active > observed
                && Interlocked.CompareExchange(ref _maximumConcurrency, active, observed) != observed);

            try
            {
                await Task.Delay(25, cancellationToken);
                return new WebFontManifest { ProfileId = request.ProfileId };
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class AssetWritingEngine : IWebFontSubsetEngine
    {
        private int _callCount;

        public int CallCount => _callCount;

        public async Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            await Task.Delay(75, cancellationToken);
            string[] ranges = request.Sequences
                .SelectMany(sequence => sequence.UnicodeScalars)
                .Where(scalar => scalar != 0xFEFF && !Rune.IsControl(new Rune(scalar)))
                .Distinct()
                .OrderBy(scalar => scalar)
                .Select(scalar => $"U+{scalar:X}")
                .ToArray();
            var assets = new List<WebFontAsset>();
            foreach (WebFontFormat format in request.Formats.Distinct())
            {
                byte[] bytes = [0x77, 0x4F, 0x46, (byte)format, 0x01, 0x02, 0x03, 0x04];
                string sha256 = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
                string extension = format == WebFontFormat.Woff2 ? "woff2" : "woff";
                string fileName = $"worker.{sha256[..16]}.{extension}";
                string directory = Path.Combine(destinationDirectory, sha256);
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(
                    Path.Combine(directory, fileName),
                    bytes,
                    cancellationToken);
                assets.Add(new WebFontAsset
                {
                    FileName = fileName,
                    Sha256 = sha256,
                    ByteLength = bytes.Length,
                    Format = format,
                    FontFamily = request.FontFamily,
                    UnicodeRanges = ranges
                });
            }

            return new WebFontManifest
            {
                ProfileId = request.ProfileId,
                Assets = assets
            };
        }
    }
}

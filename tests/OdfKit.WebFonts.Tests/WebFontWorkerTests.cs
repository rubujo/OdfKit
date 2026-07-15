using OdfKit.WebFonts.Worker;

namespace OdfKit.WebFonts.Tests;

public sealed class WebFontWorkerTests
{
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
}

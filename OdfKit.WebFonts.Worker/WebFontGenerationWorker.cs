using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Worker;

/// <summary>
/// Runs bounded generation jobs and coalesces concurrent identical requests.
/// 執行有界產生工作，並合併同時提出的相同要求。
/// </summary>
public sealed class WebFontGenerationWorker : IWebFontSubsetEngine, IWebFontTextCoverageFilter, IAsyncDisposable
{
    private readonly IWebFontSubsetEngine _engine;
    private readonly WebFontWorkerOptions _options;
    private readonly FileSystemGenerationCache? _durableCache;
    private readonly Channel<GenerationJob> _queue;
    // 以 Lazy 包裹入列作業：ConcurrentDictionary.GetOrAdd 的工廠委派在競爭下可能被呼叫多次，
    // 若直接入列會產生重複的孤兒 job；改為只在勝出項目的 Value 被存取時入列一次。
    private readonly ConcurrentDictionary<string, Lazy<Task<WebFontManifest>>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WebFontManifest> _completed = new(StringComparer.Ordinal);
    private int _completedCount;
    private int _disposeState;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task[] _consumers;

    /// <summary>
    /// Initializes and starts bounded worker consumers.
    /// 初始化並啟動有界 worker consumer。
    /// </summary>
    /// <param name="engine">The isolated subset engine adapter. / 隔離的子集引擎 adapter。</param>
    /// <param name="options">The worker limits. / worker 限制。</param>
    public WebFontGenerationWorker(IWebFontSubsetEngine engine, WebFontWorkerOptions options)
    {
        _engine = engine ?? throw new ArgumentNullException(
            nameof(engine),
            OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        _options = options ?? throw new ArgumentNullException(
            nameof(options),
            OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        if (options.QueueCapacity <= 0
            || options.MaxConcurrency <= 0
            || options.JobTimeout <= TimeSpan.Zero
            || options.MaxMemoryCacheEntries < 0
            || options.MaxCachedManifestBytes <= 0
            || options.MaxCachedAssetCount <= 0
            || options.MaxCachedAssetBytes <= 0
            || options.MaxDurableManifestEntries <= 0
            || options.MaxDurableManifestBytes < options.MaxCachedManifestBytes
            || options.DurableManifestMaxIdle <= TimeSpan.Zero
            || options.MaxDurableAssetBytes < options.MaxCachedAssetBytes
            || options.DurableAssetMaxIdle <= TimeSpan.Zero
            || options.CacheLockRetryDelay <= TimeSpan.Zero
            || options.MaxCacheLockRetryDelay < options.CacheLockRetryDelay
            || options.MaxCacheLockRetryDelay > TimeSpan.FromMilliseconds(int.MaxValue)
            || options.DurableCacheDirectory is not null
                && string.IsNullOrWhiteSpace(options.DurableCacheDirectory))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }

        if (options.DurableCacheDirectory is not null)
        {
            _durableCache = new FileSystemGenerationCache(options.DurableCacheDirectory, options);
        }

        _queue = Channel.CreateBounded<GenerationJob>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = options.MaxConcurrency == 1,
            SingleWriter = false
        });
        _consumers = Enumerable.Range(0, options.MaxConcurrency)
            .Select(_ => ConsumeAsync(_shutdown.Token))
            .ToArray();
    }

    /// <summary>
    /// Generates or reuses a bounded subset while coalescing identical requests.
    /// 產生或重用有界子集，並合併相同要求。
    /// </summary>
    /// <param name="request">The subset request. / 子集要求。</param>
    /// <param name="destinationDirectory">The trusted destination directory. / 受信任的目的目錄。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The generated or cached manifest. / 產生或快取的 manifest。</returns>
    public async Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        string key = CreateKey(request, destinationDirectory);
        // 耐久快取每次命中都必須重新驗證檔案、雜湊與連結狀態，避免同一處理程序內的
        // 記憶體捷徑掩蓋部署後遭竄改或遭替換的資產。
        if (_durableCache is null
            && _completed.TryGetValue(key, out WebFontManifest? completed))
        {
            return completed;
        }

        Lazy<Task<WebFontManifest>> lazy = _inflight.GetOrAdd(
            key,
            k => new Lazy<Task<WebFontManifest>>(() => EnqueueAsync(k, request, destinationDirectory)));
        Task<WebFontManifest> task = lazy.Value;
        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (task.IsCompleted)
            {
                // 必須連同 value 一起比對再移除：若不比對，本次工作完成後由其他呼叫端
                // 為同一 key 新登記的 Lazy 會被誤刪，導致同鍵工作重複執行、單飛失效。
                _inflight.TryRemove(new KeyValuePair<string, Lazy<Task<WebFontManifest>>>(key, lazy));
            }
        }
    }

    /// <summary>
    /// Returns contiguous text sequences supported by the selected face.
    /// 回傳所選 face 支援的連續文字序列。
    /// </summary>
    /// <param name="face">The trusted font face. / 受信任的字型 face。</param>
    /// <param name="sequences">The requested text sequences. / 要求的文字序列。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The supported contiguous sequences; an empty collection means the face has no requested glyphs. / 支援的連續序列；空集合表示該 face 不含任何要求的 glyph。</returns>
    public Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
        WebFontFaceIdentity face,
        IReadOnlyList<WebFontTextSequence> sequences,
        CancellationToken cancellationToken = default)
        => _engine is IWebFontTextCoverageFilter coverageFilter
            ? coverageFilter.FilterSupportedSequencesAsync(face, sequences, cancellationToken)
            : Task.FromResult(sequences);

    /// <summary>
    /// Stops worker consumers and releases owned resources asynchronously.
    /// 以非同步方式停止 worker consumer 並釋放擁有的資源。
    /// </summary>
    /// <returns>A value task that represents asynchronous disposal. / 代表非同步釋放作業的 value task。</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _queue.Writer.TryComplete();
        _shutdown.Cancel();
        try
        {
            await Task.WhenAll(_consumers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        while (_queue.Reader.TryRead(out GenerationJob? job))
        {
            job.Completion.TrySetCanceled(new CancellationToken(canceled: true));
            _inflight.TryRemove(job.Key, out _);
        }

        _shutdown.Dispose();
    }

    private Task<WebFontManifest> EnqueueAsync(
        string key,
        WebFontSubsetRequest request,
        string destinationDirectory)
    {
        var completion = new TaskCompletionSource<WebFontManifest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new GenerationJob(key, request, destinationDirectory, completion);
        if (!_queue.Writer.TryWrite(job))
        {
            completion.SetException(new WebFontQueueFullException());
        }

        return completion.Task;
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (GenerationJob job in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.JobTimeout);
            try
            {
                WebFontManifest manifest = await GenerateOrLoadAsync(job, timeout.Token).ConfigureAwait(false);
                TryCacheCompleted(job.Key, manifest);

                job.Completion.TrySetResult(manifest);
            }
            catch (Exception exception)
            {
                job.Completion.TrySetException(exception);
            }
            finally
            {
                _inflight.TryRemove(job.Key, out _);
            }
        }
    }

    private async Task<WebFontManifest> GenerateOrLoadAsync(
        GenerationJob job,
        CancellationToken cancellationToken)
    {
        if (_durableCache is null)
        {
            return await _engine.GenerateAsync(
                job.Request,
                job.DestinationDirectory,
                cancellationToken).ConfigureAwait(false);
        }

        WebFontManifest? cached = await _durableCache.TryLoadAsync(
            job.Key,
            job.Request,
            job.DestinationDirectory,
            cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        await using IAsyncDisposable lease = await _durableCache.AcquireLeaseAsync(
            job.Key,
            cancellationToken).ConfigureAwait(false);
        cached = await _durableCache.TryLoadAsync(
            job.Key,
            job.Request,
            job.DestinationDirectory,
            cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        WebFontManifest generated = await _engine.GenerateAsync(
            job.Request,
            job.DestinationDirectory,
            cancellationToken).ConfigureAwait(false);
        await _durableCache.StoreAsync(
            job.Key,
            generated,
            job.Request,
            job.DestinationDirectory,
            cancellationToken).ConfigureAwait(false);
        return generated;
    }

    private static string CreateKey(WebFontSubsetRequest request, string destinationDirectory)
    {
        if (request is null
            || request.Face is null
            || string.IsNullOrWhiteSpace(request.Face.FontSourceId)
            || !IsSha256(request.Face.SourceSha256)
            || request.Face.FaceIndex < 0
            || string.IsNullOrWhiteSpace(request.ProfileId)
            || string.IsNullOrWhiteSpace(request.FontFamily)
            || request.Sequences is not { Count: > 0 }
            || request.Sequences.Any(sequence => sequence is null)
            || request.Formats is not { Count: > 0 }
            || request.RequiredBrowserTargets is null
            || string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        string destinationPath = Path.GetFullPath(destinationDirectory);
        if (OperatingSystem.IsWindows())
        {
            destinationPath = destinationPath.ToUpperInvariant();
        }

        var canonical = new StringBuilder();
        AppendCanonicalPart(canonical, request.Face.FontSourceId);
        AppendCanonicalPart(canonical, request.Face.SourceSha256.ToUpperInvariant());
        canonical.Append(request.Face.FaceIndex.ToString(CultureInfo.InvariantCulture)).Append('|');
        AppendCanonicalPart(canonical, request.ProfileId);
        AppendCanonicalPart(canonical, request.FontFamily);
        AppendCanonicalPart(canonical, destinationPath);
        foreach (WebFontFormat format in request.Formats.Distinct().OrderBy(value => value))
        {
            canonical.Append((int)format).Append(',');
        }

        canonical.Append('|');
        foreach (WebFontBrowserTarget target in request.RequiredBrowserTargets.Distinct().OrderBy(value => value))
        {
            canonical.Append((int)target).Append(',');
        }

        canonical.Append('|');
        foreach (string sequence in request.Sequences
                     .Select(sequence => string.Join(
                         ',',
                         sequence.UnicodeScalars.Select(scalar => scalar.ToString("X", CultureInfo.InvariantCulture))))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            canonical.Append(sequence.Length).Append(':').Append(sequence).Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private void TryCacheCompleted(string key, WebFontManifest manifest)
    {
        // 前兩個條件因短路而未執行 Increment，必須先行返回；否則會遞減未曾遞增的
        // 計數器，使 _completedCount 隨每次工作往負值漂移。
        if (_durableCache is not null || _options.MaxMemoryCacheEntries == 0)
        {
            return;
        }

        if (Interlocked.Increment(ref _completedCount) > _options.MaxMemoryCacheEntries)
        {
            Interlocked.Decrement(ref _completedCount);
            return;
        }

        if (!_completed.TryAdd(key, manifest))
        {
            Interlocked.Decrement(ref _completedCount);
        }
    }

    private static void AppendCanonicalPart(StringBuilder canonical, string value)
        => canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('|');

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');

    private sealed record GenerationJob(
        string Key,
        WebFontSubsetRequest Request,
        string DestinationDirectory,
        TaskCompletionSource<WebFontManifest> Completion);
}

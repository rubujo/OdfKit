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
public sealed class WebFontGenerationWorker : IWebFontSubsetEngine, IAsyncDisposable
{
    private readonly IWebFontSubsetEngine _engine;
    private readonly WebFontWorkerOptions _options;
    private readonly Channel<GenerationJob> _queue;
    private readonly ConcurrentDictionary<string, Task<WebFontManifest>> _inflight = new(StringComparer.Ordinal);
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
        if (options.QueueCapacity <= 0 || options.MaxConcurrency <= 0 || options.JobTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
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

    /// <inheritdoc />
    public async Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        string key = CreateKey(request, destinationDirectory);
        Task<WebFontManifest> task = _inflight.GetOrAdd(
            key,
            _ => EnqueueAsync(key, request, destinationDirectory));
        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (task.IsCompleted)
            {
                _inflight.TryRemove(key, out _);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
        try
        {
            await Task.WhenAll(_consumers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
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
            completion.SetException(new InvalidOperationException(
                OdfLocalizer.GetMessage("Err_WebFont_QueueFull")));
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
                WebFontManifest manifest = await _engine.GenerateAsync(
                    job.Request,
                    job.DestinationDirectory,
                    timeout.Token).ConfigureAwait(false);
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

    private static string CreateKey(WebFontSubsetRequest request, string destinationDirectory)
    {
        if (request is null || string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        var canonical = new StringBuilder()
            .Append(request.Face.FontSourceId).Append('|')
            .Append(request.Face.SourceSha256).Append('|')
            .Append(request.Face.FaceIndex).Append('|')
            .Append(request.ProfileId).Append('|')
            .Append(request.FontFamily).Append('|')
            .Append(Path.GetFullPath(destinationDirectory)).Append('|');
        foreach (WebFontFormat format in request.Formats.Distinct().OrderBy(value => value))
        {
            canonical.Append((int)format).Append(',');
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

    private sealed record GenerationJob(
        string Key,
        WebFontSubsetRequest Request,
        string DestinationDirectory,
        TaskCompletionSource<WebFontManifest> Completion);
}

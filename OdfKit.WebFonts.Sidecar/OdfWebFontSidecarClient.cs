using System.IO.Pipes;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Sidecar;

/// <summary>
/// Delegates bounded WebFont operations to an authenticated local sidecar over a named pipe.
/// 透過具名 pipe，將有界 WebFont 作業委派給經驗證的本機 sidecar。
/// </summary>
public sealed class OdfWebFontSidecarClient : IWebFontSubsetEngine, IWebFontTextCoverageFilter
{
    private readonly WebFontSidecarClientOptions _options;
    private readonly string _assetRootPath;

    /// <summary>
    /// Initializes a sidecar client with validated immutable connection settings.
    /// 使用已驗證且不可變的連線設定初始化 sidecar 用戶端。
    /// </summary>
    /// <param name="options">The trusted sidecar connection settings. / 受信任的 sidecar 連線設定。</param>
    public OdfWebFontSidecarClient(WebFontSidecarClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(
            nameof(options),
            OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        ValidateOptions(options);
        _assetRootPath = Path.GetFullPath(options.AssetRootPath);
    }

    /// <summary>
    /// Queries the authenticated sidecar protocol and runtime capabilities.
    /// 查詢經驗證的 sidecar 協定與 Runtime 能力。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The negotiated sidecar health information. / 協商完成的 sidecar 健康資訊。</returns>
    public async Task<WebFontSidecarHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        SidecarFrame response = await SendAsync(
            SidecarOperation.Health,
            SidecarProtocol.CreateHealthRequest(_options.AuthenticationToken),
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return SidecarProtocol.ReadHealth(response.Payload);
    }

    /// <summary>
    /// Generates bounded WebFont subsets through the authenticated sidecar.
    /// 透過經驗證的 sidecar 產生有界的 WebFont 子集。
    /// </summary>
    /// <param name="request">The subset request. / 子集要求。</param>
    /// <param name="destinationDirectory">The trusted destination directory. / 受信任的目的目錄。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The generated manifest. / 產生的 manifest。</returns>
    public async Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }
        EnsureDestination(destinationDirectory);
        SidecarFrame response = await SendAsync(
            SidecarOperation.Generate,
            SidecarProtocol.CreateGenerateRequest(_options.AuthenticationToken, request),
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return SidecarProtocol.ReadManifest(response.Payload);
    }

    /// <summary>
    /// Filters requested text sequences by the glyph coverage reported by the sidecar.
    /// 依 sidecar 回報的 glyph 覆蓋範圍，篩選要求的文字序列。
    /// </summary>
    /// <param name="face">The font face identity. / 字型 face 識別資訊。</param>
    /// <param name="sequences">The requested text sequences. / 要求的文字序列。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The supported contiguous sequences; an empty collection means the face has no requested glyphs. / 支援的連續序列；空集合表示該 face 不含任何要求的 glyph。</returns>
    public async Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
        WebFontFaceIdentity face,
        IReadOnlyList<WebFontTextSequence> sequences,
        CancellationToken cancellationToken = default)
    {
        if (face is null)
        {
            throw new ArgumentNullException(
                nameof(face),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        if (sequences is null)
        {
            throw new ArgumentNullException(
                nameof(sequences),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }
        SidecarFrame response = await SendAsync(
            SidecarOperation.FilterSupportedSequences,
            SidecarProtocol.CreateFilterRequest(_options.AuthenticationToken, face, sequences),
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return SidecarProtocol.ReadSequencesResponse(response.Payload);
    }

    private async Task<SidecarFrame> SendAsync(
        SidecarOperation operation,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (payload.Length > _options.MaxMessageBytes)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(_options.RequestTimeout);
        using var pipe = new NamedPipeClientStream(
            ".",
            _options.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
#if NET10_0_OR_GREATER
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(requestTimeout.Token);
            connectTimeout.CancelAfter(_options.ConnectTimeout);
            await pipe.ConnectAsync(connectTimeout.Token).ConfigureAwait(false);
#else
            int timeoutMilliseconds = checked((int)Math.Ceiling(_options.ConnectTimeout.TotalMilliseconds));
            await Task.Run(() => pipe.Connect(timeoutMilliseconds), requestTimeout.Token).ConfigureAwait(false);
#endif
            await SidecarProtocol.WriteRequestFrameAsync(
                pipe,
                operation,
                payload,
                requestTimeout.Token).ConfigureAwait(false);
            SidecarFrame response = await SidecarProtocol.ReadResponseFrameAsync(
                pipe,
                _options.MaxMessageBytes,
                requestTimeout.Token).ConfigureAwait(false);
            if (response.Operation != operation)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"));
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
            throw new IOException(
                OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"),
                exception);
        }
    }

    private static void EnsureSuccess(SidecarFrame response)
    {
        switch (response.Status)
        {
            case SidecarStatus.Success:
                return;
            case SidecarStatus.InvalidRequest:
            case SidecarStatus.VersionMismatch:
                throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            case SidecarStatus.Unauthorized:
                throw new UnauthorizedAccessException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            case SidecarStatus.Unsupported:
                throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            case SidecarStatus.QueueFull:
                throw new WebFontSidecarQueueFullException();
            case SidecarStatus.Cancelled:
                throw new OperationCanceledException(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"));
            default:
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"));
        }
    }

    private void EnsureDestination(string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory)
            || !string.Equals(
                Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar),
                _assetRootPath.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"),
                nameof(destinationDirectory));
        }
    }

    private static void ValidateOptions(WebFontSidecarClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PipeName)
            || options.PipeName.Length > 128
            || options.PipeName.IndexOfAny(['\\', '/']) >= 0
            || string.IsNullOrWhiteSpace(options.AuthenticationToken)
            || Encoding.UTF8.GetByteCount(options.AuthenticationToken) < 32
            || Encoding.UTF8.GetByteCount(options.AuthenticationToken) > 512
            || string.IsNullOrWhiteSpace(options.AssetRootPath)
            || options.ConnectTimeout <= TimeSpan.Zero
            || options.ConnectTimeout > TimeSpan.FromMinutes(1)
            || options.RequestTimeout <= options.ConnectTimeout
            || options.RequestTimeout > TimeSpan.FromMinutes(30)
            || options.MaxMessageBytes is < 4096 or > 16 * 1024 * 1024)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }
    }
}

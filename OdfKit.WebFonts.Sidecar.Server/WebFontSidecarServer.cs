using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using OdfKit.Compliance;
using OdfKit.WebFonts.Worker;

namespace OdfKit.WebFonts.Sidecar.Server;

internal sealed class WebFontSidecarServer(
    IWebFontSubsetEngine engine,
    WebFontSidecarServerOptions options)
{
    private readonly IWebFontSubsetEngine _engine = engine ?? throw new ArgumentNullException(
        nameof(engine),
        OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
    private readonly WebFontSidecarServerOptions _options = options ?? throw new ArgumentNullException(
        nameof(options),
        OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
    private readonly ConcurrentDictionary<int, Task> _connections = new();
    private int _connectionId;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var slots = new SemaphoreSlim(_options.MaxConnections, _options.MaxConnections);
        while (!cancellationToken.IsCancellationRequested)
        {
            await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
            NamedPipeServerStream pipe = CreatePipe();
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                pipe.Dispose();
                slots.Release();
                throw;
            }

            int id = Interlocked.Increment(ref _connectionId);
            Task connection = HandleConnectionAsync(pipe, cancellationToken);
            _connections[id] = connection;
            _ = connection.ContinueWith(
                completedTask =>
                {
                    _connections.TryRemove(id, out _);
                    slots.Release();
                    pipe.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    public async Task DrainAsync()
    {
        Task[] connections = _connections.Values.ToArray();
        if (connections.Length > 0)
        {
            await Task.WhenAll(connections).ConfigureAwait(false);
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        PipeOptions pipeOptions = PipeOptions.Asynchronous | PipeOptions.WriteThrough;
        if (_options.CurrentUserOnly)
        {
            pipeOptions |= PipeOptions.CurrentUserOnly;
        }

        return new NamedPipeServerStream(
            _options.PipeName,
            PipeDirection.InOut,
            _options.MaxConnections,
            PipeTransmissionMode.Byte,
            pipeOptions,
            inBufferSize: 4096,
            outBufferSize: 4096);
    }

    private async Task HandleConnectionAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectionTimeout.CancelAfter(_options.ConnectionTimeout);
        CancellationToken connectionToken = connectionTimeout.Token;
        SidecarFrame request;
        try
        {
            request = await SidecarProtocol.ReadRequestFrameAsync(
                pipe,
                _options.MaxMessageBytes,
                connectionToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException
                                          or EndOfStreamException
                                          or IOException
                                          or OperationCanceledException)
        {
            return;
        }

        try
        {
            using BinaryReader reader = SidecarProtocol.CreateReader(request.Payload);
            string suppliedToken = SidecarProtocol.ReadToken(reader);
            if (!IsAuthorized(suppliedToken))
            {
                await RespondAsync(
                    pipe,
                    request.Operation,
                    SidecarStatus.Unauthorized,
                    [],
                    connectionToken).ConfigureAwait(false);
                return;
            }

            switch (request.Operation)
            {
                case SidecarOperation.Health:
                    EnsureConsumed(reader);
                    await RespondAsync(
                        pipe,
                        request.Operation,
                        SidecarStatus.Success,
                        SidecarProtocol.CreateHealthResponse(
                            _options.IsWoff2Available,
                            _options.RuntimeIdentifier),
                        connectionToken).ConfigureAwait(false);
                    break;
                case SidecarOperation.Generate:
                    WebFontSubsetRequest generationRequest = SidecarProtocol.ReadRequest(reader);
                    WebFontManifest manifest = await _engine.GenerateAsync(
                        generationRequest,
                        _options.AssetRootPath,
                        connectionToken).ConfigureAwait(false);
                    await RespondAsync(
                        pipe,
                        request.Operation,
                        SidecarStatus.Success,
                        SidecarProtocol.CreateManifestResponse(manifest),
                        connectionToken).ConfigureAwait(false);
                    break;
                case SidecarOperation.FilterSupportedSequences:
                    (WebFontFaceIdentity face, WebFontTextSequence[] sequences) =
                        SidecarProtocol.ReadFilterRequest(reader);
                    IReadOnlyList<WebFontTextSequence> supported =
                        _engine is IWebFontTextCoverageFilter coverageFilter
                            ? await coverageFilter.FilterSupportedSequencesAsync(
                                face,
                                sequences,
                                connectionToken).ConfigureAwait(false)
                            : sequences;
                    await RespondAsync(
                        pipe,
                        request.Operation,
                        SidecarStatus.Success,
                        SidecarProtocol.CreateSequencesResponse(supported),
                        connectionToken).ConfigureAwait(false);
                    break;
                default:
                    await RespondAsync(
                        pipe,
                        request.Operation,
                        SidecarStatus.InvalidRequest,
                        [],
                        connectionToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (WebFontQueueFullException)
        {
            await TryRespondAsync(pipe, request.Operation, SidecarStatus.QueueFull, connectionToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await TryRespondAsync(pipe, request.Operation, SidecarStatus.Cancelled, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            await TryRespondAsync(pipe, request.Operation, SidecarStatus.Unsupported, connectionToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or InvalidDataException
                                          or EndOfStreamException)
        {
            await TryRespondAsync(pipe, request.Operation, SidecarStatus.InvalidRequest, connectionToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            await TryRespondAsync(pipe, request.Operation, SidecarStatus.ServerError, connectionToken)
                .ConfigureAwait(false);
        }
    }

    private bool IsAuthorized(string suppliedToken)
    {
        byte[] supplied = Encoding.UTF8.GetBytes(suppliedToken);
        byte[] expected = Encoding.UTF8.GetBytes(_options.AuthenticationToken);
        return supplied.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private static async Task RespondAsync(
        Stream pipe,
        SidecarOperation operation,
        SidecarStatus status,
        byte[] payload,
        CancellationToken cancellationToken)
        => await SidecarProtocol.WriteResponseFrameAsync(
            pipe,
            operation,
            status,
            payload,
            cancellationToken).ConfigureAwait(false);

    private static async Task TryRespondAsync(
        Stream pipe,
        SidecarOperation operation,
        SidecarStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            await RespondAsync(pipe, operation, status, [], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
                                          or OperationCanceledException
                                          or ObjectDisposedException)
        {
        }
    }

    private static void EnsureConsumed(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }
    }
}

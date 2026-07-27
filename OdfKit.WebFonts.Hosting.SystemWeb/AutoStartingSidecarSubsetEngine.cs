using System.Diagnostics;
using System.Globalization;
using System.Text;
using OdfKit.Compliance;
using OdfKit.WebFonts.Sidecar;

namespace OdfKit.WebFonts.Hosting.SystemWeb;

internal sealed class AutoStartingSidecarSubsetEngine : IWebFontSubsetEngine, IWebFontTextCoverageFilter, IDisposable
{
    private const string ChildTokenEnvironmentVariable = "ODFKIT_WEBFONT_AUTOSTART_TOKEN";
    private static readonly TimeSpan HealthFreshness = TimeSpan.FromSeconds(2);

    private readonly OdfWebFontSidecarClient _client;
    private readonly OdfWebFontSidecarClient _probeClient;
    private readonly AutoStartSidecarOptions _options;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private long _lastHealthyUtcTicks;

    public AutoStartingSidecarSubsetEngine(
        WebFontSidecarClientOptions clientOptions,
        AutoStartSidecarOptions options)
    {
        _client = new OdfWebFontSidecarClient(clientOptions);
        _probeClient = new OdfWebFontSidecarClient(new WebFontSidecarClientOptions
        {
            PipeName = clientOptions.PipeName,
            AuthenticationToken = clientOptions.AuthenticationToken,
            AssetRootPath = clientOptions.AssetRootPath,
            ConnectTimeout = TimeSpan.FromMilliseconds(250),
            RequestTimeout = TimeSpan.FromSeconds(2),
            MaxMessageBytes = clientOptions.MaxMessageBytes
        });
        _options = options;
    }

    public async Task<WebFontManifest> GenerateAsync(
        WebFontSubsetRequest request,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        await EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);
        return await _client.GenerateAsync(request, destinationDirectory, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
        WebFontFaceIdentity face,
        IReadOnlyList<WebFontTextSequence> sequences,
        CancellationToken cancellationToken = default)
    {
        await EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);
        return await _client
            .FilterSupportedSequencesAsync(face, sequences, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        long lastHealthy = Interlocked.Read(ref _lastHealthyUtcTicks);
        if (lastHealthy != 0
            && DateTime.UtcNow - new DateTime(lastHealthy, DateTimeKind.Utc) <= HealthFreshness)
        {
            return;
        }

        if (await TryProbeAsync(cancellationToken).ConfigureAwait(false))
        {
            MarkHealthy();
            return;
        }

        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await TryProbeAsync(cancellationToken).ConfigureAwait(false))
            {
                MarkHealthy();
                return;
            }

            using Process process = StartHost();
            DateTime deadline = DateTime.UtcNow + _options.StartupTimeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    throw ProcessFailed();
                }

                if (await TryProbeAsync(cancellationToken).ConfigureAwait(false))
                {
                    MarkHealthy();
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"));
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task<bool> TryProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _probeClient.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException)
        {
            return false;
        }
    }

    private Process StartHost()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.HostExecutablePath,
            Arguments = CreateArguments(),
            WorkingDirectory = Path.GetDirectoryName(_options.HostExecutablePath)
                ?? AppDomain.CurrentDomain.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.EnvironmentVariables[ChildTokenEnvironmentVariable] = _options.AuthenticationToken;

        try
        {
            return Process.Start(startInfo) ?? throw ProcessFailed();
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            throw new IOException(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"), exception);
        }
    }

    private string CreateArguments()
    {
        var arguments = new StringBuilder();
        AddArgument(arguments, "--pipe");
        AddArgument(arguments, _options.PipeName);
        AddArgument(arguments, "--asset-root");
        AddArgument(arguments, _options.AssetRootPath);
        AddArgument(arguments, "--token-environment-variable");
        AddArgument(arguments, ChildTokenEnvironmentVariable);
        AddArgument(arguments, "--max-message-bytes");
        AddArgument(arguments, _options.MaxMessageBytes.ToString(CultureInfo.InvariantCulture));
        AddArgument(arguments, "--max-unicode-scalars");
        AddArgument(arguments, _options.MaxUnicodeScalars.ToString(CultureInfo.InvariantCulture));
        AddArgument(arguments, "--max-asset-bytes");
        AddArgument(arguments, _options.MaxAssetBytes.ToString(CultureInfo.InvariantCulture));
        AddArgument(arguments, "--job-timeout-seconds");
        AddArgument(arguments, _options.JobTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        if (_options.StopWithApplicationProcess)
        {
            using Process currentProcess = Process.GetCurrentProcess();
            AddArgument(arguments, "--parent-process-id");
            AddArgument(arguments, currentProcess.Id.ToString(CultureInfo.InvariantCulture));
        }

        foreach (KeyValuePair<string, string> source in _options.FontSources.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            AddArgument(arguments, "--font-source");
            AddArgument(arguments, source.Key + "=" + source.Value);
        }

        return arguments.ToString();
    }

    private static void AddArgument(StringBuilder destination, string value)
    {
        if (destination.Length > 0)
        {
            destination.Append(' ');
        }

        destination.Append('"');
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                destination.Append('\\', backslashes * 2 + 1);
                destination.Append('"');
                backslashes = 0;
                continue;
            }

            destination.Append('\\', backslashes);
            backslashes = 0;
            destination.Append(character);
        }

        destination.Append('\\', backslashes * 2);
        destination.Append('"');
    }

    private void MarkHealthy()
        => Interlocked.Exchange(ref _lastHealthyUtcTicks, DateTime.UtcNow.Ticks);

    private static IOException ProcessFailed()
        => new(OdfLocalizer.GetMessage("Err_WebFont_ProcessFailed"));

    public void Dispose()
    {
        _startGate.Dispose();
    }
}

internal sealed class AutoStartSidecarOptions
{
    public string HostExecutablePath { get; init; } = string.Empty;

    public string PipeName { get; init; } = string.Empty;

    public string AuthenticationToken { get; init; } = string.Empty;

    public string AssetRootPath { get; init; } = string.Empty;

    public int MaxMessageBytes { get; init; }

    public int MaxUnicodeScalars { get; init; }

    public long MaxAssetBytes { get; init; }

    public int JobTimeoutSeconds { get; init; }

    public TimeSpan StartupTimeout { get; init; }

    public bool StopWithApplicationProcess { get; init; }

    public IReadOnlyDictionary<string, string> FontSources { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}

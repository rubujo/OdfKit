using System.Runtime.InteropServices;
using System.Text;
using OdfKit.Compliance;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Sidecar.Server;
using OdfKit.WebFonts.Worker;

namespace OdfKit.WebFonts.Sidecar.Host;

internal sealed class HostConfiguration
{
    private const string DefaultTokenEnvironmentVariable = "ODFKIT_WEBFONT_SIDECAR_TOKEN";

    public bool ProbeOnly { get; private init; }

    public WebFontSidecarServerOptions Server { get; private init; } = new();

    public ManagedOpenTypeWebFontEngineOptions Engine { get; private init; } = new();

    public WebFontWorkerOptions Worker { get; private init; } = new();

    public int? ParentProcessId { get; private init; }

    public string ServiceName { get; private init; } = "OdfKit WebFonts Sidecar";

    public static HostConfiguration Parse(string[] args)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var switches = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument is "--probe" or "--allow-cross-user")
            {
                if (!switches.Add(argument))
                {
                    throw ConfigurationInvalid();
                }
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal)
                || !IsKnownValueArgument(argument)
                || index + 1 >= args.Length
                || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw ConfigurationInvalid();
            }

            if (!values.TryGetValue(argument, out List<string>? entries))
            {
                entries = [];
                values.Add(argument, entries);
            }

            entries.Add(args[++index]);
        }

        bool probeOnly = switches.Contains("--probe");
        string pipeName = GetSingle(values, "--pipe", required: !probeOnly);
        string assetRoot = GetSingle(values, "--asset-root", required: !probeOnly);
        string cacheRoot = GetSingle(values, "--cache-root", required: false);
        string tokenEnvironmentVariable = GetSingle(
            values,
            "--token-environment-variable",
            required: false);
        string tokenFile = GetSingle(values, "--token-file", required: false);
        if (string.IsNullOrWhiteSpace(tokenEnvironmentVariable))
        {
            tokenEnvironmentVariable = DefaultTokenEnvironmentVariable;
        }

        string authenticationToken = probeOnly
            ? new string('0', 32)
            : ResolveAuthenticationToken(tokenEnvironmentVariable, tokenFile);
        string serviceName = GetSingle(values, "--service-name", required: false);
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            serviceName = "OdfKit WebFonts Sidecar";
        }
        int maxMessageBytes = GetInt(values, "--max-message-bytes", 4 * 1024 * 1024, 4096, 16 * 1024 * 1024);
        int maxConnections = GetInt(values, "--max-connections", 8, 1, 64);
        int maxConcurrency = GetInt(values, "--max-concurrency", 1, 1, 32);
        int queueCapacity = GetInt(values, "--queue-capacity", 32, 1, 4096);
        int maxUnicodeScalars = GetInt(values, "--max-unicode-scalars", 65536, 1, 65536);
        long maxAssetBytes = GetLong(
            values,
            "--max-asset-bytes",
            64L * 1024 * 1024,
            1,
            256L * 1024 * 1024);
        int timeoutSeconds = GetInt(values, "--job-timeout-seconds", 180, 1, 1800);
        int parentProcessId = GetInt(values, "--parent-process-id", 0, 0, int.MaxValue);

        if (!probeOnly)
        {
            ValidatePipeName(pipeName);
            ValidateToken(authenticationToken);
        }

        var engine = new ManagedOpenTypeWebFontEngineOptions
        {
            MaxUnicodeScalars = maxUnicodeScalars,
            MaxOutputBytes = maxAssetBytes
        };
        foreach (string fontSource in GetMany(values, "--font-source"))
        {
            int separator = fontSource.IndexOf('=');
            if (separator <= 0 || separator == fontSource.Length - 1)
            {
                throw ConfigurationInvalid();
            }

            string id = fontSource[..separator];
            string path = Path.GetFullPath(fontSource[(separator + 1)..]);
            if (engine.FontSources.ContainsKey(id) || !File.Exists(path))
            {
                throw ConfigurationInvalid();
            }

            engine.FontSources.Add(id, path);
        }

        if (!probeOnly && engine.FontSources.Count == 0)
        {
            throw ConfigurationInvalid();
        }

        return new HostConfiguration
        {
            ProbeOnly = probeOnly,
            Server = new WebFontSidecarServerOptions
            {
                PipeName = pipeName,
                AuthenticationToken = authenticationToken,
                AssetRootPath = probeOnly ? string.Empty : Path.GetFullPath(assetRoot),
                MaxMessageBytes = maxMessageBytes,
                MaxConnections = maxConnections,
                ConnectionTimeout = TimeSpan.FromSeconds(timeoutSeconds + 10),
                CurrentUserOnly = !switches.Contains("--allow-cross-user"),
                IsWoff2Available = WebFontRuntimeCapabilities.IsWoff2Available,
                RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier
            },
            Engine = engine,
            Worker = new WebFontWorkerOptions
            {
                DurableCacheDirectory = string.IsNullOrWhiteSpace(cacheRoot)
                    ? null
                    : Path.GetFullPath(cacheRoot),
                QueueCapacity = queueCapacity,
                MaxConcurrency = maxConcurrency,
                JobTimeout = TimeSpan.FromSeconds(timeoutSeconds),
                MaxCachedAssetBytes = maxAssetBytes
            },
            ParentProcessId = parentProcessId == 0 ? null : parentProcessId,
            ServiceName = serviceName
        };
    }

    private static string ResolveAuthenticationToken(
        string tokenEnvironmentVariable,
        string tokenFile)
    {
        string? token = Environment.GetEnvironmentVariable(tokenEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(tokenFile))
        {
            string path = Path.GetFullPath(tokenFile);
            if (!File.Exists(path))
            {
                throw ConfigurationInvalid();
            }

            token = File.ReadAllText(path, Encoding.UTF8).Trim();
        }

        return string.IsNullOrWhiteSpace(token) ? throw ConfigurationInvalid() : token;
    }

    private static string GetSingle(
        Dictionary<string, List<string>> values,
        string name,
        bool required)
    {
        if (!values.TryGetValue(name, out List<string>? entries))
        {
            return required ? throw ConfigurationInvalid() : string.Empty;
        }

        return entries.Count == 1 && !string.IsNullOrWhiteSpace(entries[0])
            ? entries[0]
            : throw ConfigurationInvalid();
    }

    private static List<string> GetMany(
        Dictionary<string, List<string>> values,
        string name)
        => values.TryGetValue(name, out List<string>? entries) ? entries : [];

    private static int GetInt(
        Dictionary<string, List<string>> values,
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        string raw = GetSingle(values, name, required: false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return int.TryParse(
            raw,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out int value)
            && value >= minimum
            && value <= maximum
                ? value
                : throw ConfigurationInvalid();
    }

    private static long GetLong(
        Dictionary<string, List<string>> values,
        string name,
        long defaultValue,
        long minimum,
        long maximum)
    {
        string raw = GetSingle(values, name, required: false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return long.TryParse(
            raw,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out long value)
            && value >= minimum
            && value <= maximum
                ? value
                : throw ConfigurationInvalid();
    }

    private static void ValidatePipeName(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName)
            || pipeName.Length > 128
            || pipeName.IndexOfAny(['\\', '/']) >= 0)
        {
            throw ConfigurationInvalid();
        }
    }

    private static void ValidateToken(string token)
    {
        int byteCount = Encoding.UTF8.GetByteCount(token);
        if (byteCount is < 32 or > 512)
        {
            throw ConfigurationInvalid();
        }
    }

    private static bool IsKnownValueArgument(string argument)
        => argument is "--pipe"
            or "--asset-root"
            or "--cache-root"
            or "--token-environment-variable"
            or "--token-file"
            or "--service-name"
            or "--font-source"
            or "--max-message-bytes"
            or "--max-connections"
            or "--max-concurrency"
            or "--queue-capacity"
            or "--max-unicode-scalars"
            or "--max-asset-bytes"
            or "--job-timeout-seconds"
            or "--parent-process-id";

    private static ArgumentException ConfigurationInvalid()
        => new(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
}

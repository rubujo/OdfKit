namespace OdfKit.WebFonts.Sidecar.Server;

internal sealed class WebFontSidecarServerOptions
{
    public string PipeName { get; init; } = string.Empty;

    public string AuthenticationToken { get; init; } = string.Empty;

    public string AssetRootPath { get; init; } = string.Empty;

    public int MaxMessageBytes { get; init; }

    public int MaxConnections { get; init; }

    public TimeSpan ConnectionTimeout { get; init; }

    public bool CurrentUserOnly { get; init; }

    public bool IsWoff2Available { get; init; }

    public string RuntimeIdentifier { get; init; } = string.Empty;
}

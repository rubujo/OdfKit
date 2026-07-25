using System.Globalization;
using System.Security.Cryptography;
using OdfKit.WebFonts;
using OdfKit.WebFonts.Sidecar;

try
{
    string pipeName = RequireArgument(args, "--pipe");
    string token = GetArgument(args, "--token")
        ?? Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_SIDECAR_TOKEN")
        ?? throw new InvalidOperationException("The sidecar token environment variable is missing.");
    string assetRoot = Path.GetFullPath(RequireArgument(args, "--asset-root"));
    string fontPath = Path.GetFullPath(RequireArgument(args, "--font"));
    string fontSourceId = GetArgument(args, "--font-source-id") ?? "smoke-source";
    string? scalarHex = GetArgument(args, "--scalar");
    string smokeText = scalarHex is null
        ? "OdfKit"
        : char.ConvertFromUtf32(int.Parse(
            scalarHex,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture));
    string sourceSha256 = ComputeSha256(fontPath);

    Directory.CreateDirectory(assetRoot);
    var client = new OdfWebFontSidecarClient(new WebFontSidecarClientOptions
    {
        PipeName = pipeName,
        AuthenticationToken = token,
        AssetRootPath = assetRoot,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        RequestTimeout = TimeSpan.FromMinutes(3)
    });

    WebFontSidecarHealth health = await client.GetHealthAsync();
    Require(health.ProtocolVersion == 1, "The sidecar protocol version is invalid.");
    Require(health.IsWoff2Available, "The sidecar runtime does not provide WOFF2.");

    var face = new WebFontFaceIdentity
    {
        FontSourceId = fontSourceId,
        SourceSha256 = sourceSha256,
        FaceIndex = 0
    };
    IReadOnlyList<WebFontTextSequence> supported = await client.FilterSupportedSequencesAsync(
        face,
        [WebFontTextSequence.Create(smokeText)]);
    Require(supported.Count > 0, "The source font supports none of the smoke sequence.");

    WebFontManifest manifest = await client.GenerateAsync(
        new WebFontSubsetRequest
        {
            Face = face,
            ProfileId = "net48-sidecar-smoke@1",
            FontFamily = "OdfKit Sidecar Smoke",
            Sequences = supported,
            Formats = [WebFontFormat.Woff2]
        },
        assetRoot);
    WebFontAsset asset = manifest.Assets.Single();
    Require(asset.Format == WebFontFormat.Woff2, "The generated asset is not WOFF2.");
    string assetPath = Path.Combine(assetRoot, asset.Sha256, asset.FileName);
    Require(File.Exists(assetPath), "The generated WOFF2 asset is missing.");
    byte[] header = new byte[4];
    using (FileStream stream = File.OpenRead(assetPath))
    {
        Require(stream.Read(header, 0, header.Length) == header.Length, "The generated WOFF2 asset is truncated.");
    }
    Require(header.SequenceEqual(new byte[] { 0x77, 0x4F, 0x46, 0x32 }), "The generated asset has no WOFF2 signature.");
    Require(string.Equals(ComputeSha256(assetPath), asset.Sha256, StringComparison.Ordinal), "The generated asset hash is invalid.");

    Console.WriteLine($"PASS: net48 generated WOFF2 through NativeAOT sidecar ({health.RuntimeIdentifier}).");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.GetType().FullName);
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(exception.StackTrace);
    return 1;
}

static string RequireArgument(string[] values, string name)
{
    string? value = GetArgument(values, name);
    if (value is null)
    {
        throw new ArgumentException($"Missing required argument: {name}");
    }

    return value;
}

static string? GetArgument(string[] values, string name)
{
    int index = Array.IndexOf(values, name);
    return index >= 0 && index + 1 < values.Length && !string.IsNullOrWhiteSpace(values[index + 1])
        ? values[index + 1]
        : null;
}

static string ComputeSha256(string path)
{
    using var stream = File.OpenRead(path);
    using SHA256 algorithm = SHA256.Create();
    return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

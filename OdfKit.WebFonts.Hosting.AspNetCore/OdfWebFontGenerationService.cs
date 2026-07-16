using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OdfKit.Compliance;
using OdfKit.WebFonts.Worker;

namespace OdfKit.WebFonts.Hosting.AspNetCore;

internal sealed class OdfWebFontGenerationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = false,
        MaxDepth = 16,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebFontGenerationWorker _worker;
    private readonly WebFontAssetStore _assetStore;
    private readonly OdfWebFontGenerationOptions _options;
    private readonly string _destinationDirectory;

    public OdfWebFontGenerationService(
        WebFontGenerationWorker worker,
        WebFontAssetStore assetStore,
        IOptions<OdfWebFontOptions> assetOptions,
        IOptions<OdfWebFontGenerationOptions> generationOptions)
    {
        _worker = worker;
        _assetStore = assetStore;
        _options = generationOptions.Value;
        OdfWebFontGenerationOptionValidator.Validate(_options);
        _destinationDirectory = Path.GetFullPath(assetOptions.Value.AssetRootPath);
    }

    public async Task<IResult> GenerateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        if (request.ContentLength > _options.MaxRequestBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        byte[] body;
        try
        {
            body = await ReadBoundedBodyAsync(request.Body, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        OdfWebFontGenerationRequest? generationRequest;
        try
        {
            generationRequest = JsonSerializer.Deserialize<OdfWebFontGenerationRequest>(body, SerializerOptions);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (!TryCreateSubsetRequest(generationRequest, out WebFontSubsetRequest subsetRequest))
        {
            return Results.BadRequest();
        }

        try
        {
            WebFontManifest manifest = await _worker.GenerateAsync(
                subsetRequest,
                _destinationDirectory,
                cancellationToken).ConfigureAwait(false);
            _assetStore.RegisterGeneratedAssets(manifest);
            return Results.Json(manifest);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<byte[]> ReadBoundedBodyAsync(Stream stream, CancellationToken cancellationToken)
    {
        int maximum = _options.MaxRequestBodyBytes;
        using var buffer = new MemoryStream(capacity: Math.Min(maximum, 16 * 1024));
        byte[] chunk = new byte[Math.Min(maximum + 1, 8192)];
        while (true)
        {
            int read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximum)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryCreateSubsetRequest(
        OdfWebFontGenerationRequest? request,
        out WebFontSubsetRequest subsetRequest)
    {
        subsetRequest = null!;
        if (request is null
            || string.IsNullOrWhiteSpace(request.FontSourceId)
            || request.FaceIndex < 0
            || string.IsNullOrWhiteSpace(request.ProfileId)
            || string.IsNullOrWhiteSpace(request.FontFamily)
            || request.FontFamily.Length > 256
            || request.Sequences is not { Count: > 0 }
            || request.Sequences.Count > _options.MaxSequenceCount
            || request.Sequences.Any(string.IsNullOrEmpty)
            || request.Formats is not { Count: > 0 }
            || request.Formats.Count > _options.AllowedFormats.Count
            || request.Formats.Distinct().Count() != request.Formats.Count
            || request.Formats.Any(format => !_options.AllowedFormats.Contains(format))
            || !_options.AllowedProfileIds.Contains(request.ProfileId, StringComparer.Ordinal))
        {
            return false;
        }

        WebFontFaceIdentity? face = _options.AllowedFaces.SingleOrDefault(candidate =>
            string.Equals(candidate.FontSourceId, request.FontSourceId, StringComparison.Ordinal)
            && candidate.FaceIndex == request.FaceIndex);
        if (face is null)
        {
            return false;
        }

        try
        {
            WebFontTextSequence[] sequences = request.Sequences
                .Select(WebFontTextSequence.Create)
                .ToArray();
            int scalarCount = sequences.Sum(sequence => sequence.UnicodeScalars.Count);
            if (scalarCount <= 0
                || scalarCount > _options.MaxUnicodeScalarCount
                || !sequences.SelectMany(sequence => sequence.UnicodeScalars).Any(RequiresGlyph))
            {
                return false;
            }

            subsetRequest = new WebFontSubsetRequest
            {
                Face = new WebFontFaceIdentity
                {
                    FontSourceId = face.FontSourceId,
                    SourceSha256 = face.SourceSha256,
                    FaceIndex = face.FaceIndex
                },
                ProfileId = request.ProfileId,
                FontFamily = request.FontFamily,
                Sequences = sequences,
                Formats = request.Formats.ToArray()
            };
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool RequiresGlyph(int scalar)
        => scalar != 0xFEFF && !Rune.IsControl(new Rune(scalar));
}

internal static class OdfWebFontGenerationOptionValidator
{
    public static void Validate(OdfWebFontGenerationOptions options)
    {
        if (options is null
            || string.IsNullOrWhiteSpace(options.AuthorizationPolicyName)
            || options.AuthorizationPolicyName.Length > 256
            || string.IsNullOrWhiteSpace(options.RateLimiterPolicyName)
            || options.RateLimiterPolicyName.Length > 256
            || options.MaxRequestBodyBytes is <= 0 or > 1024 * 1024
            || options.MaxSequenceCount is <= 0 or > 4096
            || options.MaxUnicodeScalarCount is <= 0 or > 65536
            || options.AllowedFaces.Count is <= 0 or > 256
            || options.AllowedProfileIds.Count is <= 0 or > 256
            || options.AllowedFormats.Count is <= 0 or > 4
            || options.AllowedFormats.Distinct().Count() != options.AllowedFormats.Count
            || options.AllowedFormats.Any(format => !Enum.IsDefined(format))
            || options.AllowedProfileIds.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 256)
            || options.AllowedProfileIds.Distinct(StringComparer.Ordinal).Count() != options.AllowedProfileIds.Count
            || options.AllowedFaces.Any(face => !IsValidFace(face))
            || options.AllowedFaces.GroupBy(
                    face => string.Concat(face.FontSourceId, "\0", face.FaceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }
    }

    private static bool IsValidFace(WebFontFaceIdentity? face)
        => face is not null
            && !string.IsNullOrWhiteSpace(face.FontSourceId)
            && face.FontSourceId.Length <= 256
            && face.FaceIndex >= 0
            && face.SourceSha256 is { Length: 64 }
            && face.SourceSha256.All(character =>
                character is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F');
}

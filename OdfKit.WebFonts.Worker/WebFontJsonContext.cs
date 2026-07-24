using System.Text.Json.Serialization;

namespace OdfKit.WebFonts.Worker;

[JsonSourceGenerationOptions(
    AllowTrailingCommas = false,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    MaxDepth = 32,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Disallow,
    WriteIndented = false)]
[JsonSerializable(typeof(WebFontManifest))]
internal sealed partial class WebFontJsonContext : JsonSerializerContext
{
}

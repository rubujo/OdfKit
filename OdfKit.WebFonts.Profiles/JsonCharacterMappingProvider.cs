using System.Globalization;
using System.Text;
using System.Text.Json;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Profiles;

/// <summary>
/// Loads a versioned country, organization, or tenant mapping profile from bounded JSON.
/// 從有界 JSON 載入版本化的國家、組織或租戶 mapping profile。
/// </summary>
public sealed class JsonCharacterMappingProvider : ICharacterMappingProvider
{
    private readonly IReadOnlyDictionary<string, string> _mappings;
    private readonly int _maximumByteLength;

    private JsonCharacterMappingProvider(string profileId, IReadOnlyDictionary<string, string> mappings, int maximumByteLength)
    {
        ProfileId = profileId;
        _mappings = mappings;
        _maximumByteLength = maximumByteLength;
    }

    /// <inheritdoc />
    public string ProfileId { get; }

    /// <summary>
    /// Loads a profile with strict size and entry-count limits.
    /// 使用嚴格的大小與項目數限制載入 profile。
    /// </summary>
    /// <param name="stream">The trusted JSON profile stream. / 受信任的 JSON profile 串流。</param>
    /// <param name="maxBytes">The maximum JSON byte length. / JSON 位元組長度上限。</param>
    /// <param name="maxEntries">The maximum mapping entry count. / mapping 項目數上限。</param>
    /// <returns>The immutable mapping provider. / 不可變的 mapping provider。</returns>
    public static JsonCharacterMappingProvider Load(Stream stream, int maxBytes, int maxEntries)
    {
        if (stream is null || !stream.CanRead || maxBytes <= 0 || maxEntries <= 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        using var bounded = new MemoryStream();
        var buffer = new byte[81920];
        int total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            bounded.Write(buffer, 0, read);
        }

        MappingProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<MappingProfile>(bounded.ToArray(), new JsonSerializerOptions
            {
                MaxDepth = 16,
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"), exception);
        }

        if (profile is null
            || profile.SchemaVersion != 1
            || string.IsNullOrWhiteSpace(profile.ProfileId)
            || profile.ProfileId.Length > 256
            || profile.Mappings.Count is 0
            || profile.Mappings.Count > maxEntries)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        int maximumByteLength = 0;
        foreach (KeyValuePair<string, string> pair in profile.Mappings)
        {
            string rawKey = pair.Key;
            string text = pair.Value;
            string key = rawKey.ToUpperInvariant();
            if (key.Length is < 2 or > 32
                || key.Length % 2 != 0
                || !key.All(IsHex)
                || string.IsNullOrEmpty(text)
                || mappings.ContainsKey(key))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            _ = WebFontTextSequence.Create(text);
            mappings.Add(key, text);
            maximumByteLength = Math.Max(maximumByteLength, key.Length / 2);
        }

        return new JsonCharacterMappingProvider(profile.ProfileId, mappings, maximumByteLength);
    }

    /// <inheritdoc />
    public string Decode(byte[] source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source), OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        var result = new StringBuilder(source.Length);
        for (int offset = 0; offset < source.Length;)
        {
            if (source[offset] <= 0x7F)
            {
                result.Append((char)source[offset++]);
                continue;
            }

            bool mapped = false;
            int remaining = source.Length - offset;
            for (int length = Math.Min(_maximumByteLength, remaining); length > 0; length--)
            {
                string key = ToHex(source, offset, length);
                if (_mappings.TryGetValue(key, out string? text))
                {
                    result.Append(text);
                    offset += length;
                    mapped = true;
                    break;
                }
            }

            if (!mapped)
            {
                throw new DecoderFallbackException(string.Format(
                    CultureInfo.CurrentCulture,
                    OdfLocalizer.GetMessage("Err_WebFont_UnmappedByte"),
                    offset));
            }
        }

        return result.ToString();
    }

    private static string ToHex(byte[] source, int offset, int length)
    {
        var builder = new StringBuilder(length * 2);
        for (int index = 0; index < length; index++)
        {
            builder.Append(source[offset + index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static bool IsHex(char character)
        => character is >= '0' and <= '9' or >= 'A' and <= 'F';

    private sealed class MappingProfile
    {
        public int SchemaVersion { get; set; }

        public string ProfileId { get; set; } = string.Empty;

        public Dictionary<string, string> Mappings { get; set; } = new(StringComparer.Ordinal);
    }
}

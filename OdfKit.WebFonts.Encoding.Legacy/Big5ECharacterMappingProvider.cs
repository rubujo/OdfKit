using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Encoding.Legacy;

/// <summary>
/// Decodes Big5E by applying an explicit official extension mapping before strict CP950 fallback.
/// 先套用明確的官方 Big5E 擴充對照，再以嚴格 CP950 fallback 解碼 Big5E。
/// </summary>
public sealed class Big5ECharacterMappingProvider(Big5EMapping mapping) : ICharacterMappingProvider
{
    private readonly Big5EMapping _mapping = mapping ?? throw new ArgumentNullException(
        nameof(mapping),
        OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_MappingInvalid"));

    /// <summary>
    /// Gets the versioned Big5E mapping profile identifier.
    /// 取得版本化的 Big5E 對照 profile 識別碼。
    /// </summary>
    public string ProfileId => $"big5e-{_mapping.Version}";

    /// <summary>
    /// Decodes a complete Big5E byte sequence with strict fallback behavior.
    /// 使用嚴格 fallback 行為解碼完整的 Big5E 位元組序列。
    /// </summary>
    /// <param name="source">The Big5E bytes to decode. / 要解碼的 Big5E 位元組。</param>
    /// <returns>The decoded Unicode text. / 解碼後的 Unicode 文字。</returns>
    public string Decode(byte[] source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_SourceRequired"));
        }

        var result = new StringBuilder(source.Length);
        for (int index = 0; index < source.Length; index++)
        {
            byte current = source[index];
            if (current <= 0x7F)
            {
                result.Append((char)current);
                continue;
            }

            if (++index >= source.Length)
            {
                throw new DecoderFallbackException(
                    OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_ByteSequenceInvalid"));
            }

            byte trail = source[index];
            ushort code = (ushort)((current << 8) | trail);
            if (_mapping.TryGetScalar(code, out int scalar))
            {
                result.Append(char.ConvertFromUtf32(scalar));
                continue;
            }

            if (current is >= 0x81 and <= 0xA0 or >= 0xFA and <= 0xFE)
            {
                throw new DecoderFallbackException(
                    OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_ByteSequenceInvalid"));
            }

            result.Append(Big5CharacterMappingProvider.GetStrictEncoding().GetString([current, trail]));
        }

        return result.ToString();
    }
}

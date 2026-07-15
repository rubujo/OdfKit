using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Encoding.Legacy;

/// <summary>
/// Decodes deployment-owned byte sequences into explicitly assigned Unicode private-use scalars.
/// 將部署端管理的位元組序列解碼為明確指派的 Unicode 私用純量值。
/// </summary>
public sealed class PrivateUseCharacterMappingProvider : ICharacterMappingProvider
{
    private readonly IReadOnlyDictionary<string, int> _mappings;

    /// <summary>
    /// Initializes a private mapping with a tenant-scoped profile identifier.
    /// 使用租戶範圍的 profile 識別碼初始化私用對照。
    /// </summary>
    /// <param name="profileId">The tenant and mapping version identifier. / 租戶與 mapping 版本識別碼。</param>
    /// <param name="mappings">Hexadecimal byte sequences mapped to PUA scalars. / 對應至 PUA 純量值的十六進位位元組序列。</param>
    public PrivateUseCharacterMappingProvider(string profileId, IReadOnlyDictionary<string, int> mappings)
    {
        if (string.IsNullOrWhiteSpace(profileId) || mappings is null || mappings.Count == 0)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_MappingInvalid"),
                nameof(profileId));
        }

        ProfileId = profileId;
        _mappings = mappings;
    }

    /// <inheritdoc />
    public string ProfileId { get; }

    /// <inheritdoc />
    public string Decode(byte[] source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_SourceRequired"));
        }

        string key = BitConverter.ToString(source).Replace("-", string.Empty);
        if (!_mappings.TryGetValue(key, out int scalar)
            || scalar is not (>= 0xE000 and <= 0xF8FF)
                and not (>= 0xF0000 and <= 0xFFFFD)
                and not (>= 0x100000 and <= 0x10FFFD))
        {
            throw new DecoderFallbackException(
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_ByteSequenceInvalid"));
        }

        return char.ConvertFromUtf32(scalar);
    }
}

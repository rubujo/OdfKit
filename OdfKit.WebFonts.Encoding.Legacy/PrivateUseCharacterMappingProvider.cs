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

        // 以大小寫不敏感的序數比較器複製對照：Decode 以 BitConverter.ToString 產生大寫十六進位鍵，
        // 若呼叫端提供小寫鍵，區分大小寫的字典會靜默失配。防禦性複製亦避免呼叫端後續修改影響本執行個體。
        var normalized = new Dictionary<string, int>(mappings.Count, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, int> pair in mappings)
        {
            normalized[pair.Key] = pair.Value;
        }

        ProfileId = profileId;
        _mappings = normalized;
    }

    /// <summary>
    /// Gets the tenant-scoped private mapping profile identifier.
    /// 取得租戶範圍的私用對照 profile 識別碼。
    /// </summary>
    public string ProfileId { get; }

    /// <summary>
    /// Decodes a byte sequence through the explicit private-use mapping.
    /// 透過明確的私用對照解碼位元組序列。
    /// </summary>
    /// <param name="source">The mapped byte sequence. / 已建立對照的位元組序列。</param>
    /// <returns>The mapped private-use Unicode scalar. / 對照後的 Unicode 私用純量值。</returns>
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

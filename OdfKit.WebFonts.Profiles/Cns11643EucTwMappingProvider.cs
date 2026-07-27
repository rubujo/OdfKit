using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.WebFonts.Profiles;

/// <summary>
/// Strictly decodes EUC-TW bytes with version-pinned official CNS 11643 Unicode mappings.
/// 使用版本鎖定的官方 CNS 11643 Unicode 對照，嚴格解碼 EUC-TW 位元組。
/// </summary>
/// <remarks>
/// The provider contains no third-party font bytes. Callers obtain the pinned mapping archive through the documented build process and supply its Unicode tables.
/// 此 provider 不包含第三方字型位元組。呼叫端須依文件化建置流程取得鎖定的對照表封存檔，並提供其中的 Unicode 對照表。
/// </remarks>
public sealed class Cns11643EucTwMappingProvider : ITraceableCharacterMappingProvider
{
    /// <summary>
    /// Gets the currently verified official data version.
    /// 取得目前已驗證的官方資料版本。
    /// </summary>
    public const string VerifiedDataVersion = "2026-05-05";

    /// <summary>
    /// Gets the SHA-256 digest of the verified official mapping archive.
    /// 取得已驗證官方對照表封存檔的 SHA-256 摘要。
    /// </summary>
    public const string VerifiedArchiveSha256 = "f59dacc4dbdef334d7a887c3da671af02778e2c80adb2a7fd1053f64dbf9e659";

    /// <summary>
    /// Gets the official mapping archive URI.
    /// 取得官方對照表封存檔 URI。
    /// </summary>
    public const string OfficialSourceUri = "https://www.cns11643.gov.tw/opendata/MapingTables.zip";

    private readonly IReadOnlyDictionary<string, int> _mappings;

    private Cns11643EucTwMappingProvider(IReadOnlyDictionary<string, int> mappings)
    {
        _mappings = mappings;
    }

    /// <summary>
    /// Gets the versioned CNS 11643 EUC-TW profile identifier.
    /// 取得版本化的 CNS 11643 EUC-TW profile 識別碼。
    /// </summary>
    public string ProfileId => $"cns11643-euc-tw-{VerifiedDataVersion}";

    /// <summary>
    /// Gets the pinned official data version.
    /// 取得鎖定的官方資料版本。
    /// </summary>
    public string DataVersion => VerifiedDataVersion;

    /// <summary>
    /// Gets the official source URI.
    /// 取得官方來源 URI。
    /// </summary>
    public string SourceUri => OfficialSourceUri;

    /// <summary>
    /// Gets the pinned SHA-256 digest of the source archive.
    /// 取得來源封存檔鎖定的 SHA-256 摘要。
    /// </summary>
    public string SourceSha256 => VerifiedArchiveSha256;

    /// <summary>
    /// Gets the source data license identifier.
    /// 取得來源資料的授權識別碼。
    /// </summary>
    public string LicenseId => "OGDL-Taiwan-1.0";

    /// <summary>
    /// Gets the required source attribution.
    /// 取得必要的來源標示。
    /// </summary>
    public string Attribution => "數位發展部，CNS11643 中文標準交換碼全字庫網站，https://www.cns11643.gov.tw。";

    /// <summary>
    /// Loads and merges official tab-delimited CNS-to-Unicode tables with a hard entry limit.
    /// 載入並合併官方跳格分隔的 CNS 至 Unicode 對照表，且套用項目數硬性上限。
    /// </summary>
    /// <param name="readers">The official Unicode mapping table readers. / 官方 Unicode 對照表 reader。</param>
    /// <param name="maxEntries">The maximum merged mapping count. / 合併後的 mapping 項目數上限。</param>
    /// <returns>A traceable strict EUC-TW mapping provider. / 可追溯的嚴格 EUC-TW mapping provider。</returns>
    public static Cns11643EucTwMappingProvider Load(IEnumerable<TextReader> readers, int maxEntries)
    {
        if (readers is null || maxEntries <= 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        var merged = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (TextReader reader in readers)
        {
            if (reader is null)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            foreach (KeyValuePair<string, int> pair in OdfCns11643MappingTable.Parse(reader))
            {
                if (pair.Value is < 0 or > 0x10FFFF
                    || pair.Value is >= 0xD800 and <= 0xDFFF
                    || (merged.TryGetValue(pair.Key, out int existing) && existing != pair.Value))
                {
                    throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
                }

                merged[pair.Key] = pair.Value;
                if (merged.Count > maxEntries)
                {
                    throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
                }
            }
        }

        if (merged.Count == 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return new Cns11643EucTwMappingProvider(merged);
    }

    /// <summary>
    /// Decodes a complete EUC-TW sequence through the pinned CNS 11643 mapping.
    /// 透過鎖定的 CNS 11643 對照解碼完整 EUC-TW 序列。
    /// </summary>
    /// <param name="source">The EUC-TW bytes to decode. / 要解碼的 EUC-TW 位元組。</param>
    /// <returns>The decoded Unicode text. / 解碼後的 Unicode 文字。</returns>
    public string Decode(byte[] source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source), OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        var result = new StringBuilder(source.Length);
        for (int offset = 0; offset < source.Length;)
        {
            byte first = source[offset];
            if (first <= 0x7F)
            {
                result.Append((char)first);
                offset++;
                continue;
            }

            int plane;
            byte row;
            byte cell;
            int length;
            if (first is >= 0xA1 and <= 0xFE && offset + 1 < source.Length)
            {
                plane = 1;
                row = first;
                cell = source[offset + 1];
                length = 2;
            }
            else if (first == 0x8E
                     && offset + 3 < source.Length
                     && source[offset + 1] is >= 0xA2 and <= 0xB0)
            {
                plane = source[offset + 1] - 0xA0;
                row = source[offset + 2];
                cell = source[offset + 3];
                length = 4;
            }
            else
            {
                throw Unmapped(offset);
            }

            if (row is < 0xA1 or > 0xFE || cell is < 0xA1 or > 0xFE)
            {
                throw Unmapped(offset);
            }

            string key = string.Format(
                CultureInfo.InvariantCulture,
                "{0}-{1:X2}{2:X2}",
                plane,
                row - 0x80,
                cell - 0x80);
            if (!_mappings.TryGetValue(key, out int scalar))
            {
                throw Unmapped(offset);
            }

            result.Append(char.ConvertFromUtf32(scalar));
            offset += length;
        }

        return result.ToString();
    }

    [SuppressMessage(
        "Performance",
        "CA1863:Use 'CompositeFormat'",
        Justification = "The localized format string follows the current UI culture and cannot be cached as one process-wide CompositeFormat.")]
    private static DecoderFallbackException Unmapped(int offset)
        => new(string.Format(
            CultureInfo.CurrentCulture,
            OdfLocalizer.GetMessage("Err_WebFont_UnmappedByte"),
            offset));
}

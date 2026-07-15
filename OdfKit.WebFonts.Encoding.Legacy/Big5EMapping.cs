using System.Globalization;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Encoding.Legacy;

/// <summary>
/// Stores an explicit Big5E byte-pair to Unicode scalar mapping.
/// 儲存明確的 Big5E 位元組配對至 Unicode 純量值對照。
/// </summary>
public sealed class Big5EMapping
{
    private readonly IReadOnlyDictionary<ushort, int> _entries;

    private Big5EMapping(string version, IReadOnlyDictionary<ushort, int> entries)
    {
        Version = version;
        _entries = entries;
    }

    /// <summary>
    /// Gets the caller-supplied official mapping version.
    /// 取得呼叫端提供的官方 mapping 版本。
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Loads a direct mapping whose data rows contain Big5E hex, a tab, and a Unicode scalar hex value.
    /// 載入資料列包含 Big5E 十六進位碼、定位字元及 Unicode 純量值十六進位碼的直接對照表。
    /// </summary>
    /// <param name="reader">The trusted mapping reader. / 受信任的對照表 reader。</param>
    /// <param name="version">The mapping dataset version. / 對照資料集版本。</param>
    /// <returns>The immutable mapping. / 不可變對照表。</returns>
    public static Big5EMapping Load(TextReader reader, string version)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(
                nameof(reader),
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_MappingInvalid"));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_MappingInvalid"),
                nameof(version));
        }

        var entries = new Dictionary<ushort, int>();
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            string[] fields = trimmed.Split('\t');
            if (fields.Length != 2
                || !ushort.TryParse(fields[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code)
                || !int.TryParse(fields[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int scalar)
                || !IsUnicodeScalar(scalar)
                || entries.ContainsKey(code))
            {
                throw new InvalidDataException(string.Format(
                    CultureInfo.CurrentCulture,
                    OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_MappingLineInvalid"),
                    lineNumber));
            }

            entries.Add(code, scalar);
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_MappingInvalid"));
        }

        return new Big5EMapping(version, entries);
    }

    internal bool TryGetScalar(ushort code, out int scalar) => _entries.TryGetValue(code, out scalar);

    private static bool IsUnicodeScalar(int value)
        => value is >= 0 and <= 0x10FFFF && value is not (>= 0xD800 and <= 0xDFFF);
}

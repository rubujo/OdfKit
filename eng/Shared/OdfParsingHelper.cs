using System.Globalization;

namespace OdfKit.Internal;

/// <summary>
/// Provides cross-target span-aware parsing operations.
/// 提供跨目標且支援範圍的剖析操作。
/// </summary>
internal static class OdfParsingHelper
{
    /// <summary>Parses an invariant integer segment. / 剖析不因文化特性而異的整數區段。</summary>
    public static int ParseInvariantInt32(string value, int startIndex, int length)
    {
#if NET6_0_OR_GREATER
        return int.Parse(value.AsSpan(startIndex, length), CultureInfo.InvariantCulture);
#else
        return int.Parse(value.Substring(startIndex, length), CultureInfo.InvariantCulture);
#endif
    }

    /// <summary>Parses an invariant hexadecimal integer segment. / 剖析不因文化特性而異的十六進位整數區段。</summary>
    public static int ParseInvariantHexInt32(string value, int startIndex, int length)
    {
#if NET6_0_OR_GREATER
        return int.Parse(value.AsSpan(startIndex, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#else
        return int.Parse(value.Substring(startIndex, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#endif
    }

    /// <summary>Tries to parse an invariant integer segment. / 嘗試剖析不因文化特性而異的整數區段。</summary>
    public static bool TryParseInvariantInt32(string value, int startIndex, int length, out int result)
    {
#if NET6_0_OR_GREATER
        return int.TryParse(value.AsSpan(startIndex, length), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
#else
        return int.TryParse(value.Substring(startIndex, length), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
#endif
    }

    /// <summary>Tries to parse an invariant floating-point segment. / 嘗試剖析不因文化特性而異的浮點數區段。</summary>
    public static bool TryParseInvariantDouble(string value, int startIndex, int length, out double result)
    {
#if NET6_0_OR_GREATER
        return double.TryParse(value.AsSpan(startIndex, length), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#else
        return double.TryParse(value.Substring(startIndex, length), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#endif
    }

    /// <summary>Tries to parse a decimal suffix. / 嘗試剖析十進位後綴。</summary>
    public static bool TryParseInt32Suffix(string value, int startIndex, out int result)
    {
#if NET6_0_OR_GREATER
        return int.TryParse(value.AsSpan(startIndex), out result);
#else
        return int.TryParse(value.Substring(startIndex), out result);
#endif
    }

    /// <summary>Tries to parse an invariant floating-point value excluding a suffix. / 嘗試剖析排除後綴的不變浮點數值。</summary>
    public static bool TryParseInvariantDoubleWithoutSuffix(string value, int suffixLength, out double result)
    {
#if NET6_0_OR_GREATER
        return double.TryParse(value.AsSpan(0, value.Length - suffixLength), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#else
        return double.TryParse(value.Substring(0, value.Length - suffixLength), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#endif
    }
}

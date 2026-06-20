using System.Globalization;
using System.Numerics;

namespace OdfKit.Core;

/// <summary>
/// 十六進位序號正規化工具（內部協作者）。
/// </summary>
internal static class OdfSignatureDerCodec
{
    /// <summary>
    /// 將十六進位序號字串正規化為大寫、去除前導零的形式（空字串或全零正規化為 <c>"0"</c>）。
    /// </summary>
    internal static string NormalizeHexSerial(string serialHex)
    {
        if (string.IsNullOrWhiteSpace(serialHex))
            return "";
        if (BigInteger.TryParse("0" + serialHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bigInt))
        {
            if (bigInt.IsZero)
                return "0";
            return bigInt.ToString("X").TrimStart('0');
        }
        string normalized = serialHex.TrimStart('0').ToUpperInvariant();
        return normalized == "" ? "0" : normalized;
    }
}

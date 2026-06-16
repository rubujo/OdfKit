using System;
using System.Text.RegularExpressions;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>cellAddress</c> 的儲存格位址 lexical form。
/// </summary>
public readonly struct OdfCellAddressReference : IEquatable<OdfCellAddressReference>
{
    private static readonly Regex CellAddressRegex = new(
        @"^(\$?([^\. ']+|'([^']|'')+'))?\.\$?[A-Z]+\$?[0-9]+$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 以儲存格位址 lexical form 建立 <see cref="OdfCellAddressReference"/>。
    /// </summary>
    /// <param name="value">儲存格位址，例如 <c>.A1</c>、<c>$Sheet1.$B$2</c> 或 <c>'My Sheet'.C3</c>。</param>
    /// <exception cref="ArgumentException">當儲存格位址不符合 ODF <c>cellAddress</c> 格式時擲回。</exception>
    public OdfCellAddressReference(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("儲存格位址必須符合 ODF cellAddress 格式。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始儲存格位址字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析儲存格位址。
    /// </summary>
    /// <param name="value">儲存格位址字串。</param>
    /// <param name="cellAddress">成功時傳回解析後的儲存格位址。</param>
    /// <returns>若字串符合 ODF <c>cellAddress</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfCellAddressReference cellAddress)
    {
        if (IsValid(value))
        {
            cellAddress = new OdfCellAddressReference(value!);
            return true;
        }

        cellAddress = default;
        return false;
    }

    /// <summary>
    /// 傳回原始儲存格位址字串。
    /// </summary>
    /// <returns>儲存格位址字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個儲存格位址。
    /// </summary>
    /// <param name="other">要比較的儲存格位址。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfCellAddressReference other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfCellAddressReference other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個儲存格位址是否相等。
    /// </summary>
    /// <param name="left">左側儲存格位址。</param>
    /// <param name="right">右側儲存格位址。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfCellAddressReference left, OdfCellAddressReference right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個儲存格位址是否不相等。
    /// </summary>
    /// <param name="left">左側儲存格位址。</param>
    /// <param name="right">右側儲存格位址。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfCellAddressReference left, OdfCellAddressReference right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        return !ContainsControlCharacter(value) && CellAddressRegex.IsMatch(value!);
    }

    private static bool ContainsControlCharacter(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        foreach (char ch in value!)
        {
            if (char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>cellRangeAddressList</c> 的儲存格範圍位址清單 lexical form。
/// </summary>
public readonly struct OdfCellRangeAddressList : IEquatable<OdfCellRangeAddressList>
{
    /// <summary>
    /// 以儲存格範圍位址清單 lexical form 建立 <see cref="OdfCellRangeAddressList"/>。
    /// </summary>
    /// <param name="value">以空白分隔的儲存格範圍位址清單。</param>
    /// <exception cref="ArgumentException">當清單不符合 ODF <c>cellRangeAddressList</c> 格式時擲回。</exception>
    public OdfCellRangeAddressList(string value)
    {
        if (!TryParseItems(value, out OdfCellRangeAddress[] ranges))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfCellRangeAddressList_CellRangeAddressList"), nameof(value));
        }

        Value = value;
        Ranges = ranges;
    }

    /// <summary>
    /// 以儲存格範圍位址集合建立 <see cref="OdfCellRangeAddressList"/>。
    /// </summary>
    /// <param name="ranges">要寫入的儲存格範圍位址集合。</param>
    /// <exception cref="ArgumentException">當集合未包含任何位址時擲回。</exception>
    public OdfCellRangeAddressList(IEnumerable<OdfCellRangeAddress> ranges)
    {
        OdfCellRangeAddress[] items = ranges?.ToArray() ?? [];
        if (items.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfCellRangeAddressList_ListCannotBeEmpty"), nameof(ranges));
        }

        Value = string.Join(" ", items.Select(item => item.Value));
        Ranges = items;
    }

    /// <summary>
    /// 取得原始儲存格範圍位址清單字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得解析後的儲存格範圍位址集合。
    /// </summary>
    public IReadOnlyList<OdfCellRangeAddress> Ranges { get; }

    /// <summary>
    /// 嘗試解析儲存格範圍位址清單。
    /// </summary>
    /// <param name="value">儲存格範圍位址清單字串。</param>
    /// <param name="cellRangeAddressList">成功時傳回解析後的儲存格範圍位址清單。</param>
    /// <returns>若字串符合 ODF <c>cellRangeAddressList</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfCellRangeAddressList cellRangeAddressList)
    {
        if (TryParseItems(value, out OdfCellRangeAddress[] ranges))
        {
            cellRangeAddressList = new OdfCellRangeAddressList(value!, ranges);
            return true;
        }

        cellRangeAddressList = default;
        return false;
    }

    /// <summary>
    /// 傳回原始儲存格範圍位址清單字串。
    /// </summary>
    /// <returns>儲存格範圍位址清單字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個儲存格範圍位址清單。
    /// </summary>
    /// <param name="other">要比較的儲存格範圍位址清單。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfCellRangeAddressList other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfCellRangeAddressList other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個儲存格範圍位址清單是否相等。
    /// </summary>
    /// <param name="left">左側儲存格範圍位址清單。</param>
    /// <param name="right">右側儲存格範圍位址清單。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfCellRangeAddressList left, OdfCellRangeAddressList right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個儲存格範圍位址清單是否不相等。
    /// </summary>
    /// <param name="left">左側儲存格範圍位址清單。</param>
    /// <param name="right">右側儲存格範圍位址清單。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfCellRangeAddressList left, OdfCellRangeAddressList right) => !left.Equals(right);

    private OdfCellRangeAddressList(string value, OdfCellRangeAddress[] ranges)
    {
        Value = value;
        Ranges = ranges;
    }

    private static bool TryParseItems(string? value, out OdfCellRangeAddress[] ranges)
    {
        ranges = [];
        if (value is null || value.Length == 0 || ContainsControlCharacter(value))
        {
            return false;
        }

        List<OdfCellRangeAddress> parsed = [];
        int itemStart = -1;
        bool inQuotedName = false;

        for (int index = 0; index <= value.Length; index++)
        {
            char ch = index < value.Length ? value[index] : ' ';
            if (index < value.Length && ch == '\'')
            {
                if (inQuotedName && index + 1 < value.Length && value[index + 1] == '\'')
                {
                    index++;
                    continue;
                }

                inQuotedName = !inQuotedName;
            }

            bool isSeparator = index == value.Length || (ch == ' ' && !inQuotedName);
            if (!isSeparator)
            {
                if (itemStart < 0)
                {
                    itemStart = index;
                }

                continue;
            }

            if (itemStart < 0)
            {
                continue;
            }

            string item = value.Substring(itemStart, index - itemStart);
            if (!OdfCellRangeAddress.TryParse(item, out OdfCellRangeAddress range))
            {
                return false;
            }

            parsed.Add(range);
            itemStart = -1;
        }

        if (inQuotedName || parsed.Count == 0)
        {
            return false;
        }

        ranges = parsed.ToArray();
        return true;
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (char ch in value)
        {
            if (char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }
}

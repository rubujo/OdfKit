using System;
using System.Collections.Generic;
using System.Linq;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>styleNameRefs</c> 的樣式名稱參照清單 lexical form。
/// </summary>
public readonly struct OdfStyleNameList : IEquatable<OdfStyleNameList>
{
    /// <summary>
    /// 以樣式名稱參照清單 lexical form 建立 <see cref="OdfStyleNameList"/>。
    /// </summary>
    /// <param name="value">以空白分隔的樣式名稱參照清單</param>
    /// <exception cref="ArgumentException">當清單不符合 ODF <c>styleNameRefs</c> 格式時擲回</exception>
    public OdfStyleNameList(string value)
    {
        if (!TryParseItems(value, out OdfStyleName[] styleNames))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfStyleNameList_StyleNameReferenceLists"), nameof(value));
        }

        Value = value;
        StyleNames = styleNames;
    }

    /// <summary>
    /// 以樣式名稱集合建立 <see cref="OdfStyleNameList"/>。
    /// </summary>
    /// <param name="styleNames">要寫入的樣式名稱集合</param>
    public OdfStyleNameList(IEnumerable<OdfStyleName> styleNames)
    {
        OdfStyleName[] items = styleNames?.ToArray() ?? [];
        Value = string.Join(" ", items.Select(item => item.Value));
        StyleNames = items;
    }

    /// <summary>
    /// 取得原始樣式名稱參照清單字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得解析後的樣式名稱集合。
    /// </summary>
    public IReadOnlyList<OdfStyleName> StyleNames { get; }

    /// <summary>
    /// 嘗試解析樣式名稱參照清單。
    /// </summary>
    /// <param name="value">樣式名稱參照清單字串</param>
    /// <param name="styleNameList">成功時傳回解析後的樣式名稱參照清單</param>
    /// <returns>若字串符合 ODF <c>styleNameRefs</c> 格式則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfStyleNameList styleNameList)
    {
        if (TryParseItems(value, out OdfStyleName[] styleNames))
        {
            styleNameList = new OdfStyleNameList(value!, styleNames);
            return true;
        }

        styleNameList = default;
        return false;
    }

    /// <summary>
    /// 傳回原始樣式名稱參照清單字串。
    /// </summary>
    /// <returns>樣式名稱參照清單字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個樣式名稱參照清單。
    /// </summary>
    /// <param name="other">要比較的樣式名稱參照清單</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfStyleNameList other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfStyleNameList other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個樣式名稱參照清單是否相等。
    /// </summary>
    /// <param name="left">左側樣式名稱參照清單</param>
    /// <param name="right">右側樣式名稱參照清單</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfStyleNameList left, OdfStyleNameList right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個樣式名稱參照清單是否不相等。
    /// </summary>
    /// <param name="left">左側樣式名稱參照清單</param>
    /// <param name="right">右側樣式名稱參照清單</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfStyleNameList left, OdfStyleNameList right) => !left.Equals(right);

    private OdfStyleNameList(string value, OdfStyleName[] styleNames)
    {
        Value = value;
        StyleNames = styleNames;
    }

    private static bool TryParseItems(string? value, out OdfStyleName[] styleNames)
    {
        styleNames = [];
        if (value is null)
        {
            return false;
        }

        if (value.Length == 0)
        {
            return true;
        }

        List<OdfStyleName> parsed = [];
        foreach (string item in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!OdfStyleName.TryParse(item, out OdfStyleName styleName))
            {
                return false;
            }

            parsed.Add(styleName);
        }

        styleNames = parsed.ToArray();
        return true;
    }
}

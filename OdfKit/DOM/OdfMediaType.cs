using System;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF 中使用的 MIME 類型字串。
/// </summary>
public readonly struct OdfMediaType : IEquatable<OdfMediaType>
{
    /// <summary>
    /// 以指定 MIME 類型字串建立 <see cref="OdfMediaType"/>。
    /// </summary>
    /// <param name="value">MIME 類型字串，例如 <c>application/vnd.oasis.opendocument.text</c></param>
    /// <exception cref="ArgumentException">當字串不是可接受的 MIME 類型格式時擲回</exception>
    public OdfMediaType(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMediaType_MimeTypeContainType"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始 MIME 類型字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析 MIME 類型字串。
    /// </summary>
    /// <param name="value">MIME 類型字串</param>
    /// <param name="mediaType">成功時傳回解析後的 MIME 類型</param>
    /// <returns>若字串是可接受的 MIME 類型格式則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfMediaType mediaType)
    {
        if (IsValid(value))
        {
            mediaType = new OdfMediaType(value!);
            return true;
        }

        mediaType = default;
        return false;
    }

    /// <summary>
    /// 傳回 MIME 類型字串。
    /// </summary>
    /// <returns>MIME 類型字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個 MIME 類型。
    /// </summary>
    /// <param name="other">要比較的 MIME 類型</param>
    /// <returns>若值相等則為 <see langword="true"/></returns>
    public bool Equals(OdfMediaType other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfMediaType other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個 MIME 類型是否相等。
    /// </summary>
    /// <param name="left">左側 MIME 類型</param>
    /// <param name="right">右側 MIME 類型</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfMediaType left, OdfMediaType right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個 MIME 類型是否不相等。
    /// </summary>
    /// <param name="left">左側 MIME 類型</param>
    /// <param name="right">右側 MIME 類型</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfMediaType left, OdfMediaType right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int slashIndex = value!.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == value.Length - 1 || value.IndexOf('/', slashIndex + 1) >= 0)
        {
            return false;
        }

        foreach (char ch in value)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }
}

using System;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>anyIRI</c> 的 IRI 參照 lexical form。
/// </summary>
public readonly struct OdfIriReference : IEquatable<OdfIriReference>
{
    /// <summary>
    /// 以 IRI 參照 lexical form 建立 <see cref="OdfIriReference"/>。
    /// </summary>
    /// <param name="value">IRI 參照字串，可為絕對 IRI、相對 IRI、片段或空參照。</param>
    /// <exception cref="ArgumentException">當 IRI 參照包含控制字元時擲回。</exception>
    public OdfIriReference(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("IRI 參照不可包含控制字元。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始 IRI 參照字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析 IRI 參照字串。
    /// </summary>
    /// <param name="value">IRI 參照字串。</param>
    /// <param name="iriReference">成功時傳回解析後的 IRI 參照。</param>
    /// <returns>若字串不含控制字元則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfIriReference iriReference)
    {
        if (IsValid(value))
        {
            iriReference = new OdfIriReference(value!);
            return true;
        }

        iriReference = default;
        return false;
    }

    /// <summary>
    /// 傳回原始 IRI 參照字串。
    /// </summary>
    /// <returns>IRI 參照字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個 IRI 參照。
    /// </summary>
    /// <param name="other">要比較的 IRI 參照。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfIriReference other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfIriReference other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個 IRI 參照是否相等。
    /// </summary>
    /// <param name="left">左側 IRI 參照。</param>
    /// <param name="right">右側 IRI 參照。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfIriReference left, OdfIriReference right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個 IRI 參照是否不相等。
    /// </summary>
    /// <param name="left">左側 IRI 參照。</param>
    /// <param name="right">右側 IRI 參照。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfIriReference left, OdfIriReference right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null)
        {
            return false;
        }

        foreach (char ch in value)
        {
            if (char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }
}

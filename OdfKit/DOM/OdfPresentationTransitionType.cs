using System;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>presentation:transition-type</c> 的轉場類型 token。
/// </summary>
public readonly struct OdfPresentationTransitionType : IEquatable<OdfPresentationTransitionType>
{
    /// <summary>
    /// Gets the Manual value.
    /// 手動轉場 token。
    /// </summary>
    public static OdfPresentationTransitionType Manual { get; } = new("manual");

    /// <summary>
    /// Gets the Automatic value.
    /// 自動轉場 token。
    /// </summary>
    public static OdfPresentationTransitionType Automatic { get; } = new("automatic");

    /// <summary>
    /// Gets the SemiAutomatic value.
    /// 半自動轉場 token。
    /// </summary>
    public static OdfPresentationTransitionType SemiAutomatic { get; } = new("semi-automatic");

    /// <summary>
    /// Executes the Custom operation.
    /// 建立自訂轉場類型 token。
    /// </summary>
    /// <param name="value">自訂轉場 token</param>
    /// <returns>對應的轉場類型</returns>
    public static OdfPresentationTransitionType Custom(string value) => new(value);

    /// <summary>
    /// Gets the Value value.
    /// 取得轉場類型 token 字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Executes the OdfPresentationTransitionType operation.
    /// 將字串隱式轉換為轉場類型。
    /// </summary>
    /// <param name="value">轉場 token</param>
    public static implicit operator OdfPresentationTransitionType(string value) => new(value);

    /// <summary>
    /// Executes the string operation.
    /// 將轉場類型隱式轉換為字串 token。
    /// </summary>
    /// <param name="value">轉場類型</param>
    public static implicit operator string(OdfPresentationTransitionType value) => value.Value;

    /// <summary>
    /// Executes the ToString operation.
    /// 執行 ToString 作業。
    /// </summary>
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Executes the Equals operation.
    /// 執行 Equals 作業。
    /// </summary>
    /// <inheritdoc />
    public bool Equals(OdfPresentationTransitionType other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Executes the Equals operation.
    /// 執行 Equals 作業。
    /// </summary>
    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is OdfPresentationTransitionType other && Equals(other);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// 執行 GetHashCode 作業。
    /// </summary>
    /// <inheritdoc />
    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>
    /// Executes the member operation.
    /// 判斷兩個轉場類型是否相等。
    /// </summary>
    public static bool operator ==(OdfPresentationTransitionType left, OdfPresentationTransitionType right)
        => left.Equals(right);

    /// <summary>
    /// Executes the member operation.
    /// 判斷兩個轉場類型是否不相等。
    /// </summary>
    public static bool operator !=(OdfPresentationTransitionType left, OdfPresentationTransitionType right)
        => !left.Equals(right);

    private OdfPresentationTransitionType(string value)
    {
        Value = value ?? string.Empty;
    }
}

using System;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>scriptCode</c> 的文字系統代碼 lexical form。
/// </summary>
public readonly struct OdfScriptCode : IEquatable<OdfScriptCode>
{
    /// <summary>
    /// 以文字系統代碼 lexical form 建立 <see cref="OdfScriptCode"/>。
    /// </summary>
    /// <param name="value">文字系統代碼，例如 <c>Latn</c> 或 <c>Hant</c>。</param>
    /// <exception cref="ArgumentException">當文字系統代碼不符合 ODF <c>scriptCode</c> 格式時擲回。</exception>
    public OdfScriptCode(string value)
    {
        if (!OdfCountryCode.IsAlphaNumericCode(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfScriptCode_ScriptCode18"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始文字系統代碼字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析文字系統代碼。
    /// </summary>
    /// <param name="value">文字系統代碼字串。</param>
    /// <param name="scriptCode">成功時傳回解析後的文字系統代碼。</param>
    /// <returns>若字串符合 ODF <c>scriptCode</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfScriptCode scriptCode)
    {
        if (OdfCountryCode.IsAlphaNumericCode(value))
        {
            scriptCode = new OdfScriptCode(value!);
            return true;
        }

        scriptCode = default;
        return false;
    }

    /// <summary>
    /// 傳回原始文字系統代碼字串。
    /// </summary>
    /// <returns>文字系統代碼字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個文字系統代碼。
    /// </summary>
    /// <param name="other">要比較的文字系統代碼。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfScriptCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfScriptCode other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個文字系統代碼是否相等。
    /// </summary>
    /// <param name="left">左側文字系統代碼。</param>
    /// <param name="right">右側文字系統代碼。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfScriptCode left, OdfScriptCode right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個文字系統代碼是否不相等。
    /// </summary>
    /// <param name="left">左側文字系統代碼。</param>
    /// <param name="right">右側文字系統代碼。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfScriptCode left, OdfScriptCode right) => !left.Equals(right);
}

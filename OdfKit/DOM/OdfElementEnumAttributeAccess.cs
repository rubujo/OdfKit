using System;

namespace OdfKit.DOM;

/// <summary>
/// ODF 元素枚舉 token 屬性解析引擎（內部協作者）。
/// </summary>
internal static class OdfElementEnumAttributeAccess
{
    /// <summary>
    /// 以指定的 TryParse 委派解析可空枚舉屬性字串。
    /// </summary>
    /// <typeparam name="T">枚舉型別。</typeparam>
    /// <param name="value">原始屬性字串。</param>
    /// <param name="tryParse">TryParse 委派。</param>
    /// <returns>解析後的枚舉值；若格式無效則為 <see langword="null"/>。</returns>
    internal static T? GetNullable<T>(string? value, TryParseHandler<T> tryParse)
        where T : struct
        => tryParse(value, out T parsed) ? parsed : null;

    /// <summary>
    /// 以 schema 註冊表的泛型 TryParseEnumToken 解析可空枚舉屬性字串。
    /// </summary>
    /// <typeparam name="TEnum">枚舉型別。</typeparam>
    /// <param name="value">原始屬性字串。</param>
    /// <returns>解析後的枚舉值；若格式無效則為 <see langword="null"/>。</returns>
    internal static TEnum? GetEnumToken<TEnum>(string? value)
        where TEnum : struct, Enum
        => OdfElementSchemaRegistry.TryParseEnumToken(value, out TEnum result) ? result : null;

    /// <summary>
    /// 枚舉屬性 TryParse 委派。
    /// </summary>
    /// <typeparam name="T">枚舉型別。</typeparam>
    /// <param name="value">原始屬性字串。</param>
    /// <param name="result">解析結果。</param>
    /// <returns>若解析成功則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    internal delegate bool TryParseHandler<T>(string? value, out T result)
        where T : struct;
}

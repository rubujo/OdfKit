using System;

namespace OdfKit.Internal;

/// <summary>
/// Provides cross-target enum validation.
/// 提供跨目標的列舉驗證。
/// </summary>
internal static class OdfEnumHelper
{
    /// <summary>Determines whether an enum value is defined. / 判斷列舉值是否已定義。</summary>
    public static bool IsDefined<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
#if NET7_0_OR_GREATER
        return Enum.IsDefined(value);
#else
        return Enum.IsDefined(typeof(TEnum), value);
#endif
    }
}

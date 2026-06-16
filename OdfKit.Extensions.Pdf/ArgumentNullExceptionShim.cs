#if NETSTANDARD2_0
namespace OdfKit.Export.Shim;

/// <summary>
/// 為 .NET Standard 2.0 提供 ArgumentNullException.ThrowIfNull 的相容性墊片。
/// </summary>
internal static class ArgumentNullException
{
    /// <summary>
    /// 若引數為 null 則拋出 ArgumentNullException。
    /// </summary>
    public static void ThrowIfNull(object? argument, string? paramName = null)
    {
        if (argument is null)
        {
            throw new System.ArgumentNullException(paramName);
        }
    }
}
#endif

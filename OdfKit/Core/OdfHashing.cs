namespace OdfKit.Core;

/// <summary>
/// 跨 TFM 安全的雜湊合併輔助（避免 netstandard2.0 對 <see cref="HashCode"/> 的執行期相依）。
/// </summary>
internal static class OdfHashing
{
    /// <summary>
    /// 合併兩個字串欄位的雜湊碼。
    /// </summary>
    internal static int Combine(string? first, string? second)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (first is null ? 0 : StringComparer.Ordinal.GetHashCode(first));
            hash = (hash * 31) + (second is null ? 0 : StringComparer.Ordinal.GetHashCode(second));
            return hash;
        }
    }
}

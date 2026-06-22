using System;
using System.Globalization;

namespace OdfKit.Presentation;

/// <summary>
/// SMIL 時間字串格式化輔助（內部協作者）。
/// </summary>
internal static class OdfSmilTime
{
    /// <summary>
    /// 將時間間隔格式化為 SMIL 持續時間字串（例如 <c>0.5s</c>）。
    /// </summary>
    /// <param name="duration">時間間隔</param>
    /// <returns>SMIL 持續時間字串</returns>
    internal static string FormatDuration(TimeSpan duration) =>
        $"{duration.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";

    /// <summary>
    /// 將延遲時間格式化為 SMIL 延遲字串（例如 <c>0.50s</c>）。
    /// </summary>
    /// <param name="delay">延遲時間</param>
    /// <returns>SMIL 延遲字串</returns>
    internal static string FormatDelay(TimeSpan delay) =>
        $"{delay.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";
}

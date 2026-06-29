using System;
using System.Globalization;

namespace OdfKit.Presentation;

/// <summary>
/// Provides SMIL time string formatting helpers for internal collaborators.
/// SMIL 時間字串格式化輔助（內部協作者）。
/// </summary>
internal static class OdfSmilTime
{
    /// <summary>
    /// Formats a time span as a SMIL duration string, such as <c>0.5s</c>.
    /// 將時間間隔格式化為 SMIL 持續時間字串（例如 <c>0.5s</c>）。
    /// </summary>
    /// <param name="duration">The time span. / 時間間隔。</param>
    /// <returns>The SMIL duration string. / SMIL 持續時間字串。</returns>
    internal static string FormatDuration(TimeSpan duration) =>
        $"{duration.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";

    /// <summary>
    /// Formats a delay as a SMIL delay string, such as <c>0.50s</c>.
    /// 將延遲時間格式化為 SMIL 延遲字串（例如 <c>0.50s</c>）。
    /// </summary>
    /// <param name="delay">The delay time span. / 延遲時間。</param>
    /// <returns>The SMIL delay string. / SMIL 延遲字串。</returns>
    internal static string FormatDelay(TimeSpan delay) =>
        $"{delay.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";
}

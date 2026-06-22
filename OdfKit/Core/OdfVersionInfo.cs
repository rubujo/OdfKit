#pragma warning restore CS1591

using System;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 提供 OdfKit 所使用的 ODF 版本資訊與預設版本配置。
/// </summary>
public static class OdfVersionInfo
{
    /// <summary>
    /// 取得 OdfKit 預設使用的 ODF 版本，目前為 ODF 1.4。
    /// </summary>
    public static OdfVersion DefaultVersion => OdfVersion.Odf14;

    /// <summary>
    /// 取得 OdfKit 預設使用之 ODF 版本的字串表示形式。
    /// </summary>
    public const string DefaultVersionString = "1.4";

    /// <summary>
    /// 將 OdfVersion 轉換為對應的規格版本字串。
    /// </summary>
    /// <param name="version">ODF 版本</param>
    /// <returns>版本字串，如 "1.2"</returns>
    public static string ToVersionString(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 嘗試將規格版本字串轉換為 <see cref="OdfVersion"/>。
    /// </summary>
    /// <param name="value">版本字串，例如 <c>1.4</c></param>
    /// <param name="version">轉換後的 ODF 版本</param>
    /// <returns>若版本字串可辨識則為 <see langword="true"/>，否則為 <see langword="false"/></returns>
    public static bool TryParseVersionString(string? value, out OdfVersion version)
    {
        version = value switch
        {
            "1.0" => OdfVersion.Odf10,
            "1.1" => OdfVersion.Odf11,
            "1.2" => OdfVersion.Odf12,
            "1.3" => OdfVersion.Odf13,
            "1.4" => OdfVersion.Odf14,
            _ => OdfVersion.Unknown
        };
        return version != OdfVersion.Unknown;
    }
}

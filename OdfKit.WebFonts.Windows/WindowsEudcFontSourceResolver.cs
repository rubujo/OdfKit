using System.Runtime.InteropServices;
using Microsoft.Win32;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Windows;

/// <summary>
/// Resolves trusted Windows EUDC TrueType font files from the current-user registry.
/// 從目前使用者登錄設定解析受信任的 Windows EUDC TrueType 字型檔。
/// </summary>
public static class WindowsEudcFontSourceResolver
{
    /// <summary>
    /// Resolves the system-default EUDC font for a Windows code page.
    /// 解析 Windows code page 的系統預設 EUDC 字型。
    /// </summary>
    /// <param name="codePage">The positive Windows code-page identifier. / 正整數 Windows code page 識別碼。</param>
    /// <returns>The absolute trusted TTF or TTE path. / 受信任 TTF 或 TTE 的絕對路徑。</returns>
    public static string ResolveSystemDefaultFont(int codePage)
        => ResolveRegistryValue(codePage, "SystemDefaultEUDCFont");

    /// <summary>
    /// Resolves the separate EUDC font associated with a TrueType typeface.
    /// 解析與 TrueType typeface 關聯的獨立 EUDC 字型。
    /// </summary>
    /// <param name="codePage">The positive Windows code-page identifier. / 正整數 Windows code page 識別碼。</param>
    /// <param name="typeface">The exact registry typeface value name. / 精確的登錄 typeface value 名稱。</param>
    /// <returns>The absolute trusted TTF or TTE path. / 受信任 TTF 或 TTE 的絕對路徑。</returns>
    public static string ResolveAssociatedFont(int codePage, string typeface)
    {
        if (string.IsNullOrWhiteSpace(typeface) || typeface.Length > 256)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"),
                nameof(typeface));
        }

        return ResolveRegistryValue(codePage, typeface);
    }

    private static string ResolveRegistryValue(int codePage, string valueName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        if (codePage <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(codePage),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey($"EUDC\\{codePage}", writable: false);
        if (key?.GetValue(valueName) is not string configuredPath || string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        string path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), configuredPath);
        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath);
        if ((!string.Equals(extension, ".tte", StringComparison.OrdinalIgnoreCase)
             && !string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase))
            || !File.Exists(fullPath))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return fullPath;
    }
}

using System;
using System.Security;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 ZIP 項目名稱淨化引擎（Zip Slip 防禦，內部協作者）。
/// </summary>
internal static class OdfPackageEntryNameSanitizer
{
    /// <summary>
    /// 淨化與驗證 ZIP 項目名稱，防止目錄穿越攻擊（Zip Slip 漏洞防禦）。
    /// </summary>
    /// <param name="name">原始項目名稱</param>
    /// <returns>淨化後的標準項目名稱</returns>
    internal static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        if (name.Contains(":") ||
            name.Contains("//") ||
            name.Contains(@"\\") ||
            name.Contains("../") ||
            name.Contains(@"..\") ||
            name.Equals("..") ||
            name.EndsWith("/..") ||
            name.EndsWith(@"\.."))
        {
            throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfPackageEntryNameSanitizer_ForbiddenAbsolutePathDrive", name));
        }

        string normalized = name.Replace('\\', '/');

        while (normalized.StartsWith("/"))
        {
            normalized = normalized.Substring(1);
        }

        string[] parts = normalized.Split('/');
        foreach (string part in parts)
        {
            if (part == "..")
            {
                throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfPackageEntryNameSanitizer_DirectoryTraversalAttemptZip", name));
            }
        }

        return normalized;
    }
}

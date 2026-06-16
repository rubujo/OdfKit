using System;
using System.Security;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region ZIP Path & Entry Sanitize (Zip Slip Protection)


    /// <summary>
    /// 淨化與驗證 ZIP 項目名稱，防止目錄穿越攻擊（Zip Slip 漏洞防禦）。
    /// </summary>
    /// <param name="name">原始項目名稱</param>
    /// <returns>淨化後的標準項目名稱</returns>
    public static string SanitizeEntryName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Enforce strict directory traversal and malformed path defenses
        if (name.Contains(":") ||
            name.Contains("//") ||
            name.Contains(@"\\") ||
            name.Contains("../") ||
            name.Contains(@"..\") ||
            name.Equals("..") ||
            name.EndsWith("/..") ||
            name.EndsWith(@"\.."))
        {
            throw new SecurityException($"Forbidden absolute path, drive specifier, UNC format, double slashes, or directory traversal: {name}");
        }

        // Normalize backslashes to forward slashes
        string normalized = name.Replace('\\', '/');

        // Strip leading slashes
        while (normalized.StartsWith("/"))
        {
            normalized = normalized.Substring(1);
        }

        // Additional defense in split parts
        string[] parts = normalized.Split('/');
        foreach (var part in parts)
        {
            if (part == "..")
            {
                throw new SecurityException($"Directory traversal attempt (Zip Slip) detected in entry name: {name}");
            }
        }

        return normalized;
    }


    #endregion
}

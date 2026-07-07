using System;
using System.Security;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 ZIP 專案名稱淨化引擎（Zip Slip 防禦，內部協作者）。
/// </summary>
internal static class OdfPackageEntryNameSanitizer
{
    /// <summary>
    /// 淨化與驗證 ZIP 專案名稱，防止目錄穿越攻擊（Zip Slip 漏洞防禦）。
    /// </summary>
    /// <param name="name">原始專案名稱</param>
    /// <returns>淨化後的標準專案名稱</returns>
    internal static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return NormalizePackagePath(name, allowParentSegments: false);
    }

    internal static string NormalizeReferenceUri(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return uri;

        string path = uri;
        int fragmentIndex = path.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            path = path.Substring(0, fragmentIndex);
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? absoluteUri))
        {
            if (string.Equals(absoluteUri.Scheme, "odf", StringComparison.OrdinalIgnoreCase))
            {
                path = absoluteUri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            }
            else if (string.Equals(absoluteUri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            {
                path = absoluteUri.LocalPath;
            }
            else
            {
                throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfPackageEntryNameSanitizer_ForbiddenAbsolutePathDrive", uri));
            }
        }
        else
        {
            path = Uri.UnescapeDataString(path);
        }

        return NormalizePackagePath(path, allowParentSegments: true);
    }

    private static string NormalizePackagePath(string name, bool allowParentSegments)
    {
        if (name.Contains(":") ||
            name.Contains("//") ||
            name.Contains(@"\\") ||
            (!allowParentSegments &&
             (name.Contains("../") ||
              name.Contains(@"..\") ||
              name.Equals("..") ||
              name.EndsWith("/..") ||
              name.EndsWith(@"\.."))))
        {
            throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfPackageEntryNameSanitizer_ForbiddenAbsolutePathDrive", name));
        }

        string normalized = name.Replace('\\', '/');
        bool hasTrailingDirectorySeparator = normalized.EndsWith("/", StringComparison.Ordinal);

        while (normalized.StartsWith("/"))
        {
            normalized = normalized.Substring(1);
        }

        string[] parts = normalized.Split('/');
        var normalizedParts = new System.Collections.Generic.List<string>(parts.Length);
        foreach (string part in parts)
        {
            if (part.Length == 0 || part == ".")
            {
                continue;
            }

            if (part.TrimEnd(' ') == "..")
            {
                if (allowParentSegments && normalizedParts.Count > 0)
                {
                    normalizedParts.RemoveAt(normalizedParts.Count - 1);
                    continue;
                }

                throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfPackageEntryNameSanitizer_DirectoryTraversalAttemptZip", name));
            }

            normalizedParts.Add(part);
        }

        string result = string.Join("/", normalizedParts);
        if (hasTrailingDirectorySeparator && result.Length > 0)
        {
            result += "/";
        }

        return result;
    }
}

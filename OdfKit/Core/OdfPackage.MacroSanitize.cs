using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Xml;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Macro Sanitization


    /// <summary>
    /// 淨化封裝以移除所有 VBA、StarBasic 巨集指令碼、簽章以及指令碼參考。
    /// </summary>
    public void SanitizeMacros()
    {
        _lock.Wait();
        try
        {
            // 1. Collect and remove macro-related entries (basic/ folder, macrosignatures, etc.)
            var entriesToRemove = new List<string>();
            foreach (var key in _entries.Keys)
            {
                if (key.StartsWith("basic/", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("macrosignatures.xml", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("META-INF/macrosignatures.xml", StringComparison.OrdinalIgnoreCase))
                {
                    entriesToRemove.Add(key);
                }
            }

            foreach (var key in entriesToRemove)
            {
                _entries.Remove(key);
                _manifest.Remove(key);
                OdfKitDiagnostics.Info($"Removed macro or signature entry: {key}");
            }

            // 2. Iterate and sanitize all XML files inside the package (excluding META-INF/manifest.xml)
            var xmlEntries = new List<OdfPackageEntry>();
            foreach (var entry in _entries.Values)
            {
                if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                    !entry.Name.Equals("META-INF/manifest.xml", StringComparison.OrdinalIgnoreCase))
                {
                    xmlEntries.Add(entry);
                }
            }

            foreach (var entry in xmlEntries)
            {
                try
                {
                    OdfNode root;
                    using (var stream = entry.OpenReader())
                    {
                        root = OdfXmlReader.Parse(stream, _loadOptions);
                    }

                    if (SanitizeXmlNode(root))
                    {
                        using var ms = new MemoryStream();
                        OdfXmlWriter.Write(root, ms, _saveOptions);

                        byte[] sanitizedBytes = ms.ToArray();
                        _entries[entry.Name] = new OdfPackageEntry(entry.Name, sanitizedBytes);
                        OdfKitDiagnostics.Info($"Sanitized macro references in XML entry: {entry.Name}");
                    }
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to sanitize XML entry '{entry.Name}': {ex.Message}");
                }
            }

            // 3. Remove outdated document signatures
            RemoveOutdatedSignatures();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 遞迴淨化指定的 XML 節點，移除事件監聽器與巨集或指令碼屬性。
    /// </summary>
    /// <param name="node">要淨化的 ODF 節點</param>
    /// <returns>若節點被修改則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public static bool SanitizeXmlNode(OdfNode node)
    {
        if (node == null)
            return false;
        bool modified = false;

        // 1. Recursive removal of event-listener and script elements
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if (child.NodeType == OdfNodeType.Element)
            {
                bool shouldRemove = false;

                if ((child.LocalName == "event-listeners" && (child.NamespaceUri == OdfNamespaces.Office || child.NamespaceUri == OdfNamespaces.Presentation)) ||
                    (child.LocalName == "event-listener" && (child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0" || child.NamespaceUri == OdfNamespaces.Presentation)) ||
                    (child.LocalName == "script" && child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0") ||
                    (child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0"))
                {
                    shouldRemove = true;
                }

                if (shouldRemove)
                {
                    node.RemoveChild(child);
                    modified = true;
                }
                else
                {
                    if (SanitizeXmlNode(child))
                    {
                        modified = true;
                    }
                }
            }
        }

        // 2. Scan and sanitize attributes pointing to scripts/macros
        var attributesToRemove = new List<OdfAttributeName>();
        foreach (var attr in node.Attributes)
        {
            string val = attr.Value;
            bool isHref = attr.Key.LocalName == "href" && attr.Key.NamespaceUri == OdfNamespaces.XLink;
            if (val != null)
            {
                if (val.IndexOf("ooo:StarBasic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    val.IndexOf("vnd.sun.star.script", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    val.IndexOf("vnd.sun.star.VBA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (isHref && (val.IndexOf("basic/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("basic\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Scripts/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Scripts\\", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    attributesToRemove.Add(attr.Key);
                }
            }
        }

        foreach (var attrKey in attributesToRemove)
        {
            node.RemoveAttribute(attrKey.LocalName, attrKey.NamespaceUri);
            modified = true;
        }

        return modified;
    }


    #endregion

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

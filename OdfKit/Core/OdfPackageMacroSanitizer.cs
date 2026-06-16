using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝巨集淨化引擎（內部協作者）。
/// </summary>
internal static class OdfPackageMacroSanitizer
{
    /// <summary>
    /// 淨化封裝以移除所有 VBA、StarBasic 巨集指令碼、簽章以及指令碼參考。
    /// </summary>
    internal static void Sanitize(OdfPackage.OdfPackageMacroSanitizeCollaborators ctx)
    {
        var entriesToRemove = new List<string>();
        foreach (string key in ctx.Entries.Keys)
        {
            if (key.StartsWith("basic/", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("macrosignatures.xml", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("META-INF/macrosignatures.xml", StringComparison.OrdinalIgnoreCase))
            {
                entriesToRemove.Add(key);
            }
        }

        foreach (string key in entriesToRemove)
        {
            ctx.Entries.Remove(key);
            ctx.Manifest.Remove(key);
            OdfKitDiagnostics.Info($"Removed macro or signature entry: {key}");
        }

        var xmlEntries = new List<OdfPackageEntry>();
        foreach (OdfPackageEntry entry in ctx.Entries.Values)
        {
            if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                !entry.Name.Equals("META-INF/manifest.xml", StringComparison.OrdinalIgnoreCase))
            {
                xmlEntries.Add(entry);
            }
        }

        foreach (OdfPackageEntry entry in xmlEntries)
        {
            try
            {
                OdfNode root;
                using (Stream stream = entry.OpenReader())
                    root = OdfXmlReader.Parse(stream, ctx.LoadOptions);

                if (OdfPackage.SanitizeXmlNode(root))
                {
                    using var ms = new MemoryStream();
                    OdfXmlWriter.Write(root, ms, ctx.SaveOptions);

                    byte[] sanitizedBytes = ms.ToArray();
                    ctx.Entries[entry.Name] = new OdfPackageEntry(entry.Name, sanitizedBytes);
                    OdfKitDiagnostics.Info($"Sanitized macro references in XML entry: {entry.Name}");
                }
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to sanitize XML entry '{entry.Name}': {ex.Message}");
            }
        }

        ctx.RemoveOutdatedSignatures();
    }
}

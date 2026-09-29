using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝巨集淨化引擎（內部協作者）。
/// </summary>
internal static class OdfPackageMacroSanitizer
{
    /// <summary>
    /// 判斷封裝項目是否為巨集或巨集簽章。內嵌物件（如 <c>Object 1/</c>）本身是完整的 ODF 子文件，
    /// 其 <c>Basic/</c>、<c>Scripts/</c> 與巨集簽章同樣必須移除，因此比對任意深度的路徑區段。
    /// </summary>
    private static bool IsMacroEntry(string key)
    {
        string[] segments = key.Split('/');
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("basic", StringComparison.OrdinalIgnoreCase) ||
                segments[i].Equals("Scripts", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return key.Equals("macrosignatures.xml", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("/macrosignatures.xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 淨化封裝以移除所有 VBA、StarBasic 巨集指令碼、簽章以及指令碼參考。
    /// </summary>
    internal static void Sanitize(OdfPackage.OdfPackageMacroSanitizeCollaborators ctx)
    {
        var entriesToRemove = new List<string>();
        foreach (string key in ctx.Entries.Keys)
        {
            if (IsMacroEntry(key))
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

        var failedEntries = new List<string>();
        foreach (OdfPackageEntry entry in xmlEntries)
        {
            try
            {
                OdfNode root;
                using (Stream stream = entry.OpenReader())
                    root = OdfXmlReader.Parse(stream, ctx.LoadOptions);

                if (OdfPackageXmlMacroSanitizer.SanitizeNode(root))
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
                failedEntries.Add(entry.Name);
            }
        }

        ctx.RemoveOutdatedSignatures();

        if (failedEntries.Count > 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage(
                "Err_OdfPackageMacroSanitizer_SanitizeFailed", failedEntries.Count, string.Join(", ", failedEntries)));
        }
    }
}

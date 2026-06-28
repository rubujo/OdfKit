using System.IO;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

/// <summary>
/// ODF 文件儲存與持久化引擎（內部協作者）。
/// </summary>
internal static class OdfDocumentPersistenceEngine
{
    /// <summary>
    /// 執行儲存前準備：樣式去重、統計更新、版本套用與 DOM 寫入封裝。
    /// </summary>
    internal static void PrepareDomEntriesForSave(OdfDocument.OdfDocumentPersistenceCollaborators ctx, OdfSaveOptions options)
    {
        ctx.FlushTrackedEmbeddedDocuments(options);
        ctx.PrepareForPersistence(options);
        ctx.StyleEngine.FlushPendingStyles();
        ctx.Package.FormulaExternalLinksForSave = ctx.FormulaExternalLinks;
        OdfFontResolver.EmbedFontSubsets(ctx.Package, ctx.ContentDom, ctx.StylesDom);
        OdfDocumentMetadataEngine.UpdateDocumentStatistics(ctx.MetaDom, ctx.ContentDom);
        ApplySaveVersionOptions(ctx, options);
        WriteAllDomEntries(ctx, options);
        PruneUnusedMedia(ctx, options);
    }

    /// <summary>
    /// 將所有標準 DOM 樹寫入封裝容器。
    /// </summary>
    internal static void WriteAllDomEntries(OdfDocument.OdfDocumentPersistenceCollaborators ctx, OdfSaveOptions options)
    {
        foreach (var desc in ctx.ContentXmlForPersistence.Descendants())
        {
            if (desc is TableTableElement table)
            {
                table.MaterializeSparseCells();
            }
        }

        WriteDomToEntry(ctx, "content.xml", ctx.ContentXmlForPersistence, options);
        WriteDomToEntry(ctx, "styles.xml", ctx.StylesDom, options);
        WriteDomToEntry(ctx, "meta.xml", ctx.MetaDom, options);
        WriteDomToEntry(ctx, "settings.xml", ctx.SettingsDom, options);
    }

    /// <summary>
    /// 將單一 DOM 樹序列化並寫入封裝 entry。
    /// </summary>
    internal static void WriteDomToEntry(
        OdfDocument.OdfDocumentPersistenceCollaborators ctx,
        string name,
        OdfNode node,
        OdfSaveOptions options)
    {
        using var ms = new MemoryStream();
        OdfXmlWriter.Write(node, ms, options);
        string path = string.IsNullOrEmpty(ctx.SubPath) ? name : ctx.SubPath + name;
        ctx.Package.WriteEntry(path, ms.ToArray(), "text/xml");
    }

    private static void ApplySaveVersionOptions(OdfDocument.OdfDocumentPersistenceCollaborators ctx, OdfSaveOptions options)
    {
        OdfVersion? effectiveVersion = options.ForceVersion ?? ctx.TargetVersion;
        if (effectiveVersion is not OdfVersion forcedVersion)
        {
            return;
        }

        string version = OdfVersionInfo.ToVersionString(forcedVersion);
        ctx.Package.Version = forcedVersion;
        SetDocumentRootVersion(ctx.ContentDom, version);
        SetDocumentRootVersion(ctx.StylesDom, version);
        SetDocumentRootVersion(ctx.MetaDom, version);
        SetDocumentRootVersion(ctx.SettingsDom, version);
    }

    private static void SetDocumentRootVersion(OdfNode node, string version)
    {
        if (node.NodeType == OdfNodeType.Element)
        {
            node.SetAttribute("version", OdfNamespaces.Office, version, "office");
        }
    }

    private static void PruneUnusedMedia(OdfDocument.OdfDocumentPersistenceCollaborators ctx, OdfSaveOptions options)
    {
        if (!options.PruneUnusedMedia || !string.IsNullOrEmpty(ctx.SubPath))
        {
            return;
        }

        ctx.Package.PruneUnusedMedia(CollectReferencedPackageMediaPaths(ctx));
    }

    private static System.Collections.Generic.IEnumerable<string> CollectReferencedPackageMediaPaths(
        OdfDocument.OdfDocumentPersistenceCollaborators ctx)
    {
        foreach (string path in CollectReferencedPackageMediaPaths(ctx.ContentXmlForPersistence))
        {
            yield return path;
        }

        foreach (string path in CollectReferencedPackageMediaPaths(ctx.StylesDom))
        {
            yield return path;
        }

        foreach (string path in CollectReferencedPackageMediaPaths(ctx.MetaDom))
        {
            yield return path;
        }

        foreach (string path in CollectReferencedPackageMediaPaths(ctx.SettingsDom))
        {
            yield return path;
        }
    }

    private static System.Collections.Generic.IEnumerable<string> CollectReferencedPackageMediaPaths(OdfNode root)
    {
        foreach (OdfNode node in EnumerateSelfAndDescendants(root))
        {
            foreach (System.Collections.Generic.KeyValuePair<OdfAttributeName, string> attribute in node.Attributes)
            {
                string? normalizedPath = NormalizePackageMediaReference(attribute.Value);
                if (normalizedPath is not null)
                {
                    yield return normalizedPath;
                }
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<OdfNode> EnumerateSelfAndDescendants(OdfNode root)
    {
        yield return root;
        foreach (OdfNode descendant in root.Descendants())
        {
            yield return descendant;
        }
    }

    private static string? NormalizePackageMediaReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        int fragmentIndex = value.IndexOf("#", System.StringComparison.Ordinal);
        if (fragmentIndex >= 0)
        {
            value = value.Substring(0, fragmentIndex);
        }

        while (value.StartsWith("./", System.StringComparison.Ordinal))
        {
            value = value.Substring(2);
        }

        if (!value.StartsWith("Pictures/", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return OdfPackage.SanitizeEntryName(value);
    }
}

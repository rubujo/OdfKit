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
        ctx.PrepareForPersistence(options);
        ctx.StyleEngine.FlushPendingStyles();
        ctx.Package.FormulaExternalLinksForSave = ctx.FormulaExternalLinks;
        OdfFontResolver.EmbedFontSubsets(ctx.Package, ctx.ContentDom, ctx.StylesDom);
        OdfDocumentMetadataEngine.UpdateDocumentStatistics(ctx.MetaDom, ctx.ContentDom);
        ApplySaveVersionOptions(ctx, options);
        WriteAllDomEntries(ctx, options);
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
}

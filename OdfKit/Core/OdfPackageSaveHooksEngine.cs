using System;
using System.IO;
using System.Threading;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Styles;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝儲存前處理掛鉤引擎（公式評估、字型嵌入等）。
/// </summary>
internal static class OdfPackageSaveHooksEngine
{
    /// <summary>
    /// 依儲存選項執行公式評估與字型嵌入等預儲存處理。
    /// </summary>
    internal static void Process(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        CancellationToken cancellationToken)
    {
        OdfFormulaSaveStrategy formulaStrategy = ctx.SaveOptions.FormulaStrategy;
        bool processFormulas =
            formulaStrategy != OdfFormulaSaveStrategy.PreserveCachedValues;
        bool embedFonts = ctx.SaveOptions.EmbedUsedFonts;

        if (!processFormulas && !embedFonts)
            return;

        var nonLazyOptions = ctx.LoadOptions != null
            ? new OdfLoadOptions
            {
                StrictXmlParsing = ctx.LoadOptions.StrictXmlParsing,
                ValidateMimeType = ctx.LoadOptions.ValidateMimeType,
                MaxZipEntries = ctx.LoadOptions.MaxZipEntries,
                MaxEntrySize = ctx.LoadOptions.MaxEntrySize,
                MaxTotalUncompressedSize = ctx.LoadOptions.MaxTotalUncompressedSize,
                MaxPackageSize = ctx.LoadOptions.MaxPackageSize,
                MaxXmlCharactersInDocument = ctx.LoadOptions.MaxXmlCharactersInDocument,
                Password = ctx.LoadOptions.Password,
                CryptographyProvider = ctx.LoadOptions.CryptographyProvider,
                OpenPgpKeyProvider = ctx.LoadOptions.OpenPgpKeyProvider,
                AllowLazyLoading = false
            }
            : new OdfLoadOptions { AllowLazyLoading = false };

        OdfNode? contentRoot = null;
        OdfNode? stylesRoot = null;
        OdfNode? settingsRoot = null;

        if (ctx.Entries.TryGetValue("content.xml", out var contentEntry))
        {
            try
            {
                using var stream = contentEntry.OpenReader();
                contentRoot = OdfXmlReader.Parse(stream, nonLazyOptions);
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to parse content.xml for save processing: {ex.Message}");
            }
        }

        if (embedFonts && ctx.Entries.TryGetValue("styles.xml", out var stylesEntry))
        {
            try
            {
                using var stream = stylesEntry.OpenReader();
                stylesRoot = OdfXmlReader.Parse(stream, nonLazyOptions);
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to parse styles.xml for save processing: {ex.Message}");
            }
        }

        if (formulaStrategy == OdfFormulaSaveStrategy.MarkForRecalculation &&
            ctx.Entries.TryGetValue("settings.xml", out var settingsEntry))
        {
            using var stream = settingsEntry.OpenReader();
            settingsRoot = OdfXmlReader.Parse(stream, nonLazyOptions);
        }

        bool contentModified = false;
        bool stylesModified = false;
        bool settingsModified = false;

        if (processFormulas && contentRoot != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (formulaStrategy == OdfFormulaSaveStrategy.MarkForRecalculation)
            {
                MarkFormulasForRecalculation(contentRoot);
                if (settingsRoot is not null)
                {
                    OdfDocumentSettingsEngine.SetAutoCalculate(settingsRoot, true);
                    settingsModified = true;
                }
            }
            else
            {
                OdfFormulaEvaluationOptions options =
                    ctx.SaveOptions.FormulaEvaluationOptions ??
                    throw new InvalidOperationException(
                        OdfLocalizer.GetMessage(
                            "Err_OdfPackageSaveHooks_FormulaOptionsNull"));
                DefaultFormulaEvaluator evaluator =
                    options.Evaluator ?? new DefaultFormulaEvaluator();
                evaluator.EvaluateFormulasInDocument(
                    contentRoot,
                    ctx.FormulaExternalLinksForSave,
                    options,
                    cancellationToken);
            }

            contentModified = true;
        }

        if (embedFonts && (contentRoot != null || stylesRoot != null))
        {
            try
            {
                var dummy = new OdfNode(OdfNodeType.Element, "dummy", string.Empty);
                ctx.Package.FontContext.EmbedFonts(ctx.Package, contentRoot ?? dummy, stylesRoot ?? dummy);
                if (contentRoot != null)
                    contentModified = true;
                if (stylesRoot != null)
                    stylesModified = true;
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to embed fonts in document on save: {ex.Message}");
            }
        }

        if (contentModified && contentRoot != null)
            WriteXmlEntry(ctx, "content.xml", contentRoot);

        if (stylesModified && stylesRoot != null)
            WriteXmlEntry(ctx, "styles.xml", stylesRoot);

        if (settingsModified && settingsRoot != null)
            WriteXmlEntry(ctx, "settings.xml", settingsRoot);
    }

    private static void MarkFormulasForRecalculation(OdfNode contentRoot)
    {
        foreach (OdfNode node in contentRoot.Descendants())
        {
            if (node.NodeType != OdfNodeType.Element ||
                node.LocalName != "table-cell" ||
                node.NamespaceUri != OdfNamespaces.Table ||
                node.GetAttribute("formula", OdfNamespaces.Table) is null)
            {
                continue;
            }

            node.RemoveAttribute("value-type", OdfNamespaces.Office);
            node.RemoveAttribute("value", OdfNamespaces.Office);
            node.RemoveAttribute("string-value", OdfNamespaces.Office);
            node.RemoveAttribute("boolean-value", OdfNamespaces.Office);
            node.RemoveAttribute("date-value", OdfNamespaces.Office);
            node.RemoveAttribute("time-value", OdfNamespaces.Office);
            node.Children.Clear();
        }
    }

    private static void WriteXmlEntry(OdfPackage.OdfPackageSaveCollaborators ctx, string entryName, OdfNode root)
    {
        try
        {
            using var ms = new MemoryStream();
            OdfXmlWriter.Write(root, ms, ctx.SaveOptions);
            ctx.WriteEntry(entryName, ms.ToArray(), "text/xml");
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Error($"Failed to write updated {entryName} back to package on save: {ex.Message}", ex);
        }
    }
}

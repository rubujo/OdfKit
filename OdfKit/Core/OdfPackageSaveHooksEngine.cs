using System;
using System.IO;
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
    internal static void Process(OdfPackage.OdfPackageSaveCollaborators ctx)
    {
        bool evaluateFormulas = ctx.SaveOptions.EvaluateFormulasOnSave;
        bool embedFonts = ctx.SaveOptions.EmbedUsedFonts;

        if (!evaluateFormulas && !embedFonts)
            return;

        var nonLazyOptions = ctx.LoadOptions != null
            ? new OdfLoadOptions
            {
                StrictXmlParsing = ctx.LoadOptions.StrictXmlParsing,
                ValidateMimeType = ctx.LoadOptions.ValidateMimeType,
                MaxZipEntries = ctx.LoadOptions.MaxZipEntries,
                MaxEntrySize = ctx.LoadOptions.MaxEntrySize,
                MaxTotalUncompressedSize = ctx.LoadOptions.MaxTotalUncompressedSize,
                MaxXmlCharactersInDocument = ctx.LoadOptions.MaxXmlCharactersInDocument,
                Password = ctx.LoadOptions.Password,
                CryptographyProvider = ctx.LoadOptions.CryptographyProvider,
                OpenPgpKeyProvider = ctx.LoadOptions.OpenPgpKeyProvider,
                AllowLazyLoading = false
            }
            : new OdfLoadOptions { AllowLazyLoading = false };

        OdfNode? contentRoot = null;
        OdfNode? stylesRoot = null;

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

        bool contentModified = false;
        bool stylesModified = false;

        if (evaluateFormulas && contentRoot != null)
        {
            try
            {
                var evaluator = new DefaultFormulaEvaluator();
                evaluator.EvaluateFormulasInDocument(contentRoot, ctx.FormulaExternalLinksForSave);
                contentModified = true;
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to evaluate formulas in document on save: {ex.Message}");
            }
        }

        if (embedFonts && (contentRoot != null || stylesRoot != null))
        {
            try
            {
                var dummy = new OdfNode(OdfNodeType.Element, "dummy", string.Empty);
                OdfFontResolver.EmbedFonts(ctx.Package, contentRoot ?? dummy, stylesRoot ?? dummy);
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

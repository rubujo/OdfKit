using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Styles;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Saving and Atomic Save - Internals

    private void RemoveOutdatedSignatures()
    {
        // 修改後自動清除失效數位簽章，以避免警告
        if (HasEntry("META-INF/documentsignatures.xml"))
        {
            // 暫時略過 WriteEntry/RemoveEntry，以避免無限遞迴
            _entries.Remove("META-INF/documentsignatures.xml");
            _manifest.Remove("META-INF/documentsignatures.xml");
            OdfKitDiagnostics.Info("Outdated digital signatures removed due to package edit.");
        }
    }

    private void ProcessSaveHooks()
    {
        bool evaluateFormulas = _saveOptions.EvaluateFormulasOnSave;
        bool embedFonts = _saveOptions.EmbedUsedFonts;

        if (!evaluateFormulas && !embedFonts)
        {
            return;
        }

        OdfNode? contentRoot = null;
        OdfNode? stylesRoot = null;

        if (_entries.TryGetValue("content.xml", out var contentEntry))
        {
            try
            {
                using var stream = contentEntry.OpenReader();
                contentRoot = OdfXmlReader.Parse(stream, _loadOptions);
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to parse content.xml for save processing: {ex.Message}");
            }
        }

        if (embedFonts && _entries.TryGetValue("styles.xml", out var stylesEntry))
        {
            try
            {
                using var stream = stylesEntry.OpenReader();
                stylesRoot = OdfXmlReader.Parse(stream, _loadOptions);
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
                evaluator.EvaluateFormulasInDocument(contentRoot);
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
                OdfFontResolver.EmbedFonts(this, contentRoot ?? dummy, stylesRoot ?? dummy);
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
        {
            try
            {
                using var ms = new MemoryStream();
                OdfXmlWriter.Write(contentRoot, ms, _saveOptions);
                WriteEntry("content.xml", ms.ToArray(), "text/xml");
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Error($"Failed to write updated content.xml back to package on save: {ex.Message}", ex);
            }
        }

        if (stylesModified && stylesRoot != null)
        {
            try
            {
                using var ms = new MemoryStream();
                OdfXmlWriter.Write(stylesRoot, ms, _saveOptions);
                WriteEntry("styles.xml", ms.ToArray(), "text/xml");
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Error($"Failed to write updated styles.xml back to package on save: {ex.Message}", ex);
            }
        }
    }

    #endregion
}

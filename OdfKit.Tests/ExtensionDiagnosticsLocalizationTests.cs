using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

public class ExtensionDiagnosticsLocalizationTests
{
    [Fact]
    public void ExtensionDiagnosticsDoNotUseLiteralMessages()
    {
        string repoRoot = FindRepositoryRoot();
        var literalDiagnosticPattern = new Regex(
            @"OdfKit(?:\.Core)?\.OdfKitDiagnostics\.(?:Warn|Info|Error)\(\s*(?:\$?@?""|@?\$?"")",
            RegexOptions.Compiled);

        foreach (string extensionRoot in Directory.GetDirectories(repoRoot, "OdfKit.Extensions.*"))
        {
            foreach (string file in Directory.EnumerateFiles(extensionRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                string source = File.ReadAllText(file);

                Assert.DoesNotMatch(literalDiagnosticPattern, source);
            }
        }
    }

    [Fact]
    public void ExtensionDiagnosticKeysResolveForAllSupportedCultures()
    {
        string[] cultures = ["en", "zh-TW", "de", "fr", "nl", "nb", "pt", "it", "sk", "da", "ms", "ko"];
        string[] keys =
        [
            "Diag_OdfTextMeasurer_GdiFontMeasurementFallback",
            "Diag_XlsxToOdfConverter_ChartImportSkipped",
            "Diag_XlsxToOdfConverter_PivotTableImportSkipped",
            "Diag_OdfToXlsxConverter_ChartExportSkipped",
            "Diag_OdfPdfExporter_TrueTypeCollectionFontFallback",
            "Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing",
            "Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing",
            "Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted",
            "Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound",
            "Diag_OdfHybridPdfHelper_OdfAttachmentInjected",
            "Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing",
            "Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected",
            "Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed",
            "Diag_LibreOfficeRenderer_KillAfterTimeoutFailed",
            "Diag_LibreOfficeRenderer_SandboxDeleteFailed"
        ];
        object?[] args = ["A", 2, "C"];

        foreach (string cultureName in cultures)
        {
            var culture = new CultureInfo(cultureName);
            foreach (string key in keys)
            {
                string message = OdfLocalizer.GetMessage(key, culture, args);

                Assert.NotEqual(key, message);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "OdfKit.slnx")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}

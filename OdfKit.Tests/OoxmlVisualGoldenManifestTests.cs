using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OOXML 視覺 golden 場景 manifest 結構完整性。
/// </summary>
public class OoxmlVisualGoldenManifestTests
{
    /// <summary>
    /// 驗證 manifest 包含預期的兩個 OOXML 視覺場景與門檻設定。
    /// </summary>
    [Fact]
    public void Manifest_DefinesExpectedScenarios()
    {
        string manifestPath = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "fixtures",
            "ooxml-visual-golden",
            "manifest.json");
        Assert.True(File.Exists(manifestPath), $"找不到 manifest：{manifestPath}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(5.0, root.GetProperty("thresholdPercent").GetDouble());

        string[] scenarioIds = root.GetProperty("scenarios")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToArray();

        Assert.Equal(["odt-docx-word-pdf", "ods-xlsx-excel-pdf"], scenarioIds);

        JsonElement docxScenario = root.GetProperty("scenarios").EnumerateArray()
            .First(item => item.GetProperty("id").GetString() == "odt-docx-word-pdf");
        Assert.Equal(
            "OfficeInteropConversionTests.WordAndLibreOffice_RenderConvertedDocxToPdf",
            docxScenario.GetProperty("test").GetString());

        string diffScriptPath = Path.Combine(FindRepositoryRoot(), "eng", "scripts", "PdfVisualDiff.py");
        Assert.True(File.Exists(diffScriptPath), $"找不到 PdfVisualDiff.py：{diffScriptPath}");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 repository root。");
    }
}

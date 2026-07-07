using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證官方單檔 sample 可在不依賴 LibreOffice 的情境下編譯執行。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class SampleSmokeTests
{
    /// <summary>
    /// 驗證 <c>samples/Sample.cs</c> 可執行 smoke 路徑並產生可載入、可驗證的代表性 ODF 檔案。
    /// </summary>
    [Fact]
    public void SampleScriptSmokeModeGeneratesValidOdfDocuments()
    {
        string repoRoot = FindRepositoryRoot();
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitSampleSmoke_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "output");
        Directory.CreateDirectory(outputDir);

        try
        {
            string runOutput = RunSample(repoRoot, outputDir);
            Assert.Contains("所有示範文件已成功建立", runOutput, StringComparison.Ordinal);

            (string FileName, OdfDocumentKind Kind)[] expected =
            [
                ("output_text.odt", OdfDocumentKind.Text),
                ("output_text_updated.odt", OdfDocumentKind.Text),
                ("output_spreadsheet.ods", OdfDocumentKind.Spreadsheet),
                ("output_presentation.odp", OdfDocumentKind.Presentation),
                ("output_drawing.odg", OdfDocumentKind.Graphics),
                ("output_chart.odc", OdfDocumentKind.Chart),
                ("output_formula.odf", OdfDocumentKind.Formula),
                ("output_image.odi", OdfDocumentKind.Image),
                ("output_database.odb", OdfDocumentKind.Database),
                ("output_stream.ods", OdfDocumentKind.Spreadsheet),
                ("output_stream.odt", OdfDocumentKind.Text)
            ];

            foreach ((string fileName, OdfDocumentKind kind) in expected)
            {
                string path = Path.Combine(outputDir, fileName);
                Assert.True(File.Exists(path), $"範例產出檔案不存在：{path}");

                using (OdfDocument document = OdfDocument.Load(path))
                {
                    Assert.Equal(kind, document.DocumentKind);
                }

                OdfValidationReport report = OdfValidator.Validate(path);
                Assert.True(report.IsValid, FormatIssues(fileName, report));
            }

            Assert.False(File.Exists(Path.Combine(outputDir, "output_pdf.pdf")));
            Assert.False(File.Exists(Path.Combine(outputDir, "output_docx.docx")));
            Assert.False(File.Exists(Path.Combine(outputDir, "output_xlsx.xlsx")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string RunSample(string repoRoot, string outputDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = repoRoot
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("samples/Sample.cs");
        startInfo.ArgumentList.Add("-p:RunAnalyzersDuringBuild=false");
        startInfo.ArgumentList.Add("-p:UseSharedCompilation=false");
        startInfo.Environment["NuGetAudit"] = "false";
        startInfo.Environment["ODFKIT_SAMPLE_OUTPUT_DIR"] = outputDir;
        startInfo.Environment["ODFKIT_SAMPLE_SMOKE_ONLY"] = "true";

        string? restoreConfigFile = Environment.GetEnvironmentVariable("RestoreConfigFile");
        if (!string.IsNullOrWhiteSpace(restoreConfigFile))
        {
            startInfo.Environment["RestoreConfigFile"] = restoreConfigFile;
        }

        var output = new StringBuilder();
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 dotnet run。");
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(300_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"執行範例程式 Sample.cs 逾時，輸出：{output}");
        }

        process.WaitForExit(5_000);
        string runOutput = output.ToString();
        if (process.ExitCode != 0 && IsRestoreUnavailable(runOutput))
        {
            Assert.Skip("目前 restore 環境無法解析範例相依性，略過 Sample.cs smoke 測試。");
        }

        Assert.True(process.ExitCode == 0, $"執行範例程式 Sample.cs 失敗，輸出：{runOutput}");
        return runOutput;
    }

    private static bool IsRestoreUnavailable(string output) =>
        output.Contains("NU1301", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("Unable to load the service index", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("No such host is known", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("The remote name could not be resolved", StringComparison.OrdinalIgnoreCase);

    private static string FormatIssues(string fileName, OdfValidationReport report) =>
        string.Join(
            ", ",
            report.Issues.Select(issue => fileName + ":" + issue.RuleId + ":" + issue.Message + ":" + issue.PackagePath));

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

        throw new DirectoryNotFoundException("找不到 OdfKit repository root。");
    }
}

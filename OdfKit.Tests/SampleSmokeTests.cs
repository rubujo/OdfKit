using System;
using System.Collections.Generic;
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
    /// <remarks>
    /// 此測試以子行程執行 <c>dotnet run samples/Sample.cs</c>。
    /// 全量／雙 TFM 的 <c>dotnet test</c> 宿主可能注入 <c>TargetFramework</c> 等 MSBuild 環境變數；
    /// 本機已重現：子行程若繼承 <c>TargetFramework=net8.0</c> 會 NETSDK1005，
    /// <c>netstandard2.0</c> 會 CS0012（與 OdfKit 條件式套件／TFM 有關）。
    /// 因此必須淨化宿主污染，並以 <c>-f net10.0</c> 固定 file-based app 的目標框架。
    /// </remarks>
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

    /// <summary>
    /// 驗證子行程若帶有宿主式 <c>TargetFramework=net8.0</c> 污染，淨化後仍可成功執行 sample smoke。
    /// </summary>
    /// <remarks>
    /// 污染只寫入 <see cref="ProcessStartInfo.Environment"/>，不修改目前測試進程環境，避免平行測試互相干擾。
    /// </remarks>
    [Fact]
    public void SampleScriptSmokeModeSurvivesHostTargetFrameworkPollution()
    {
        string repoRoot = FindRepositoryRoot();
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitSampleSmokePollute_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "output");
        Directory.CreateDirectory(outputDir);

        try
        {
            // 模擬雙 TFM 測試宿主常見污染（本機已證：未淨化時會 NETSDK1005）。
            string runOutput = RunSample(
                repoRoot,
                outputDir,
                hostPollution: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TargetFramework"] = "net8.0",
                });
            Assert.Contains("所有示範文件已成功建立", runOutput, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(outputDir, "output_text.odt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string RunSample(
        string repoRoot,
        string outputDir,
        IReadOnlyDictionary<string, string>? hostPollution = null)
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
        // 固定 file-based app 目標為 net10.0（Sample 與 OdfKit net10.0 條件相依一致）。
        // 命令列屬性優先於環境變數，作為淨化後的第二道防線。
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("net10.0");
        startInfo.ArgumentList.Add("-p:RunAnalyzersDuringBuild=false");
        startInfo.ArgumentList.Add("-p:UseSharedCompilation=false");

        // 先套用模擬宿主污染，再淨化（回歸測試用；正式路徑 hostPollution 為 null）。
        if (hostPollution is not null)
        {
            foreach ((string key, string value) in hostPollution)
            {
                startInfo.Environment[key] = value;
            }
        }

        IReadOnlyList<string> removedKeys = SanitizeDotnetChildEnvironment(startInfo.Environment);

        startInfo.Environment["NuGetAudit"] = "false";
        startInfo.Environment["ODFKIT_SAMPLE_OUTPUT_DIR"] = outputDir;
        startInfo.Environment["ODFKIT_SAMPLE_SMOKE_ONLY"] = "true";

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
            Assert.Fail(FormatRunFailure("逾時", output.ToString(), removedKeys));
        }

        process.WaitForExit(5_000);
        string runOutput = output.ToString();
        if (process.ExitCode != 0 && IsRestoreUnavailable(runOutput))
        {
            Assert.Skip("目前 restore 環境無法解析範例相依性，略過 Sample.cs smoke 測試。");
        }

        Assert.True(process.ExitCode == 0, FormatRunFailure($"exit={process.ExitCode}", runOutput, removedKeys));
        return runOutput;
    }

    /// <summary>
    /// 清除自 <c>dotnet test</c>／MSBuild 宿主繼承、會干擾獨立 <c>dotnet run</c> 的環境變數。
    /// 不清除 <c>PATH</c>／<c>DOTNET_ROOT</c>／NuGet 快取等執行 SDK 所需變數。
    /// </summary>
    /// <returns>實際移除的鍵名（供失敗診斷）。</returns>
    private static IReadOnlyList<string> SanitizeDotnetChildEnvironment(IDictionary<string, string?> environment)
    {
        var removed = new List<string>();

        // 經本機重現：TargetFramework 注入會讓 #:project 參照的多 TFM 專案解析錯目標。
        // 不把父級 RestoreConfigFile 再寫回子行程（舊實作會主動傳播宿主 restore 污染）。
        string[] exactKeys =
        [
            "TargetFramework",
            "TargetFrameworks",
            "TargetFrameworkMoniker",
            "TargetFrameworkIdentifier",
            "TargetFrameworkVersion",
            "RuntimeIdentifier",
            "RuntimeIdentifiers",
            "RestoreConfigFile",
            "RestoreAdditionalProjectSources",
            "RestoreAdditionalProjectFallbackFolders",
            "RestoreAdditionalProjectFallbackFoldersExcludes",
            "VisualStudioVersion",
            "VSINSTALLDIR",
            "VSAPPIDDIR",
            "VSAPPIDNAME",
            "VSLANG",
            "VSSKUEDITION",
        ];

        foreach (string key in exactKeys)
        {
            if (environment.Remove(key))
            {
                removed.Add(key);
            }
        }

        // 僅清 MSBuild 宿主屬性前綴。
        // 刻意保留一般 DOTNET_*（如 DOTNET_ROOT、DOTNET_HOST_PATH），避免破壞 SDK 探索。
        // MSBuildSDKsPath／MSBUILD_EXE_PATH 若來自 VS／test 宿主，可能指向與 CLI 不一致的工具鏈，一併移除，
        // 讓子行程的 `dotnet` 自行解析 SDK（與在乾淨 shell 執行一致）。
        foreach (string key in environment.Keys.ToArray())
        {
            if (key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("DOTNET_MSBUILD", StringComparison.OrdinalIgnoreCase))
            {
                if (environment.Remove(key))
                {
                    removed.Add(key);
                }
            }
        }

        return removed;
    }

    private static string FormatRunFailure(string reason, string runOutput, IReadOnlyList<string> removedKeys)
    {
        string sanitized = removedKeys.Count == 0
            ? "(none)"
            : string.Join(", ", removedKeys);
        return $"執行範例程式 Sample.cs 失敗（{reason}）。已淨化環境鍵：{sanitized}。輸出：{runOutput}";
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

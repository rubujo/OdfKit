using System.IO;
using System.Text;
using System.Text.Json;
using OdfKit.Cli;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OdfKit CLI 的主要命令流程。
/// </summary>
public class CliTests
{
    /// <summary>
    /// 驗證 help 命令會列出主要子命令。
    /// </summary>
    [Fact]
    public void HelpListsAvailableCommands()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = OdfKitCli.Run(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("validate", output.ToString());
        Assert.Contains("convert-flat", output.ToString());
        Assert.Contains("--baseline odf-validator", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    /// 驗證 validate、info 與 metadata 可讀取 ODF 文件。
    /// </summary>
    [Fact]
    public void ValidateInfoAndMetadataReadPackage()
    {
        string path = CreateTempPath(".odt");
        try
        {
            using (TextDocument document = TextDocument.Create())
            {
                document.Title = "CLI 測試";
                document.Body.Paragraphs.Add("內容");
                document.Save(path);
            }

            AssertCommand(["validate", path], 0, "kind: Text");
            AssertCommand(["info", path], 0, "mime: application/vnd.oasis.opendocument.text");
            AssertCommand(["metadata", path], 0, "title: CLI 測試");
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// 驗證 validate 可輸出穩定 JSON 結果。
    /// </summary>
    [Fact]
    public void ValidateCanWriteJsonOutput()
    {
        string path = CreateTempPath(".odt");
        try
        {
            using (TextDocument document = TextDocument.Create())
            {
                document.Body.Paragraphs.Add("JSON");
                document.Save(path);
            }

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate", path, "--format", "json"], output, error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            Assert.Equal(1, json.RootElement.GetProperty("summary").GetProperty("fileCount").GetInt32());
            Assert.Equal("Text", json.RootElement.GetProperty("files")[0].GetProperty("documentKind").GetString());
            Assert.True(json.RootElement.GetProperty("files")[0].GetProperty("isValid").GetBoolean());
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// 驗證 validate 可遞迴驗證資料夾內的 ODF 檔案。
    /// </summary>
    [Fact]
    public void ValidateCanScanDirectoryRecursively()
    {
        string root = CreateTempDirectory();
        try
        {
            string first = Path.Combine(root, "first.odt");
            string nestedDirectory = Path.Combine(root, "nested");
            Directory.CreateDirectory(nestedDirectory);
            string second = Path.Combine(nestedDirectory, "second.odt");
            CreateTextDocument(first, "第一份");
            CreateTextDocument(second, "第二份");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate", root, "--recursive"], output, error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("summary: files=2", output.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 quiet 模式成功時不輸出驗證明細。
    /// </summary>
    [Fact]
    public void ValidateQuietSuppressesSuccessfulTextOutput()
    {
        string path = CreateTempPath(".odt");
        try
        {
            CreateTextDocument(path, "quiet");
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate", path, "--quiet"], output, error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// 驗證 fail-on warning 會將設定檔問題視為失敗。
    /// </summary>
    [Fact]
    public void ValidateFailOnWarningReturnsFailureForProfileIssues()
    {
        string path = CreateTempPath(".odt");
        try
        {
            CreateMacroPackage(path);

            AssertCommand(
                ["validate", path, "--profile", OdfComplianceProfiles.RocTaiwanGovernmentOdfTools.Id, "--fail-on", "warning"],
                1,
                "DisallowMacroByDefault");
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// 驗證 validate 可執行外部 baseline 命令並輸出 JSON 對照結果。
    /// </summary>
    [Fact]
    public void ValidateBaselineCommandWritesJsonParityResult()
    {
        string path = CreateTempPath(".odt");
        string command = CreateBaselineCommand(exitCode: 0);
        try
        {
            CreateTextDocument(path, "baseline");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                ["validate", path, "--format", "json", "--baseline", "command", "--baseline-command", command],
                output,
                error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            JsonElement summary = json.RootElement.GetProperty("summary");
            JsonElement file = json.RootElement.GetProperty("files")[0];
            JsonElement baseline = file.GetProperty("baseline");
            Assert.Equal(1, summary.GetProperty("baselineFileCount").GetInt32());
            Assert.Equal(0, summary.GetProperty("baselineMismatchCount").GetInt32());
            Assert.True(baseline.GetProperty("isValid").GetBoolean());
            Assert.True(baseline.GetProperty("matchesOdfKit").GetBoolean());
            Assert.Contains("baseline-ok", baseline.GetProperty("standardOutput").GetString());
        }
        finally
        {
            TryDelete(path);
            TryDelete(command);
        }
    }

    /// <summary>
    /// 驗證外部 baseline 與 OdfKit 分類不同時會讓 validate 失敗。
    /// </summary>
    [Fact]
    public void ValidateBaselineMismatchReturnsFailure()
    {
        string path = CreateTempPath(".odt");
        string command = CreateBaselineCommand(exitCode: 1);
        try
        {
            CreateTextDocument(path, "baseline mismatch");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                ["validate", path, "--baseline", "command", "--baseline-command", command],
                output,
                error);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("baseline-matches: False", output.ToString());
        }
        finally
        {
            TryDelete(path);
            TryDelete(command);
        }
    }

    /// <summary>
    /// 驗證已文件化的外部 baseline 差異不會讓 validate 失敗。
    /// </summary>
    [Fact]
    public void ValidateBaselineDocumentedExceptionDoesNotFail()
    {
        string path = CreateTempPath(".odt");
        string command = CreateBaselineCommand(exitCode: 1);
        string exceptions = CreateTempPath(".json");
        try
        {
            CreateTextDocument(path, "baseline documented exception");
            File.WriteAllText(
                exceptions,
                "{\n" +
                "  \"exceptions\": [\n" +
                "    {\n" +
                "      \"path\": \"" + Path.GetFileName(path) + "\",\n" +
                "      \"baseline\": \"command\",\n" +
                "      \"odfKitIsValid\": true,\n" +
                "      \"baselineIsValid\": false,\n" +
                "      \"reason\": \"測試外部 validator 分類差異。\"\n" +
                "    }\n" +
                "  ]\n" +
                "}\n",
                Encoding.UTF8);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                [
                    "validate",
                    path,
                    "--format",
                    "json",
                    "--baseline",
                    "command",
                    "--baseline-command",
                    command,
                    "--baseline-exceptions",
                    exceptions
                ],
                output,
                error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            JsonElement summary = json.RootElement.GetProperty("summary");
            JsonElement baseline = json.RootElement.GetProperty("files")[0].GetProperty("baseline");
            Assert.Equal(0, summary.GetProperty("baselineMismatchCount").GetInt32());
            Assert.Equal(1, summary.GetProperty("baselineDocumentedExceptionCount").GetInt32());
            Assert.False(baseline.GetProperty("matchesOdfKit").GetBoolean());
            Assert.True(baseline.GetProperty("documentedException").GetBoolean());
        }
        finally
        {
            TryDelete(path);
            TryDelete(command);
            TryDelete(exceptions);
        }
    }

    /// <summary>
    /// 驗證 baseline exception 檔案不存在時會回傳使用錯誤。
    /// </summary>
    [Fact]
    public void ValidateBaselineExceptionsRequiresExistingFile()
    {
        string path = CreateTempPath(".odt");
        string command = CreateBaselineCommand(exitCode: 1);
        string exceptions = CreateTempPath(".json");
        try
        {
            CreateTextDocument(path, "missing baseline exceptions");
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                [
                    "validate",
                    path,
                    "--baseline",
                    "command",
                    "--baseline-command",
                    command,
                    "--baseline-exceptions",
                    exceptions
                ],
                output,
                error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("baseline exceptions file not found", error.ToString());
        }
        finally
        {
            TryDelete(path);
            TryDelete(command);
        }
    }

    /// <summary>
    /// 驗證 ODF Validator baseline 缺少 JAR 時會回傳使用錯誤。
    /// </summary>
    [Fact]
    public void ValidateOdfValidatorBaselineRequiresJar()
    {
        string path = CreateTempPath(".odt");
        string jarPath = Path.Combine(Path.GetTempPath(), "odfkit-missing-" + Path.GetRandomFileName() + ".jar");
        try
        {
            CreateTextDocument(path, "missing jar");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                ["validate", path, "--baseline", "odf-validator", "--baseline-jar", jarPath],
                output,
                error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("ODF Validator JAR", error.ToString());
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// 驗證 convert-flat 與 pack 可在封裝 ODF 與 Flat XML 之間轉換。
    /// </summary>
    [Fact]
    public void ConvertFlatAndPackRoundTrip()
    {
        string sourcePath = CreateTempPath(".odt");
        string flatPath = CreateTempPath(".fodt");
        string packedPath = CreateTempPath(".odt");
        try
        {
            using (TextDocument document = TextDocument.Create())
            {
                document.Body.Paragraphs.Add("flat");
                document.Save(sourcePath);
            }

            AssertCommand(["convert-flat", sourcePath, flatPath], 0, "wrote:");
            using (OdfPackage flat = OdfPackage.Open(flatPath))
            {
                Assert.True(flat.IsFlatXml);
            }

            AssertCommand(["pack", flatPath, packedPath], 0, "wrote:");
            using (OdfPackage packed = OdfPackage.Open(packedPath))
            {
                Assert.False(packed.IsFlatXml);
                Assert.Equal("application/vnd.oasis.opendocument.text", packed.MimeType);
            }
        }
        finally
        {
            TryDelete(sourcePath);
            TryDelete(flatPath);
            TryDelete(packedPath);
        }
    }

    /// <summary>
    /// 驗證未知命令會回傳使用錯誤。
    /// </summary>
    [Fact]
    public void UnknownCommandReturnsUsageError()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = OdfKitCli.Run(["unknown"], output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("unknown command", error.ToString());
    }

    /// <summary>
    /// 驗證 validate 對不存在路徑與未知選項回傳使用錯誤。
    /// </summary>
    [Fact]
    public void ValidateReturnsUsageErrorForMissingPathAndUnknownOption()
    {
        using StringWriter missingOutput = new();
        using StringWriter missingError = new();
        int missingExitCode = OdfKitCli.Run(
            ["validate", Path.Combine(Path.GetTempPath(), "odfkit-missing-" + Path.GetRandomFileName() + ".odt")],
            missingOutput,
            missingError);

        using StringWriter optionOutput = new();
        using StringWriter optionError = new();
        int optionExitCode = OdfKitCli.Run(["validate", "--bogus"], optionOutput, optionError);

        Assert.Equal(2, missingExitCode);
        Assert.Contains("path not found", missingError.ToString());
        Assert.Equal(string.Empty, missingOutput.ToString());
        Assert.Equal(2, optionExitCode);
        Assert.Contains("unknown option", optionError.ToString());
        Assert.Equal(string.Empty, optionOutput.ToString());
    }

    private static void AssertCommand(string[] args, int expectedExitCode, string expectedOutput)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = OdfKitCli.Run(args, output, error);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Contains(expectedOutput, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private static string CreateTempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(), "OdfKitCli_" + Path.GetRandomFileName() + extension);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "OdfKitCli_" + Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateTextDocument(string path, string text)
    {
        using TextDocument document = TextDocument.Create();
        document.Body.Paragraphs.Add(text);
        document.Save(path);
    }

    private static void CreateMacroPackage(string path)
    {
        using FileStream stream = File.Create(path);
        using OdfPackage package = OdfPackage.Create(stream, leaveOpen: true);
        package.SetMimeType("application/vnd.oasis.opendocument.text");
        package.WriteEntry(
            "content.xml",
            Encoding.UTF8.GetBytes("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:body><office:text /></office:body></office:document-content>"),
            "text/xml");
        package.WriteEntry("Basic/script.xlb", Encoding.UTF8.GetBytes("macro"), "application/octet-stream");
        package.Save();
    }

    private static string CreateBaselineCommand(int exitCode)
    {
        string path = CreateTempPath(".cmd");
        File.WriteAllText(
            path,
            "@echo off\r\n" +
            "echo baseline-ok %1\r\n" +
            "exit /b " + exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n",
            Encoding.ASCII);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

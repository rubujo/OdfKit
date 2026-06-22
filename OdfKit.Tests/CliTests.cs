using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using OdfKit.Cli;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
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
        Assert.Contains("validate-corpus", output.ToString());
        Assert.Contains("sanitize", output.ToString());
        Assert.Contains("typed-dom-coverage", output.ToString());
        Assert.Contains("convert-flat", output.ToString());
        Assert.Contains("convert-csv", output.ToString());
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
    /// 驗證 sanitize 會將巨集 artifact 移除並另存輸出檔。
    /// </summary>
    [Fact]
    public void SanitizeRemovesMacroArtifactsToOutputFile()
    {
        string sourcePath = CreateTempPath(".odt");
        string outputPath = CreateTempPath(".odt");
        try
        {
            CreateMacroPackage(sourcePath);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["sanitize", sourcePath, outputPath], output, error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("wrote: " + outputPath, output.ToString());
            Assert.Contains("removed-artifacts: 1", output.ToString());
            using (OdfPackage source = OdfPackage.Open(sourcePath))
            {
                Assert.True(source.HasEntry("Basic/script.xlb"));
            }

            using OdfPackage sanitized = OdfPackage.Open(outputPath);
            Assert.False(sanitized.HasEntry("Basic/script.xlb"));
            Assert.True(sanitized.HasEntry("content.xml"));
        }
        finally
        {
            TryDelete(sourcePath);
            TryDelete(outputPath);
        }
    }

    /// <summary>
    /// 驗證 sanitize 可讀取加密輸入並將清理後的輸出重新加密。
    /// </summary>
    [Fact]
    public void SanitizeEncryptedPackageCanReencryptOutput()
    {
        const string inputPassword = "CliInputSecret";
        const string outputPassword = "CliOutputSecret";
        string sourcePath = CreateTempPath(".odt");
        string outputPath = CreateTempPath(".odt");
        try
        {
            CreateMacroPackage(sourcePath, inputPassword);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                [
                    "sanitize",
                    sourcePath,
                    outputPath,
                    "--password",
                    inputPassword,
                    "--output-password",
                    outputPassword,
                    "--encryption",
                    "aes256"
                ],
                output,
                error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("encrypted-output: true", output.ToString());
            Assert.Contains("encryption-algorithm: aes256", output.ToString());

            using (OdfPackage metadataPackage = OdfPackage.Open(outputPath))
            {
                Assert.NotNull(metadataPackage.GetEntryEncryptionInfo("content.xml"));
            }

            using OdfPackage sanitized = OdfPackage.Open(
                outputPath,
                new OdfLoadOptions { Password = outputPassword });
            Assert.False(sanitized.HasEntry("Basic/script.xlb"));
            Assert.True(sanitized.HasEntry("content.xml"));
        }
        finally
        {
            TryDelete(sourcePath);
            TryDelete(outputPath);
        }
    }

    /// <summary>
    /// 驗證 typed-dom-coverage 可輸出 machine-readable JSON 摘要。
    /// </summary>
    [Fact]
    public void TypedDomCoverageCanWriteJsonReport()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = OdfKitCli.Run(["typed-dom-coverage", "--format", "json"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        JsonElement summary = json.RootElement.GetProperty("summary");
        Assert.True(summary.GetProperty("schemaElementCount").GetInt32() >= 550);
        Assert.True(summary.GetProperty("typedElementCount").GetInt32() >= 550);
        Assert.True(summary.GetProperty("schemaChildElementRelationCount").GetInt32() >= 2000);
        Assert.True(summary.GetProperty("schemaAttributeCount").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("childElementRelations").GetArrayLength() >= 2000);
        Assert.Contains(
            json.RootElement.GetProperty("childElementRelations").EnumerateArray(),
            relation => relation.GetProperty("parentNamespaceUri").GetString() == OdfNamespaces.Office &&
                relation.GetProperty("parentLocalName").GetString() == "body" &&
                relation.GetProperty("childNamespaceUri").GetString() == OdfNamespaces.Office &&
                relation.GetProperty("childLocalName").GetString() == "text");
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("childElementCollection").GetInt32() >= 2000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("int").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("bool").GetInt32() >= 10000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("time").GetInt32() >= 6);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("length").GetInt32() >= 10000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("borderWidths").GetInt32() >= 723);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("duration").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("angle").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleName").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleNameList").GetInt32() >= 300);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("color").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("iriReference").GetInt32() >= 400);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("percent").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellAddress").GetInt32() >= 400);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellRangeAddress").GetInt32() >= 400);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellRangeAddressList").GetInt32() >= 800);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("vector3D").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("point3D").GetInt32() >= 90);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("pointList").GetInt32() >= 90);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("languageCode").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("countryCode").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("scriptCode").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("languageTag").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("namespacedToken").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("character").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textEncoding").GetInt32() >= 438);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("targetFrameName").GetInt32() >= 205);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xLinkType").GetInt32() >= 172);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xLinkShow").GetInt32() >= 160);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xLinkActuate").GetInt32() >= 167);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("numberStyle").GetInt32() >= 109);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("numberCalendar").GetInt32() >= 106);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableOrder").GetInt32() >= 108);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableType").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationEffect").GetInt32() >= 131);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationSpeed").GetInt32() >= 231);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationAction").GetInt32() >= 125);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationTransitionType").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationTransitionStyle").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foTextTransform").GetInt32() >= 111);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foTextAlign").GetInt32() >= 106);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleTextRotationScale").GetInt32() >= 111);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleTextCombine").GetInt32() >= 111);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawFill").GetInt32() >= 109);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawFillImageRefPoint").GetInt32() >= 109);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawColorMode").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleVerticalAlign").GetInt32() >= 105);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleVerticalPos").GetInt32() >= 106);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleVerticalRel").GetInt32() >= 106);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleHorizontalPos").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleHorizontalRel").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleWrap").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleRunThrough").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleWrapContourMode").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleWritingMode").GetInt32() >= 104);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableDisplayMemberMode").GetInt32() >= 103);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableLayoutMode").GetInt32() >= 103);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableMemberType").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableGroupedBy").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableSortMode").GetInt32() >= 103);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableConditionSource").GetInt32() >= 103);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableFunction").GetInt32() >= 109);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("databaseRule").GetInt32() >= 206);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("databaseIsNullable").GetInt32() >= 103);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("databaseDataSourceSettingType").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("animationColorInterpolation").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("animationColorInterpolationDirection").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawNoHref").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationPresetClass").GetInt32() >= 103);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("numberTransliterationStyle").GetInt32() >= 105);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleScriptType").GetInt32() >= 111);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleTextEmphasize").GetInt32() >= 111);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawStrokeLineJoin").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("svgStrokeLineCap").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foKeepTogether").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foWrapOption").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("dr3dProjection").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("dr3dShadeMode").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("svgFillRule").GetInt32() >= 109);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableBorderModel").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textLabelFollowedBy").GetInt32() >= 107);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textListLevelPositionMode").GetInt32() >= 106);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textIndexScope").GetInt32() >= 104);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textTableType").GetInt32() >= 103);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textAnchorType").GetInt32() >= 102);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textNoteClass").GetInt32() >= 101);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textSelectPage").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textReferenceFormat").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textStartNumberingAt").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textFootnotesPosition").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textCaptionSequenceFormat").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textNumberPosition").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textPlaceholderType").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textAnimation").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textAnimationDirection").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textKind").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineStyle").GetInt32() >= 534);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineType").GetInt32() >= 433);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineWidth").GetInt32() >= 433);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineMode").GetInt32() >= 333);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontStyle").GetInt32() >= 433);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontVariant").GetInt32() >= 211);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontWeight").GetInt32() >= 433);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontFamilyGeneric").GetInt32() >= 335);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontPitch").GetInt32() >= 335);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontRelief").GetInt32() >= 111);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontStretch").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleLineBreak").GetInt32() >= 98);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleRepeat").GetInt32() >= 111);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleDirection").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("formOrientation").GetInt32() >= 99);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableDirection").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableOrientation").GetInt32() >= 104);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xmlName").GetInt32() >= 1000);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleFamily").GetInt32() >= 50);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("odfVersion").GetInt32() >= 50);
        Assert.True(json.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("mediaType").GetInt32() >= 100);
        Assert.True(json.RootElement.GetProperty("elements").GetArrayLength() >= 550);
    }

    /// <summary>
    /// 驗證 validate 的 JSON 輸出保留可讀的 Unicode 文字，而非 \uXXXX 跳脫序列。
    /// </summary>
    [Fact]
    public void ValidateJsonOutputPreservesReadableUnicodeText()
    {
        string path = CreateTempPath(".odt");
        try
        {
            using (FileStream stream = File.Create(path))
            {
                using OdfPackage package = OdfPackage.Create(stream, leaveOpen: true);
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry(
                    "content.xml",
                    Encoding.UTF8.GetBytes(
                        "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body><office:text /></office:body></office:document-content>"),
                    "text/xml");
                package.Save();
            }

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                ["validate", path, "--format", "json", "--profile", OdfComplianceProfiles.OasisOdf14Strict.Id],
                output,
                error);

            Assert.NotEqual(0, exitCode);
            string json = output.ToString();
            Assert.Contains("加入或修正", json);
            Assert.DoesNotContain("\\u52a0", json);

            using JsonDocument document = JsonDocument.Parse(json);
            string? suggestedFix = document.RootElement
                .GetProperty("files")[0]
                .GetProperty("issues")[0]
                .GetProperty("suggestedFix")
                .GetString();
            Assert.Contains("加入或修正", suggestedFix);
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
    /// 驗證 validate-corpus 可執行 manifest 並輸出 JSON 摘要。
    /// </summary>
    [Fact]
    public void ValidateCorpusCanWriteJsonSummary()
    {
        string root = CreateTempDirectory();
        try
        {
            string fixture = Path.Combine(root, "fixture.odt");
            string manifest = Path.Combine(root, "manifest.json");
            CreateTextDocument(fixture, "corpus");
            WriteCorpusManifest(manifest, "fixture.odt", "valid");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest, "--format", "json"], output, error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            JsonElement summary = json.RootElement.GetProperty("summary");
            Assert.Equal(1, summary.GetProperty("fixtureCount").GetInt32());
            Assert.Equal(1, summary.GetProperty("passedCount").GetInt32());
            Assert.Equal(0, summary.GetProperty("failedCount").GetInt32());
            JsonElement result = json.RootElement.GetProperty("fixtures")[0];
            Assert.Equal("generated-valid", result.GetProperty("id").GetString());
            Assert.True(result.GetProperty("kindMatches").GetBoolean());
            Assert.True(result.GetProperty("versionMatches").GetBoolean());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 對 expected classification 不一致時會失敗。
    /// </summary>
    [Fact]
    public void ValidateCorpusExpectedMismatchReturnsFailure()
    {
        string root = CreateTempDirectory();
        try
        {
            string fixture = Path.Combine(root, "fixture.odt");
            string manifest = Path.Combine(root, "manifest.json");
            CreateTextDocument(fixture, "corpus mismatch");
            WriteCorpusManifest(manifest, "fixture.odt", "invalid");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest], output, error);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("fail", output.ToString());
            Assert.Contains("failed=1", output.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 對 kind 宣告不一致時會失敗。
    /// </summary>
    [Fact]
    public void ValidateCorpusKindMismatchReturnsFailure()
    {
        string root = CreateTempDirectory();
        try
        {
            string fixture = Path.Combine(root, "fixture.odt");
            string manifest = Path.Combine(root, "manifest.json");
            CreateTextDocument(fixture, "corpus kind mismatch");
            WriteCorpusManifest(manifest, "fixture.odt", "valid", kind: "Spreadsheet");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest], output, error);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("kind-matches: False", output.ToString());
            Assert.Contains("kind-mismatches=1", output.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 對 version 宣告不一致時會失敗。
    /// </summary>
    [Fact]
    public void ValidateCorpusVersionMismatchReturnsFailure()
    {
        string root = CreateTempDirectory();
        try
        {
            string fixture = Path.Combine(root, "fixture.odt");
            string manifest = Path.Combine(root, "manifest.json");
            CreateTextDocument(fixture, "corpus version mismatch");
            WriteCorpusManifest(manifest, "fixture.odt", "valid", version: "1.3");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest], output, error);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("version-matches: False", output.ToString());
            Assert.Contains("version-mismatches=1", output.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 會拒絕逃出 corpus root 的 fixture 路徑。
    /// </summary>
    [Fact]
    public void ValidateCorpusRejectsPathEscapingRoot()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "manifest.json");
            WriteCorpusManifest(manifest, "../outside.odt", "valid");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest], output, error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("escapes the corpus root", error.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 會拒絕重複 fixture id。
    /// </summary>
    [Fact]
    public void ValidateCorpusRejectsDuplicateFixtureIds()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "manifest.json");
            WriteCorpusManifest(
                manifest,
                [
                    ("generated-valid", "first.odt", "valid", "Text", "1.4", "semantic-equivalent"),
                    ("generated-valid", "second.odt", "valid", "Text", "1.4", "semantic-equivalent")
                ]);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest], output, error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("duplicate corpus fixture id", error.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 會拒絕未知 roundTrip 策略。
    /// </summary>
    [Fact]
    public void ValidateCorpusRejectsUnknownRoundTripPolicy()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "manifest.json");
            WriteCorpusManifest(
                manifest,
                [
                    ("generated-valid", "fixture.odt", "valid", "Text", "1.4", "unknown-policy")
                ]);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest], output, error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("roundTrip", error.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證外部 corpus fixture 必須宣告可追溯來源 URI。
    /// </summary>
    [Fact]
    public void ValidateCorpusRejectsExternalFixtureWithoutSourceUri()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "manifest.json");
            WriteCorpusManifest(
                manifest,
                [
                    new CorpusFixtureTemplate(
                        "odf-validator-sample-valid",
                        "fixture.odt",
                        "ODF Validator sample",
                        null,
                        "external-review-required",
                        "valid",
                        "Text",
                        "1.4",
                        "semantic-equivalent")
                ]);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest], output, error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("sourceUri", error.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證外部 corpus fixture 有來源 URI 時仍可由 CLI 執行。
    /// </summary>
    [Fact]
    public void ValidateCorpusAcceptsExternalFixtureWithSourceUri()
    {
        string root = CreateTempDirectory();
        try
        {
            string fixture = Path.Combine(root, "fixture.odt");
            string manifest = Path.Combine(root, "manifest.json");
            CreateTextDocument(fixture, "external source uri");
            WriteCorpusManifest(
                manifest,
                [
                    new CorpusFixtureTemplate(
                        "odf-validator-sample-valid",
                        "fixture.odt",
                        "ODF Validator sample",
                        "https://odftoolkit.org/conformance/ODFValidator.html",
                        "external-review-required",
                        "valid",
                        "Text",
                        "1.4",
                        "semantic-equivalent")
                ]);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest, "--format", "json"], output, error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            Assert.Equal(1, json.RootElement.GetProperty("summary").GetProperty("passedCount").GetInt32());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus metadata-only 可檢查外部 manifest 而不要求 fixture 檔案存在。
    /// </summary>
    [Fact]
    public void ValidateCorpusMetadataOnlyDoesNotRequireFixtureFiles()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "manifest.json");
            WriteCorpusManifest(
                manifest,
                [
                    new CorpusFixtureTemplate(
                        "odf-validator-sample-valid",
                        "odf-validator/samples/valid/text/sample.odt",
                        "ODF Validator sample",
                        "https://odftoolkit.org/conformance/ODFValidator.html",
                        "external-review-required",
                        "valid",
                        "Text",
                        "1.4",
                        "semantic-equivalent")
                ]);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(["validate-corpus", manifest, "--metadata-only", "--format", "json"], output, error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            JsonElement summary = json.RootElement.GetProperty("summary");
            JsonElement fixture = json.RootElement.GetProperty("fixtures")[0];
            Assert.True(summary.GetProperty("metadataOnly").GetBoolean());
            Assert.Equal(1, summary.GetProperty("fixtureCount").GetInt32());
            Assert.Equal("https://odftoolkit.org/conformance/ODFValidator.html", fixture.GetProperty("sourceUri").GetString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 baseline exception manifest 不能包含重複例外。
    /// </summary>
    [Fact]
    public void ValidateCorpusRejectsDuplicateBaselineExceptions()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "manifest.json");
            string exceptions = Path.Combine(root, "baseline-exceptions.json");
            WriteCorpusManifest(manifest, "fixture.odt", "valid");
            File.WriteAllText(
                exceptions,
                "{\n" +
                "  \"exceptions\": [\n" +
                "    {\n" +
                "      \"path\": \"fixture.odt\",\n" +
                "      \"baseline\": \"command\",\n" +
                "      \"odfKitIsValid\": true,\n" +
                "      \"baselineIsValid\": false,\n" +
                "      \"profileId\": \"" + OdfComplianceProfiles.OasisOdf14Extended.Id + "\",\n" +
                "      \"reason\": \"第一筆。\"\n" +
                "    },\n" +
                "    {\n" +
                "      \"path\": \"fixture.odt\",\n" +
                "      \"baseline\": \"command\",\n" +
                "      \"odfKitIsValid\": true,\n" +
                "      \"baselineIsValid\": false,\n" +
                "      \"profileId\": \"" + OdfComplianceProfiles.OasisOdf14Extended.Id + "\",\n" +
                "      \"reason\": \"重複例外。\"\n" +
                "    }\n" +
                "  ]\n" +
                "}\n",
                Encoding.UTF8);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                ["validate-corpus", manifest, "--metadata-only", "--baseline-exceptions", exceptions],
                output,
                error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("duplicate baseline exception", error.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 baseline exception manifest 不能引用 manifest 之外的 fixture。
    /// </summary>
    [Fact]
    public void ValidateCorpusRejectsUnusedBaselineExceptions()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "manifest.json");
            string exceptions = Path.Combine(root, "baseline-exceptions.json");
            WriteCorpusManifest(manifest, "fixture.odt", "valid");
            File.WriteAllText(
                exceptions,
                "{\n" +
                "  \"exceptions\": [\n" +
                "    {\n" +
                "      \"path\": \"missing.odt\",\n" +
                "      \"baseline\": \"command\",\n" +
                "      \"odfKitIsValid\": true,\n" +
                "      \"baselineIsValid\": false,\n" +
                "      \"profileId\": \"" + OdfComplianceProfiles.OasisOdf14Extended.Id + "\",\n" +
                "      \"reason\": \"不存在於 manifest 的例外。\"\n" +
                "    }\n" +
                "  ]\n" +
                "}\n",
                Encoding.UTF8);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                ["validate-corpus", manifest, "--metadata-only", "--baseline-exceptions", exceptions],
                output,
                error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("does not match any corpus fixture", error.ToString());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 對未文件化的 baseline 分類差異會失敗。
    /// </summary>
    [Fact]
    public void ValidateCorpusBaselineMismatchReturnsFailure()
    {
        string root = CreateTempDirectory();
        string command = CreateBaselineCommand(exitCode: 1);
        try
        {
            string fixture = Path.Combine(root, "fixture.odt");
            string manifest = Path.Combine(root, "manifest.json");
            CreateTextDocument(fixture, "corpus baseline mismatch");
            WriteCorpusManifest(manifest, "fixture.odt", "valid");

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                [
                    "validate-corpus",
                    manifest,
                    "--format",
                    "json",
                    "--baseline",
                    "command",
                    "--baseline-command",
                    command
                ],
                output,
                error);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            using JsonDocument json = JsonDocument.Parse(output.ToString());
            JsonElement summary = json.RootElement.GetProperty("summary");
            JsonElement fixtureResult = json.RootElement.GetProperty("fixtures")[0];
            JsonElement baseline = fixtureResult.GetProperty("baseline");
            Assert.Equal(1, summary.GetProperty("baselineFileCount").GetInt32());
            Assert.Equal(1, summary.GetProperty("baselineMismatchCount").GetInt32());
            Assert.Equal(0, summary.GetProperty("baselineDocumentedExceptionCount").GetInt32());
            Assert.False(fixtureResult.GetProperty("passed").GetBoolean());
            Assert.False(baseline.GetProperty("isValid").GetBoolean());
            Assert.False(baseline.GetProperty("matchesOdfKit").GetBoolean());
            Assert.False(baseline.GetProperty("documentedException").GetBoolean());
        }
        finally
        {
            TryDeleteDirectory(root);
            TryDelete(command);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus 可沿用 baseline documented exceptions。
    /// </summary>
    [Fact]
    public void ValidateCorpusBaselineDocumentedExceptionDoesNotFail()
    {
        string root = CreateTempDirectory();
        string command = CreateBaselineCommand(exitCode: 1);
        try
        {
            string fixture = Path.Combine(root, "fixture.odt");
            string manifest = Path.Combine(root, "manifest.json");
            string exceptions = Path.Combine(root, "baseline-exceptions.json");
            CreateTextDocument(fixture, "corpus baseline exception");
            WriteCorpusManifest(manifest, "fixture.odt", "valid");
            File.WriteAllText(
                exceptions,
                "{\n" +
                "  \"exceptions\": [\n" +
                "    {\n" +
                "      \"path\": \"fixture.odt\",\n" +
                "      \"baseline\": \"command\",\n" +
                "      \"odfKitIsValid\": true,\n" +
                "      \"baselineIsValid\": false,\n" +
                "      \"profileId\": \"" + OdfComplianceProfiles.OasisOdf14Extended.Id + "\",\n" +
                "      \"reason\": \"測試 corpus baseline 分類差異。\"\n" +
                "    }\n" +
                "  ]\n" +
                "}\n",
                Encoding.UTF8);

            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = OdfKitCli.Run(
                [
                    "validate-corpus",
                    manifest,
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
            JsonElement baseline = json.RootElement.GetProperty("fixtures")[0].GetProperty("baseline");
            Assert.Equal(0, summary.GetProperty("baselineMismatchCount").GetInt32());
            Assert.Equal(1, summary.GetProperty("baselineDocumentedExceptionCount").GetInt32());
            Assert.False(baseline.GetProperty("matchesOdfKit").GetBoolean());
            Assert.True(baseline.GetProperty("documentedException").GetBoolean());
        }
        finally
        {
            TryDeleteDirectory(root);
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
    /// 驗證 convert-csv 可在 ODS 與 CSV 之間轉換。
    /// </summary>
    [Fact]
    public void ConvertCsvRoundTripsSpreadsheetData()
    {
        string odsPath = CreateTempPath(".ods");
        string csvPath = CreateTempPath(".csv");
        string roundTripPath = CreateTempPath(".ods");
        try
        {
            using (SpreadsheetDocument workbook = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = workbook.Worksheets.Add("Data");
                sheet.Cells[0, 0].CellValue = "名稱";
                sheet.Cells[0, 1].CellValue = "數量";
                sheet.Cells[1, 0].CellValue = "蘋果";
                sheet.Cells[1, 1].CellValue = "10";
                workbook.Save(odsPath);
            }

            AssertCommand(["convert-csv", odsPath, csvPath], 0, "direction: ods-to-csv");
            string csv = File.ReadAllText(csvPath, Encoding.UTF8);
            Assert.Contains("名稱", csv);
            Assert.Contains("蘋果", csv);

            AssertCommand(["convert-csv", csvPath, roundTripPath], 0, "direction: csv-to-ods");
            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath);
            Assert.Equal("蘋果", loaded.Worksheets[0].Cells[1, 0].CellValue);
        }
        finally
        {
            TryDelete(odsPath);
            TryDelete(csvPath);
            TryDelete(roundTripPath);
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

    private static void CreateMacroPackage(string path, string? password = null)
    {
        using FileStream stream = File.Create(path);
        OdfSaveOptions? options = password is null
            ? null
            : new OdfSaveOptions
            {
                Password = password,
                EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256
            };
        using OdfPackage package = OdfPackage.Create(stream, leaveOpen: true, options);
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
            "exit /b " + exitCode.ToString(CultureInfo.InvariantCulture) + "\r\n",
            Encoding.ASCII);
        return path;
    }

    private static void WriteCorpusManifest(
        string manifestPath,
        string fixturePath,
        string expected,
        string kind = "Text",
        string version = "1.4")
    {
        WriteCorpusManifest(
            manifestPath,
            [("generated-valid", fixturePath, expected, kind, version, "semantic-equivalent")]);
    }

    private static void WriteCorpusManifest(
        string manifestPath,
        IReadOnlyList<(string Id, string Path, string Expected, string Kind, string Version, string RoundTrip)> fixtures)
    {
        WriteCorpusManifest(
            manifestPath,
            fixtures
                .Select(fixture => new CorpusFixtureTemplate(
                    fixture.Id,
                    fixture.Path,
                    "generated",
                    null,
                    "generated-no-copyright",
                    fixture.Expected,
                    fixture.Kind,
                    fixture.Version,
                    fixture.RoundTrip))
                .ToArray());
    }

    private static void WriteCorpusManifest(
        string manifestPath,
        IReadOnlyList<CorpusFixtureTemplate> fixtures)
    {
        StringBuilder builder = new();
        builder.AppendLine("{");
        builder.AppendLine("  \"fixtures\": [");
        for (int i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            builder.AppendLine("    {");
            builder.AppendLine("      \"id\": \"" + fixture.Id + "\",");
            builder.AppendLine("      \"path\": \"" + fixture.Path.Replace("\\", "\\\\") + "\",");
            builder.AppendLine("      \"source\": \"" + fixture.Source + "\",");
            if (fixture.SourceUri is not null)
            {
                builder.AppendLine("      \"sourceUri\": \"" + fixture.SourceUri + "\",");
            }

            builder.AppendLine("      \"license\": \"" + fixture.License + "\",");
            builder.AppendLine("      \"kind\": \"" + fixture.Kind + "\",");
            builder.AppendLine("      \"version\": \"" + fixture.Version + "\",");
            builder.AppendLine("      \"profile\": \"" + OdfComplianceProfiles.OasisOdf14Extended.Id + "\",");
            builder.AppendLine("      \"expected\": \"" + fixture.Expected + "\",");
            builder.AppendLine("      \"roundTrip\": \"" + fixture.RoundTrip + "\"");
            builder.Append(i + 1 == fixtures.Count ? "    }\n" : "    },\n");
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        File.WriteAllText(manifestPath, builder.ToString(), Encoding.UTF8);
    }

    private sealed record CorpusFixtureTemplate(
        string Id,
        string Path,
        string Source,
        string? SourceUri,
        string License,
        string Expected,
        string Kind,
        string Version,
        string RoundTrip);

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

using System.IO;
using OdfKit.Cli;
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
}

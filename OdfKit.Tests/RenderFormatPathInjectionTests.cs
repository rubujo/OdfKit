using System;
using System.IO;
using System.Threading.Tasks;
using OdfKit.Extensions.Rendering;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：轉換格式（<c>format</c>／<c>convertTo</c>／<c>targetFormat</c>）不得造成路徑穿越。
/// 修正前，格式的副檔名部分被直接拼進沙盒內的檔案路徑並作為 <c>File.Move</c> 的來源；
/// 含 <c>..</c> 時，沙盒外的任意既有檔案會被搬走（實測：格式 <c>pdf/../../dir/secret.txt</c>
/// 使 <c>secret.txt</c> 消失，且轉換仍回報成功）。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class RenderFormatPathInjectionTests
{
    public static TheoryData<string> InvalidFormats =>
    [
        "pdf/../x",
        "../pdf",
        "pdf\\..\\x",
        "pdf/../../../../etc/passwd",
        "pdf\nx",
        "pdf\rx",
        "pdf\0x",
        "..",
        "x..y",
        ":writer_pdf_Export",
    ];

    [Theory]
    [MemberData(nameof(InvalidFormats))]
    public void EnsureValidFormatRejectsPathLikeFormats(string format)
    {
        Assert.Throws<ArgumentException>(() => LibreOfficeRenderer.EnsureValidFormat(format, nameof(format)));
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("PDF")]
    [InlineData(".pdf")]
    [InlineData("docx")]
    [InlineData("x_i")]
    [InlineData("pdf-simulate-error")]
    [InlineData("pdf:writer_pdf_Export")]
    [InlineData("csv:Text - txt - csv (StarCalc):44,34,76,1")]
    [InlineData("html:XHTML Writer File:UTF8")]
    // 既有的參數注入測試依賴：空格、引號與 shell 特殊字元只是單一命令列引數，不涉及路徑。
    [InlineData("pdf --outdir C:\\InjectedDir")]
    [InlineData("pdf&dir|whoami<input>output%temp% --foo")]
    [InlineData("pdf-delay --foo=bar")]
    public void EnsureValidFormatAcceptsRealFormatsAndFilterOptions(string format)
    {
        LibreOfficeRenderer.EnsureValidFormat(format, nameof(format));
    }

    [Fact]
    public async Task ConvertFileAsyncWithParentSegmentsInFormatDoesNotTouchFilesOutsideTheSandbox()
    {
        string mock = MockSofficeFinder.GetMockSofficePath();
        Assert.False(string.IsNullOrEmpty(mock), "MockSoffice not found.");

        string victimDirectory = Path.Combine(Path.GetTempPath(), "odfkit-fmt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(victimDirectory);
        try
        {
            string secret = Path.Combine(victimDirectory, "secret.txt");
            File.WriteAllText(secret, "TOP-SECRET");
            string input = Path.Combine(victimDirectory, "input.odt");
            File.WriteAllText(input, "x");
            string output = Path.Combine(victimDirectory, "out.bin");

            var renderer = new LibreOfficeRenderer { LibreOfficePath = mock, Timeout = TimeSpan.FromSeconds(10) };
            string format = "pdf/../../" + Path.GetFileName(victimDirectory) + "/secret.txt";

            await Assert.ThrowsAsync<ArgumentException>(
                () => renderer.ConvertFileAsync(input, output, format, TestContext.Current.CancellationToken));

            Assert.Equal("TOP-SECRET", File.ReadAllText(secret));
            Assert.False(File.Exists(output));
        }
        finally
        {
            Directory.Delete(victimDirectory, true);
        }
    }

    [Theory]
    [InlineData("pdf/../x")]
    [InlineData("..")]
    public async Task LocalProcessBackendWithPathLikeConvertToThrowsArgumentException(string convertTo)
    {
        var backend = new LocalProcessBackend();
        using var input = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => backend.ConvertAsync(input, "odt", convertTo, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LocalProcessBackendWithPathLikeInputExtensionThrowsArgumentException()
    {
        var backend = new LocalProcessBackend();
        using var input = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => backend.ConvertAsync(input, "odt/../x", "pdf", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("odt", "pdf/../x")]
    [InlineData("odt/../x", "pdf")]
    public async Task UnoserverRestBackendWithPathLikeFormatThrowsBeforeAnyNetworkRequest(string inputExtension, string convertTo)
    {
        // 端點不存在：若驗證沒有在送出請求之前執行，會得到 HttpRequestException 而非 ArgumentException。
        var backend = new UnoserverRestBackend("http://127.0.0.1:1/request");
        using var input = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => backend.ConvertAsync(input, inputExtension, convertTo, TestContext.Current.CancellationToken));
    }
}

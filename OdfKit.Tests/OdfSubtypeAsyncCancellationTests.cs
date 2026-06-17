using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證高階文件子類型非同步載入管線的 CancellationToken 協作取消行為。
/// </summary>
public class OdfSubtypeAsyncCancellationTests
{
    /// <summary>
    /// 預先取消的語彙應使 TextDocument.LoadAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task TextDocument_LoadAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        await using var source = await CreateMinimalOdtStreamAsync(TestContext.Current.CancellationToken);
        source.Position = 0;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var _ = await TextDocument.LoadAsync(source, "test.odt", cts.Token);
        });
    }

    /// <summary>
    /// 預先取消的語彙應使 SpreadsheetDocument.LoadAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task SpreadsheetDocument_LoadAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        await using var source = await CreateMinimalOdsStreamAsync(TestContext.Current.CancellationToken);
        source.Position = 0;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var _ = await SpreadsheetDocument.LoadAsync(source, "test.ods", cts.Token);
        });
    }

    private static async Task<MemoryStream> CreateMinimalOdtStreamAsync(CancellationToken cancellationToken)
    {
        using var doc = TextDocument.Create();
        doc.Body.Paragraphs.Add("子類型取消測試");

        var stream = new MemoryStream();
        await doc.SaveAsync(stream, cancellationToken: cancellationToken);
        stream.Position = 0;
        return stream;
    }

    private static async Task<MemoryStream> CreateMinimalOdsStreamAsync(CancellationToken cancellationToken)
    {
        using var workbook = SpreadsheetDocument.Create();
        workbook.Worksheets.Add("Sheet1");

        var stream = new MemoryStream();
        await workbook.SaveAsync(stream, cancellationToken: cancellationToken);
        stream.Position = 0;
        return stream;
    }
}

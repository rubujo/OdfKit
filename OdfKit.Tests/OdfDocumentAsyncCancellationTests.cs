using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF 文件非同步儲存管線的 CancellationToken 協作取消行為。
/// </summary>
public class OdfDocumentAsyncCancellationTests
{
    /// <summary>
    /// 預先取消的語彙應使 SaveAsync(path) 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task SaveAsync_Path_PreCancelledToken_ThrowsOperationCanceledException()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".odt");
        try
        {
            using var doc = TextDocument.Create();
            doc.Body.Paragraphs.Add("取消測試");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await doc.SaveAsync(path, cancellationToken: cts.Token);
            });
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// 預先取消的語彙應使 SaveAsync(stream) 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task SaveAsync_Stream_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var doc = TextDocument.Create();
        doc.Body.Paragraphs.Add("取消測試");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var destination = new MemoryStream();
            await doc.SaveAsync(destination, cancellationToken: cts.Token);
        });
    }

    /// <summary>
    /// 未取消時 SaveAsync(stream) 應成功寫入可載入的 ODF 文件。
    /// </summary>
    [Fact]
    public async Task SaveAsync_Stream_DefaultToken_CompletesSuccessfully()
    {
        using var doc = TextDocument.Create();
        doc.Body.Paragraphs.Add("非同步儲存");

        await using var destination = new MemoryStream();
        await doc.SaveAsync(destination, cancellationToken: TestContext.Current.CancellationToken);

        destination.Position = 0;
        await using OdfDocument loaded = await OdfDocument.LoadAsync(destination, "test.odt", TestContext.Current.CancellationToken);
        Assert.IsType<TextDocument>(loaded);
    }
}

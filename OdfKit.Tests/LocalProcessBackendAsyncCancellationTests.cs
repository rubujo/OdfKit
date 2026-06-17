using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Extensions.Rendering;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 <see cref="LocalProcessBackend"/> 非同步轉檔管線的 CancellationToken 協作取消行為。
/// </summary>
public class LocalProcessBackendAsyncCancellationTests
{
    /// <summary>
    /// 預先取消的語彙應使 ConvertAsync 在串流複製階段拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task ConvertAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var backend = new LocalProcessBackend();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await backend.ConvertAsync(input, "odt", "pdf", cts.Token);
        });
    }

    /// <summary>
    /// 轉檔進行中收到取消訊號時，應將取消傳遞至底層渲染器並拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task ConvertAsync_CancellationDuringConversion_PropagatesToRenderer()
    {
        var renderer = new BlockingRenderer();
        var backend = new LocalProcessBackend(renderer);
        using var cts = new CancellationTokenSource();
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        Task<Stream> runTask = backend.ConvertAsync(input, "odt", "pdf", cts.Token);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runTask);
    }

    private sealed class BlockingRenderer : LibreOfficeRenderer
    {
        public override Task ConvertFileAsync(
            string inputFilePath,
            string outputPath,
            string format,
            CancellationToken cancellationToken = default)
        {
            return Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
        }
    }
}

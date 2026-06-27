using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Extensions.Rendering;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 LibreOfficeHttpRenderer 的併發、取消與 Mock 轉檔行為之單元測試。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Interop)]
public class LibreOfficeHttpRendererTests
{
    private sealed class MockConversionBackend : ILibreOfficeConversionBackend
    {
        private readonly Func<Stream, string, string, CancellationToken, Task<Stream>> _onConvert;

        public int ActiveCallsCount;
        public int MaxConcurrentCallsObserved;
        private readonly object _lock = new();

        public MockConversionBackend(Func<Stream, string, string, CancellationToken, Task<Stream>> onConvert)
        {
            _onConvert = onConvert;
        }

        public async Task<Stream> ConvertAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct)
        {
            lock (_lock)
            {
                ActiveCallsCount++;
                if (ActiveCallsCount > MaxConcurrentCallsObserved)
                {
                    MaxConcurrentCallsObserved = ActiveCallsCount;
                }
            }

            try
            {
                return await _onConvert(input, inputExtension, convertTo, ct);
            }
            finally
            {
                lock (_lock)
                {
                    ActiveCallsCount--;
                }
            }
        }
    }

    /// <summary>
    /// 驗證在正常流程下，轉換後端回傳的資料流能成功複製到輸出端。
    /// </summary>
    [Fact]
    public async Task ConvertAsync_Success_CopiesStream()
    {
        var mockBackend = new MockConversionBackend((input, ext, to, ct) =>
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write("MOCK PDF CONTENT");
            writer.Flush();
            ms.Position = 0;
            return Task.FromResult<Stream>(ms);
        });

        using var renderer = new LibreOfficeHttpRenderer(mockBackend, maxConcurrentCalls: 2);
        using var doc = TextDocument.Create();
        using var output = new MemoryStream();

        await renderer.ConvertAsync(doc, output, "pdf", TestContext.Current.CancellationToken);

        output.Position = 0;
        using var reader = new StreamReader(output);
        string result = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Equal("MOCK PDF CONTENT", result);
    }

    /// <summary>
    /// 驗證 LibreOfficeHttpRenderer 的最大並行上限 (maxConcurrentCalls) 確實發揮節流限流效果。
    /// </summary>
    [Fact]
    public async Task ConvertAsync_ConcurrencyThrottling_RespectsMaxConcurrentCalls()
    {
        var tcs = new TaskCompletionSource<bool>();

        var mockBackend = new MockConversionBackend(async (input, ext, to, ct) =>
        {
            // 讓請求卡住，用以量測併發數
            await tcs.Task;
            return new MemoryStream();
        });

        // 限制最大併發為 2
        using var renderer = new LibreOfficeHttpRenderer(mockBackend, maxConcurrentCalls: 2);
        using var doc = TextDocument.Create();

        // 同時發起 4 個轉換工作
        var t1 = renderer.ConvertAsync(doc, Stream.Null, "pdf", TestContext.Current.CancellationToken);
        var t2 = renderer.ConvertAsync(doc, Stream.Null, "pdf", TestContext.Current.CancellationToken);
        var t3 = renderer.ConvertAsync(doc, Stream.Null, "pdf", TestContext.Current.CancellationToken);
        var t4 = renderer.ConvertAsync(doc, Stream.Null, "pdf", TestContext.Current.CancellationToken);

        // 給一些時間讓執行緒進入 Wait
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // 雖然有 4 個任務在執行，但由於限流，最多只能有 2 個能同時在 backend 中執行
        Assert.True(mockBackend.MaxConcurrentCallsObserved <= 2,
            $"最大觀測到的併發為 {mockBackend.MaxConcurrentCallsObserved}，但不應超過限流閥值 2。");

        // 釋放卡住的任務以結束測試
        tcs.SetResult(true);

        await Task.WhenAll(t1, t2, t3, t4);
    }

    /// <summary>
    /// 驗證 CancellationToken 取消訊號能正確傳遞並拋出取消例外，防範無用背景運算耗費資源。
    /// </summary>
    [Fact]
    public async Task ConvertAsync_Cancellation_PropagatesToBackend()
    {
        var mockBackend = new MockConversionBackend(async (input, ext, to, ct) =>
        {
            // 等待取消觸發
            await Task.Delay(5000, ct);
            return new MemoryStream();
        });

        using var renderer = new LibreOfficeHttpRenderer(mockBackend, maxConcurrentCalls: 2);
        using var doc = TextDocument.Create();
        using var cts = new CancellationTokenSource();

        var runTask = renderer.ConvertAsync(doc, Stream.Null, "pdf", cts.Token);

        // 延遲一段時間後執行取消
        await Task.Delay(50, TestContext.Current.CancellationToken);
        cts.Cancel();

        // 驗證應拋出 TaskCanceledException 或 OperationCanceledException 例外
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runTask);
    }
}

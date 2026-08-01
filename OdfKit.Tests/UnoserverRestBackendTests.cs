using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Extensions.Rendering;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 unoserver REST 後端的大型串流低常駐行為。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class UnoserverRestBackendTests
{
    [Fact]
    public async Task ConvertAsyncSpoolsRequestAndResponseWithoutMemoryStreamResult()
    {
        byte[] inputBytes = new byte[256 * 1024];
        Random.Shared.NextBytes(inputBytes);
        int receivedBytes = 0;
        using var client = new HttpClient(new DelegateHandler(async (request, cancellationToken) =>
        {
            byte[] multipartBytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            receivedBytes = multipartBytes.Length;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            };
        }));
        var backend = new UnoserverRestBackend("https://converter.example.test/request", client);
        using var input = new MemoryStream(inputBytes, writable: false);

        using Stream result = await backend.ConvertAsync(
            input,
            "odt",
            "pdf",
            TestContext.Current.CancellationToken);

        Assert.IsNotType<MemoryStream>(result);
        Assert.True(receivedBytes > inputBytes.Length);
        Assert.Equal([1, 2, 3, 4], await ReadAllBytesAsync(result));
        Assert.True(input.CanRead);
    }

    [Fact]
    public void TemporaryFileCleanupFailureDoesNotEscape()
    {
        Exception? exception = Record.Exception(() =>
            UnoserverRestBackend.TryDeleteTemporaryFile(
                "unavailable.tmp",
                _ => throw new IOException("Simulated cleanup failure.")));

        Assert.Null(exception);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        return buffer.ToArray();
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}

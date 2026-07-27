using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Internal;

/// <summary>
/// Provides cancellation-aware asynchronous operations across supported targets.
/// 在所有受支援目標提供可取消的非同步操作。
/// </summary>
internal static class OdfAsyncHelper
{
    /// <summary>Reads the remaining text with cancellation support. / 以取消支援讀取剩餘文字。</summary>
    public static Task<string> ReadToEndAsync(StreamReader reader, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        return reader.ReadToEndAsync(cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return reader.ReadToEndAsync();
#endif
    }

    /// <summary>Opens HTTP content as a stream with cancellation support. / 以取消支援將 HTTP 內容開啟為串流。</summary>
    public static Task<Stream> ReadAsStreamAsync(HttpContent content, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        return content.ReadAsStreamAsync(cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return content.ReadAsStreamAsync();
#endif
    }

    /// <summary>Flushes a text writer with cancellation support. / 以取消支援排清文字寫入器。</summary>
    public static Task FlushAsync(TextWriter writer, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        return writer.FlushAsync(cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return writer.FlushAsync();
#endif
    }
}

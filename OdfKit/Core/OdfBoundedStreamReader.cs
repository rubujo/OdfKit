using System;
using System.IO;
using System.Buffers;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 具大小上限的串流讀取工具（內部協作者）。
/// </summary>
internal static class OdfBoundedStreamReader
{
    internal const int DefaultBufferSize = 81920;

    internal static void CopyTo(
        Stream source,
        Stream destination,
        long maxBytes,
        string errorMessageKey,
        long initialBytes = 0)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        try
        {
            long totalBytes = initialBytes;
            EnsureInitialBytes(totalBytes, maxBytes, errorMessageKey);
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalBytes = AddBytes(totalBytes, bytesRead, maxBytes, errorMessageKey);

                destination.Write(buffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static async Task CopyToAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        string errorMessageKey,
        CancellationToken cancellationToken = default,
        long initialBytes = 0)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        try
        {
            long totalBytes = initialBytes;
            EnsureInitialBytes(totalBytes, maxBytes, errorMessageKey);
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                totalBytes = AddBytes(totalBytes, bytesRead, maxBytes, errorMessageKey);

                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static async Task<byte[]> ReadHttpContentAsync(
        HttpContent content,
        long maxBytes,
        string errorMessageKey,
        CancellationToken cancellationToken = default)
    {
        long? contentLength = content.Headers.ContentLength;
        if (contentLength.HasValue && maxBytes > 0 && contentLength.Value > maxBytes)
        {
            throw new SecurityException(OdfLocalizer.GetMessage(errorMessageKey, contentLength.Value, maxBytes));
        }

        using Stream stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
        using var ms = new MemoryStream(contentLength.HasValue && contentLength.Value <= int.MaxValue
            ? (int)contentLength.Value
            : 0);
        await CopyToAsync(stream, ms, maxBytes, errorMessageKey, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>
    /// 檢查初始位元組數是否已超過上限，超過時以 <paramref name="errorMessageKey"/> 對應的在地化訊息擲出 <see cref="SecurityException"/>。
    /// </summary>
    internal static void EnsureInitialBytes(long totalBytes, long maxBytes, string errorMessageKey) =>
        EnsureInitialBytes(totalBytes, maxBytes, (exceeded, max) => new SecurityException(OdfLocalizer.GetMessage(errorMessageKey, exceeded, max)));

    /// <summary>
    /// 以溢位安全的方式累加已讀取位元組數，超過上限時以 <paramref name="errorMessageKey"/> 對應的在地化訊息擲出 <see cref="SecurityException"/>。
    /// </summary>
    internal static long AddBytes(long totalBytes, long bytesRead, long maxBytes, string errorMessageKey) =>
        AddBytes(totalBytes, bytesRead, maxBytes, (exceeded, max) => new SecurityException(OdfLocalizer.GetMessage(errorMessageKey, exceeded, max)));

    /// <summary>
    /// 檢查初始位元組數是否已超過上限，超過時以 <paramref name="exceptionFactory"/> 建構的例外擲出。
    /// </summary>
    internal static void EnsureInitialBytes(long totalBytes, long maxBytes, Func<long, long, Exception> exceptionFactory)
    {
        if (maxBytes > 0 && totalBytes > maxBytes)
        {
            throw exceptionFactory(totalBytes, maxBytes);
        }
    }

    /// <summary>
    /// 以溢位安全的方式累加已讀取位元組數，超過上限時以 <paramref name="exceptionFactory"/> 建構的例外擲出。
    /// </summary>
    internal static long AddBytes(long totalBytes, long bytesRead, long maxBytes, Func<long, long, Exception> exceptionFactory)
    {
        if (maxBytes > 0 && bytesRead > maxBytes - totalBytes)
        {
            long exceededBytes = totalBytes > long.MaxValue - bytesRead ? long.MaxValue : totalBytes + bytesRead;
            throw exceptionFactory(exceededBytes, maxBytes);
        }

        return totalBytes + bytesRead;
    }
}

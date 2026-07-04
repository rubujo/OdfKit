using System;
using System.IO;
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
        byte[] buffer = new byte[DefaultBufferSize];
        long totalBytes = initialBytes;
        EnsureInitialBytes(totalBytes, maxBytes, errorMessageKey);
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytes = AddBytes(totalBytes, bytesRead, maxBytes, errorMessageKey);

            destination.Write(buffer, 0, bytesRead);
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
        byte[] buffer = new byte[DefaultBufferSize];
        long totalBytes = initialBytes;
        EnsureInitialBytes(totalBytes, maxBytes, errorMessageKey);
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes = AddBytes(totalBytes, bytesRead, maxBytes, errorMessageKey);

            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
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

    private static void EnsureInitialBytes(long totalBytes, long maxBytes, string errorMessageKey)
    {
        if (maxBytes > 0 && totalBytes > maxBytes)
        {
            throw new SecurityException(OdfLocalizer.GetMessage(errorMessageKey, totalBytes, maxBytes));
        }
    }

    private static long AddBytes(long totalBytes, int bytesRead, long maxBytes, string errorMessageKey)
    {
        if (maxBytes > 0 && bytesRead > maxBytes - totalBytes)
        {
            long exceededBytes = totalBytes > long.MaxValue - bytesRead ? long.MaxValue : totalBytes + bytesRead;
            throw new SecurityException(OdfLocalizer.GetMessage(errorMessageKey, exceededBytes, maxBytes));
        }

        return totalBytes + bytesRead;
    }
}

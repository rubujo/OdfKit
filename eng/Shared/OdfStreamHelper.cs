using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Internal;

/// <summary>
/// Provides cross-target asynchronous stream operations.
/// 提供跨目標的非同步串流操作。
/// </summary>
internal static class OdfStreamHelper
{
    /// <summary>
    /// Reads asynchronously into an array segment.
    /// 非同步讀取至陣列區段。
    /// </summary>
    public static Task<int> ReadAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        return stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
#else
        return stream.ReadAsync(buffer, offset, count, cancellationToken);
#endif
    }

    /// <summary>
    /// Writes an array segment asynchronously.
    /// 非同步寫入陣列區段。
    /// </summary>
    public static Task WriteAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        return stream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
#else
        return stream.WriteAsync(buffer, offset, count, cancellationToken);
#endif
    }
}

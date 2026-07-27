using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Export;

internal static class OdfManagedExportWriter
{
    internal static OdfExportReport Write(Stream destination, string content, OdfExportFormat format, string backend)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(destination, nameof(destination));

        // Writes the already-built string directly through a StreamWriter instead of first
        // allocating a full UTF-8 byte[] copy of it — avoids holding two full-size buffers
        // (string + byte[]) in memory at once for large exports.
        var encoding = new UTF8Encoding(false);
        using (var writer = new StreamWriter(destination, encoding, 4096, leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
        }

        return new OdfExportReport(format, backend) { BytesWritten = encoding.GetByteCount(content) };
    }

    internal static async Task<OdfExportReport> WriteAsync(
        Stream destination,
        string content,
        OdfExportFormat format,
        string backend,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(destination, nameof(destination));
        cancellationToken.ThrowIfCancellationRequested();

        var encoding = new UTF8Encoding(false);
        var writer = new StreamWriter(destination, encoding, 4096, leaveOpen: true);
        try
        {
            await writer.WriteAsync(content).ConfigureAwait(false);
            await OdfKit.Internal.OdfAsyncHelper.FlushAsync(writer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writer.Dispose();
        }

        return new OdfExportReport(format, backend) { BytesWritten = encoding.GetByteCount(content) };
    }

    internal static OdfExportReport WritePath(string path, string content, OdfExportFormat format, string backend)
    {
        EnsureDirectory(path);
        using FileStream stream = File.Create(path);
        return Write(stream, content, format, backend);
    }

    internal static async Task<OdfExportReport> WritePathAsync(
        string path,
        string content,
        OdfExportFormat format,
        string backend,
        CancellationToken cancellationToken)
    {
        EnsureDirectory(path);
        using FileStream stream = File.Create(path);
        return await WriteAsync(stream, content, format, backend, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(null, nameof(path));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;

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
        string temporaryPath = OdfAtomicFile.CreateTemporaryPath(path);
        try
        {
            OdfExportReport report;
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                report = Write(stream, content, format, backend);
            }

            OdfAtomicFile.Publish(temporaryPath, Path.GetFullPath(path));
            return report;
        }
        finally
        {
            OdfAtomicFile.TryDelete(temporaryPath);
        }
    }

    internal static async Task<OdfExportReport> WritePathAsync(
        string path,
        string content,
        OdfExportFormat format,
        string backend,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectory(path);
        string temporaryPath = OdfAtomicFile.CreateTemporaryPath(path);
        try
        {
            OdfExportReport report;
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                report = await WriteAsync(stream, content, format, backend, cancellationToken).ConfigureAwait(false);
            }

            OdfAtomicFile.Publish(temporaryPath, Path.GetFullPath(path));
            return report;
        }
        finally
        {
            OdfAtomicFile.TryDelete(temporaryPath);
        }
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

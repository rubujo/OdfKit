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
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));
        byte[] bytes = new UTF8Encoding(false).GetBytes(content);
        destination.Write(bytes, 0, bytes.Length);
        return new OdfExportReport(format, backend) { BytesWritten = bytes.Length };
    }

    internal static async Task<OdfExportReport> WriteAsync(
        Stream destination,
        string content,
        OdfExportFormat format,
        string backend,
        CancellationToken cancellationToken)
    {
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));
        byte[] bytes = new UTF8Encoding(false).GetBytes(content);
        await destination.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        return new OdfExportReport(format, backend) { BytesWritten = bytes.Length };
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

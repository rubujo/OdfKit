using System;
using System.IO;

namespace OdfKit.Core;

/// <summary>
/// 解析封裝專案的 MIME 媒體類型。
/// </summary>
internal static class OdfPackageMediaTypeResolver
{
    /// <summary>
    /// 依據專案路徑與呼叫端指定值解析最終 MIME 類型。
    /// </summary>
    /// <param name="entryName">封裝內專案路徑</param>
    /// <param name="mediaType">呼叫端指定的 MIME 類型</param>
    /// <returns>最終要寫入 manifest 的 MIME 類型</returns>
    internal static string Resolve(string entryName, string? mediaType)
    {
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            return mediaType!.Trim();
        }

        if (entryName.EndsWith("/mimetype", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entryName, "mimetype", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string extension = Path.GetExtension(entryName);
        return extension.ToLowerInvariant() switch
        {
            ".xml" => "text/xml",
            ".rdf" => "application/rdf+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".emf" => "image/x-emf",
            ".wmf" => "image/x-wmf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".html" or ".htm" => "text/html",
            _ => "application/octet-stream"
        };
    }
}

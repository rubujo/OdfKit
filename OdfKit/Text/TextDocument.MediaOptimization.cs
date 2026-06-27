using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Text;

public partial class TextDocument
{
    /// <summary>
    /// 最佳化文件中的圖片媒體，並同步更新封裝路徑與 <c>draw:image</c> 參照。
    /// </summary>
    /// <param name="maxDpi">目標最大 DPI；會傳入 <paramref name="optimizer"/> 供影像後端判斷是否需要重採樣</param>
    /// <param name="jpegQuality">JPEG 輸出品質，範圍為 1 至 100</param>
    /// <param name="optimizer">實際執行影像重採樣或轉碼的委派；回傳 <see langword="null"/> 時保留原圖</param>
    /// <returns>已更新的媒體項目數量</returns>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="jpegQuality"/> 不在 1 到 100 之間時擲出</exception>
    public int OptimizeMedia(double maxDpi, int jpegQuality, OdfMediaOptimizer? optimizer = null)
    {
        if (jpegQuality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(jpegQuality), OdfLocalizer.GetMessage("Err_OdfImageExporter_QualityValueBetween1"));
        }

        if (optimizer is null)
        {
            return 0;
        }

        int optimizedCount = 0;
        foreach (OdfImage image in Body.Images.Items)
        {
            string? href = image.ImageHref;
            if (string.IsNullOrEmpty(href) || !Package.HasEntry(href!))
            {
                continue;
            }

            byte[] originalBytes = Package.ReadEntry(href!);
            string mediaType = Package.Manifest.TryGetValue(href!, out string? manifestMediaType)
                ? manifestMediaType
                : string.Empty;
            var request = new OdfMediaOptimizationRequest(
                href!,
                mediaType,
                originalBytes,
                image.Width,
                image.Height,
                maxDpi,
                jpegQuality);

            OdfOptimizedMedia? optimized = optimizer(request);
            if (optimized is null || optimized.Bytes.Length == 0)
            {
                continue;
            }

            string targetPath = CreateOptimizedMediaPath(href!, optimized.Extension);
            Package.WriteEntry(targetPath, optimized.Bytes, optimized.MediaType);
            image.ImageNode.SetAttribute("href", OdfNamespaces.XLink, targetPath, "xlink");
            optimizedCount++;
        }

        if (optimizedCount > 0)
        {
            Package.PruneUnusedMedia(CollectReferencedImagePaths());
        }

        return optimizedCount;
    }

    private IEnumerable<string> CollectReferencedImagePaths()
    {
        foreach (OdfImage image in Body.Images.Items)
        {
            if (!string.IsNullOrEmpty(image.ImageHref))
            {
                yield return image.ImageHref!;
            }
        }
    }

    private static string CreateOptimizedMediaPath(string originalPath, string extension)
    {
        string normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? Path.GetExtension(originalPath)
            : extension;
        if (!normalizedExtension.StartsWith(".", StringComparison.Ordinal))
        {
            normalizedExtension = "." + normalizedExtension;
        }

        string directory = Path.GetDirectoryName(originalPath)?.Replace('\\', '/') ?? "Pictures";
        string fileName = Path.GetFileNameWithoutExtension(originalPath);
        return OdfPackage.SanitizeEntryName($"{directory}/{fileName}.optimized{normalizedExtension}");
    }
}

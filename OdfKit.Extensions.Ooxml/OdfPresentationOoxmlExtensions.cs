using System;
using System.IO;
using OdfKit.Presentation;

namespace OdfKit.Conversion;

/// <summary>
/// 提供 PresentationDocument 與 PPTX 互轉的高階擴充方法。
/// </summary>
public static class OdfPresentationOoxmlExtensions
{
    /// <summary>
    /// 將簡報文件轉換為 PPTX 位元組陣列。
    /// </summary>
    public static byte[] ToPptx(this PresentationDocument presentation)
    {
        if (presentation is null)
            throw new ArgumentNullException(nameof(presentation));

        using var stream = new MemoryStream();
        OdpToPptxConverter.Convert(presentation, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 將簡報文件儲存為 PPTX 檔案。
    /// </summary>
    public static void SaveAsPptx(this PresentationDocument presentation, string path)
    {
        if (presentation is null)
            throw new ArgumentNullException(nameof(presentation));

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路徑不可為空。", nameof(path));

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream fileStream = File.Create(path);
        OdpToPptxConverter.Convert(presentation, fileStream);
    }

    /// <summary>
    /// 從 PPTX 串流匯入為簡報文件。
    /// </summary>
    public static PresentationDocument ToOdpPresentation(this Stream pptxStream)
    {
        if (pptxStream is null)
            throw new ArgumentNullException(nameof(pptxStream));

        return PptxToOdpConverter.Convert(pptxStream);
    }

    /// <summary>
    /// 從 PPTX 檔案載入為簡報文件。
    /// </summary>
    public static PresentationDocument LoadPptxAsOdp(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路徑不可為空。", nameof(path));

        using FileStream fileStream = File.OpenRead(path);
        return PptxToOdpConverter.Convert(fileStream);
    }
}

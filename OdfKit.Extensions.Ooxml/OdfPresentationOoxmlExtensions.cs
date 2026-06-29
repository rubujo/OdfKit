using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Presentation;
namespace OdfKit.Conversion;

/// <summary>
/// Adds OOXML conversion helpers for ODF presentation documents.
/// 提供 PresentationDocument 與 PPTX 互轉的高階擴充方法。
/// </summary>
public static class OdfPresentationOoxmlExtensions
{
    /// <summary>
    /// Converts the presentation document to PPTX.
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
    /// Saves the presentation document as a PPTX file.
    /// 將簡報文件儲存為 PPTX 檔案。
    /// </summary>
    public static void SaveAsPptx(this PresentationDocument presentation, string path)
    {
        if (presentation is null)
            throw new ArgumentNullException(nameof(presentation));

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPresentationOoxmlExtensions_PathCannotBeEmpty_2"), nameof(path));

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream fileStream = File.Create(path);
        OdpToPptxConverter.Convert(presentation, fileStream);
    }

    /// <summary>
    /// Converts a PPTX file into an ODP presentation document.
    /// 從 PPTX 串流匯入為簡報文件。
    /// </summary>
    public static PresentationDocument ToOdpPresentation(this Stream pptxStream)
    {
        if (pptxStream is null)
            throw new ArgumentNullException(nameof(pptxStream));

        return PptxToOdpConverter.Convert(pptxStream);
    }

    /// <summary>
    /// Loads a PPTX file as an ODP presentation document.
    /// 從 PPTX 檔案載入為簡報文件。
    /// </summary>
    public static PresentationDocument LoadPptxAsOdp(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPresentationOoxmlExtensions_PathCannotBeEmpty_2"), nameof(path));

        using FileStream fileStream = File.OpenRead(path);
        return PptxToOdpConverter.Convert(fileStream);
    }
}

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// Renders ODF documents through the PDF export pipeline.
/// 實作 IOdfRenderer 介面，以提供 OdfDocument 的 PDF 匯出功能。
/// </summary>
public sealed class OdfPdfRenderer : IOdfRenderer
{
    static OdfPdfRenderer()
    {
        try
        {
            OdfRendererRegistry.Register(new OdfPdfRenderer());
        }
        catch
        {
            // 靜默忽略註冊失敗（可能在無 UI 測試或重覆初始化環境）
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfPdfRenderer"/> class.
    /// 初始化 OdfPdfRenderer 類別的新執行個體。
    /// </summary>
    public OdfPdfRenderer()
    {
    }

    /// <summary>
    /// Exports the document to PDF.
    /// 將指定的 OdfDocument 轉換並寫入 PDF 輸出資料流。
    /// </summary>
    /// <param name="document">The source or target object. / 要進行轉換的 ODF 文件</param>
    /// <param name="pdfStream">The source or target object. / 要寫入 PDF 的目標資料流</param>
    /// <param name="certificate">The value to use. / 用於簽章 PDF 的憑證；此 PDFsharp 實作目前不支援 PDF 簽章</param>
    public void ExportToPdf(OdfDocument document, Stream pdfStream, X509Certificate2? certificate = null)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (pdfStream is null)
            throw new ArgumentNullException(nameof(pdfStream));
        if (document is TextDocument textDoc)
        {
            if (certificate is null)
            {
                OdfPdfExporter.Export(textDoc, pdfStream);
                return;
            }

            using var unsignedPdf = new MemoryStream();
            OdfPdfExporter.Export(textDoc, unsignedPdf);
            unsignedPdf.Position = 0;
            OdfPdfSignatureWriter.Sign(unsignedPdf, pdfStream, certificate);
        }
        else
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfPdfRenderer_PdfExportOnlySupportedForTextDocument"));
        }
    }
}

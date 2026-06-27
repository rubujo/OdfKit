using System.Text;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using OdfKit.Core;
using OdfKit.Export;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODT 轉換至 PDF 匯出 API。
/// </summary>
public class PdfExportTests
{
    /// <summary>
    /// 驗證含標題與段落的 ODT 可匯出為非空 PDF 位元組流。
    /// </summary>
    [Fact]
    public void Export_TextDocumentWithContent_ProducesNonEmptyPdfStream()
    {
        using var doc = TextDocument.Create();
        doc.AddHeading("PDF 測試標題", 1);
        doc.AddParagraph("這是一段測試內容，用來驗證 PDF 匯出功能。");
        doc.AddHeading("次標題", 2);
        doc.AddParagraph("第二段落。");

        using var ms = new MemoryStream();
        OdfPdfExporter.Export(doc, ms);

        Assert.True(ms.Length > 1024, "PDF 輸出不得小於 1 KB。");
        ms.Position = 0;
        byte[] header = new byte[5];
        ms.Read(header, 0, 5);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(header));
    }

    /// <summary>
    /// 驗證空文件可匯出為 PDF 且不拋出例外。
    /// </summary>
    [Fact]
    public void Export_EmptyDocument_DoesNotThrow()
    {
        using var doc = TextDocument.Create();
        using var ms = new MemoryStream();
        var ex = Record.Exception(() => OdfPdfExporter.Export(doc, ms));
        Assert.Null(ex);
    }

    /// <summary>
    /// 驗證 PDF 轉譯器收到簽章憑證時會產生可驗證的 CMS detached PDF 簽章。
    /// </summary>
    [Fact]
    public void Renderer_ExportToPdfWithCertificate_WritesVerifiableDetachedSignature()
    {
        using var doc = TextDocument.Create();
        doc.AddParagraph("PDF 簽章測試內容。");
        using var ms = new MemoryStream();
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=PdfSigner", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(5));

        var renderer = new OdfPdfRenderer();
        renderer.ExportToPdf(doc, ms, certificate);

        byte[] pdf = ms.ToArray();
        string pdfText = Encoding.ASCII.GetString(pdf);

        Assert.Contains("/SubFilter /adbe.pkcs7.detached", pdfText, StringComparison.Ordinal);
        Assert.Contains("/ByteRange [", pdfText, StringComparison.Ordinal);

        (byte[] signedContent, byte[] signatureBytes) = ExtractPdfSignatureParts(pdf);
        var signedCms = new SignedCms(new ContentInfo(signedContent), detached: true);
        signedCms.Decode(signatureBytes);
        signedCms.CheckSignature(new X509Certificate2Collection(certificate), verifySignatureOnly: true);
    }

    /// <summary>
    /// 驗證 OdfHybridPdfHelper 可將 ODF 檔案注入 PDF 中，並完整無損地提取回原始位元組。
    /// </summary>
    [Fact]
    public void HybridPdf_InjectThenExtract_RoundTripsOriginalOdfBytes()
    {
        using var doc = TextDocument.Create();
        doc.AddHeading("混合 PDF 測試文件", 1);
        doc.AddParagraph("這是用來測試 OdfHybridPdfHelper 注入與提取的內容。");

        using var odtStream = new MemoryStream();
        doc.SaveToStream(odtStream);
        byte[] odtBytes = odtStream.ToArray();

        using var pdfStream = new MemoryStream();
        OdfPdfExporter.Export(doc, pdfStream);

        using var hybridStream = new MemoryStream();
        odtStream.Position = 0;
        pdfStream.Position = 0;
        OdfHybridPdfHelper.InjectOdfToPdf(pdfStream, odtStream, hybridStream, "source.odt");

        hybridStream.Position = 0;
        byte[]? extracted = OdfHybridPdfHelper.ExtractOdfFromPdf(hybridStream);

        Assert.NotNull(extracted);
        Assert.Equal(odtBytes, extracted);
    }

    private static (byte[] SignedContent, byte[] SignatureBytes) ExtractPdfSignatureParts(byte[] pdf)
    {
        string text = Encoding.ASCII.GetString(pdf);
        var byteRangeMatch = System.Text.RegularExpressions.Regex.Match(
            text,
            @"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]",
            System.Text.RegularExpressions.RegexOptions.RightToLeft);
        Assert.True(byteRangeMatch.Success);

        int offset1 = int.Parse(byteRangeMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        int length1 = int.Parse(byteRangeMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        int offset2 = int.Parse(byteRangeMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        int length2 = int.Parse(byteRangeMatch.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);

        using var signedContent = new MemoryStream();
        signedContent.Write(pdf, offset1, length1);
        signedContent.Write(pdf, offset2, length2);

        int contentsMarker = text.LastIndexOf("/Contents <", StringComparison.Ordinal);
        Assert.True(contentsMarker >= 0);
        int hexStart = contentsMarker + "/Contents <".Length;
        int hexEnd = text.IndexOf('>', hexStart);
        Assert.True(hexEnd > hexStart);

        byte[] contents = Convert.FromHexString(text.Substring(hexStart, hexEnd - hexStart));
        return (signedContent.ToArray(), TrimDer(contents));
    }

    private static byte[] TrimDer(byte[] bytes)
    {
        Assert.True(bytes.Length > 2);
        int lengthByte = bytes[1];
        int headerLength = 2;
        int contentLength;
        if ((lengthByte & 0x80) == 0)
        {
            contentLength = lengthByte;
        }
        else
        {
            int lengthBytes = lengthByte & 0x7F;
            Assert.InRange(lengthBytes, 1, 4);
            Assert.True(bytes.Length >= 2 + lengthBytes);
            headerLength += lengthBytes;
            contentLength = 0;
            for (int i = 0; i < lengthBytes; i++)
            {
                contentLength = (contentLength << 8) | bytes[2 + i];
            }
        }

        int totalLength = headerLength + contentLength;
        Assert.InRange(totalLength, 1, bytes.Length);
        byte[] result = new byte[totalLength];
        Buffer.BlockCopy(bytes, 0, result, 0, totalLength);
        return result;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using OdfKit.Export;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 以獨立路徑（.NET 的 SignedCms，另以 openssl 與 pdfium 驗證過）檢查 PDF 簽章：
/// 目錄物件含巢狀字典時不得被截斷，增量更新的 trailer 要沿用原檔的 /ID 與 /Info。
/// </summary>
public sealed class PdfSignatureStructureTests
{
    private const string Pdf =
        "%PDF-1.4\n" +
        "1 0 obj\n<< /Producer (測試) >>\nendobj\n" +
        "2 0 obj\n<< /Type /Catalog /Pages 3 0 R /ViewerPreferences << /HideToolbar true /Direction /L2R >> /PageMode /UseOutlines >>\nendobj\n" +
        "3 0 obj\n<< /Type /Pages /Count 0 /Kids [] >>\nendobj\n" +
        "xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000185 00000 n \n" +
        "trailer\n<< /Size 4 /Root 2 0 R /Info 1 0 R /ID [<1E14EF40013BD84DBBABE75EAD7F3643><1E14EF40013BD84DBBABE75EAD7F3643>] >>\n" +
        "startxref\n233\n%%EOF\n";

    private static (byte[] Signed, X509Certificate2 Certificate) SignSample()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=OdfKit Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(Pdf));
        using var destination = new MemoryStream();
        OdfPdfSignatureWriter.Sign(source, destination, certificate);
        return (destination.ToArray(), certificate);
    }

    private static bool Verifies(byte[] pdf)
    {
        string text = Encoding.Latin1.GetString(pdf);
        Match range = Regex.Match(text, @"/ByteRange \[(\d+) (\d+) (\d+) (\d+)\]");
        int[] r = range.Groups.Cast<Group>().Skip(1).Select(g => int.Parse(g.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        byte[] content = pdf.Skip(r[0]).Take(r[1]).Concat(pdf.Skip(r[2]).Take(r[3])).ToArray();
        string hex = Regex.Match(text, @"/Contents <([0-9A-Fa-f]+)>").Groups[1].Value;
        byte[] cms = Convert.FromHexString(hex.TrimEnd('0').Length % 2 == 0 ? hex.TrimEnd('0') : hex.TrimEnd('0') + "0");
        var signed = new SignedCms(new ContentInfo(content), detached: true);
        signed.Decode(cms);
        try
        {
            signed.CheckSignature(verifySignatureOnly: true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// 簽章涵蓋 ByteRange 以外的全部位元組；竄改任何一個位元組都會使驗證失敗。
    /// </summary>
    [Fact]
    public void SignatureVerifiesAndDetectsTampering()
    {
        (byte[] signed, X509Certificate2 certificate) = SignSample();
        using (certificate)
        {
            Assert.True(Verifies(signed));

            byte[] tampered = (byte[])signed.Clone();
            tampered[20] ^= 1;
            Assert.False(Verifies(tampered));
        }
    }

    /// <summary>
    /// 目錄含巢狀字典時整個目錄物件都保留，並加上 /AcroForm。
    /// </summary>
    [Fact]
    public void CatalogWithNestedDictionaryIsPreserved()
    {
        (byte[] signed, X509Certificate2 certificate) = SignSample();
        using (certificate)
        {
            string text = Encoding.Latin1.GetString(signed);
            Assert.Contains("/ViewerPreferences << /HideToolbar true /Direction /L2R >>", text, StringComparison.Ordinal);
            Assert.Contains("/PageMode /UseOutlines", text[text.LastIndexOf("2 0 obj", StringComparison.Ordinal)..], StringComparison.Ordinal);
            Assert.Contains("/AcroForm", text[text.LastIndexOf("2 0 obj", StringComparison.Ordinal)..], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 增量更新的 trailer 沿用原檔的 /ID 與 /Info。
    /// </summary>
    [Fact]
    public void IncrementalTrailerKeepsIdAndInfo()
    {
        (byte[] signed, X509Certificate2 certificate) = SignSample();
        using (certificate)
        {
            string text = Encoding.Latin1.GetString(signed);
            string lastTrailer = text[text.LastIndexOf("trailer", StringComparison.Ordinal)..];
            Assert.Contains("/Info 1 0 R", lastTrailer, StringComparison.Ordinal);
            Assert.Contains("/ID [<1E14EF40013BD84DBBABE75EAD7F3643><1E14EF40013BD84DBBABE75EAD7F3643>]", lastTrailer, StringComparison.Ordinal);
        }
    }
}

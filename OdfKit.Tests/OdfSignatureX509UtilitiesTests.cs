using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 針對 OdfSignatureX509Utilities.VerifySigningCertificateDigest 的專屬測試（修正 4：
/// 4 處硬編碼英文訊息改走 OdfLocalizer.GetMessage）。透過反射以外的方式直接呼叫 internal
/// 方法（測試專案已透過 InternalsVisibleTo 取得存取權），略過完整簽章驗證管線，
/// 精準鎖定本檔案訊息在地化的行為語意。
/// </summary>
public class OdfSignatureX509UtilitiesTests
{
    [Fact]
    public void MissingCertDigestElementsReturnsLocalizedErrorMessage()
    {
        RunWithEnUsCulture(() =>
        {
            using X509Certificate2 cert = CreateSelfSignedCertificate();

            // 故意省略 CertDigest 底下的 DigestMethod／DigestValue。
            XmlElement signatureElement = BuildSignatureElement(
                "<xades:CertDigest xmlns:xades=\"http://uri.etsi.org/01903/v1.3.2#\"/>");

            bool result = OdfSignatureX509Utilities.VerifySigningCertificateDigest(signatureElement, cert, out string? errorMessage);

            Assert.False(result);
            Assert.Equal(
                OdfLocalizer.GetMessage("Err_OdfSignatureX509Utilities_CertDigestElementsMissing"),
                errorMessage);
        });
    }

    [Fact]
    public void UnsupportedDigestAlgorithmReturnsLocalizedErrorMessageWithAlgorithm()
    {
        RunWithEnUsCulture(() =>
        {
            using X509Certificate2 cert = CreateSelfSignedCertificate();
            const string unsupportedAlgorithm = "http://www.w3.org/2001/04/xmlenc#sha512";

            XmlElement signatureElement = BuildSignatureElement(
                $"""
                <xades:CertDigest xmlns:xades="http://uri.etsi.org/01903/v1.3.2#" xmlns:ds="http://www.w3.org/2000/09/xmldsig#">
                  <ds:DigestMethod Algorithm="{unsupportedAlgorithm}" />
                  <ds:DigestValue>AAAA</ds:DigestValue>
                </xades:CertDigest>
                """);

            bool result = OdfSignatureX509Utilities.VerifySigningCertificateDigest(signatureElement, cert, out string? errorMessage);

            Assert.False(result);
            Assert.Equal(
                OdfLocalizer.GetMessage("Err_OdfSignatureX509Utilities_UnsupportedCertDigestAlgorithm", unsupportedAlgorithm),
                errorMessage);
        });
    }

    [Fact]
    public void CertDigestMismatchReturnsLocalizedErrorMessage()
    {
        RunWithEnUsCulture(() =>
        {
            using X509Certificate2 cert = CreateSelfSignedCertificate();

            // 演算法正確（SHA-256），但摘要值刻意錯誤（全零），必定與實際憑證摘要不符。
            string wrongDigest = Convert.ToBase64String(new byte[32]);
            XmlElement signatureElement = BuildSignatureElement(
                $"""
                <xades:CertDigest xmlns:xades="http://uri.etsi.org/01903/v1.3.2#" xmlns:ds="http://www.w3.org/2000/09/xmldsig#">
                  <ds:DigestMethod Algorithm="http://www.w3.org/2001/04/xmlenc#sha256" />
                  <ds:DigestValue>{wrongDigest}</ds:DigestValue>
                </xades:CertDigest>
                """);

            bool result = OdfSignatureX509Utilities.VerifySigningCertificateDigest(signatureElement, cert, out string? errorMessage);

            Assert.False(result);
            Assert.Equal(
                OdfLocalizer.GetMessage("Err_OdfSignatureX509Utilities_CertDigestMismatch"),
                errorMessage);
        });
    }

    [Fact]
    public void SerialNumberMismatchReturnsLocalizedErrorMessageWithBothSerials()
    {
        RunWithEnUsCulture(() =>
        {
            using X509Certificate2 cert = CreateSelfSignedCertificate();

            // CertDigest 使用實際憑證的正確 SHA-256 摘要，確保先前的檢查全部通過，
            // 單獨鎖定 IssuerSerial 序號比對失敗的訊息在地化行為。
            string correctDigest = Convert.ToBase64String(SHA256.HashData(cert.RawData));
            var bigSerial = System.Numerics.BigInteger.Parse(
                "0" + cert.SerialNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            string actualSerialBase10 = bigSerial.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string wrongSerialBase10 = (bigSerial + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

            XmlElement signatureElement = BuildSignatureElement(
                $"""
                <xades:CertDigest xmlns:xades="http://uri.etsi.org/01903/v1.3.2#" xmlns:ds="http://www.w3.org/2000/09/xmldsig#">
                  <ds:DigestMethod Algorithm="http://www.w3.org/2001/04/xmlenc#sha256" />
                  <ds:DigestValue>{correctDigest}</ds:DigestValue>
                </xades:CertDigest>
                <xades:IssuerSerial xmlns:xades="http://uri.etsi.org/01903/v1.3.2#" xmlns:ds="http://www.w3.org/2000/09/xmldsig#">
                  <ds:X509IssuerName>{cert.Issuer}</ds:X509IssuerName>
                  <ds:X509SerialNumber>{wrongSerialBase10}</ds:X509SerialNumber>
                </xades:IssuerSerial>
                """);

            bool result = OdfSignatureX509Utilities.VerifySigningCertificateDigest(signatureElement, cert, out string? errorMessage);

            Assert.False(result);
            Assert.Equal(
                OdfLocalizer.GetMessage(
                    "Err_OdfSignatureX509Utilities_SerialNumberMismatch",
                    actualSerialBase10,
                    wrongSerialBase10),
                errorMessage);
        });
    }

    private static void RunWithEnUsCulture(Action action)
    {
        var originalUICulture = Thread.CurrentThread.CurrentUICulture;
        var originalDefaultCulture = OdfLocalizer.DefaultCulture;

        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        OdfLocalizer.DefaultCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            action();
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
            OdfLocalizer.DefaultCulture = originalDefaultCulture;
        }
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=X509UtilitiesTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
    }

    private static XmlElement BuildSignatureElement(string certDigestAndIssuerSerialXml)
    {
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(
            $"""
            <ds:Signature xmlns:ds="http://www.w3.org/2000/09/xmldsig#" xmlns:xades="http://uri.etsi.org/01903/v1.3.2#">
              <ds:Object>
                <xades:QualifyingProperties>
                  <xades:SignedProperties>
                    <xades:SignedSignatureProperties>
                      <xades:SigningCertificate>
                        <xades:Cert>
                          {certDigestAndIssuerSerialXml}
                        </xades:Cert>
                      </xades:SigningCertificate>
                    </xades:SignedSignatureProperties>
                  </xades:SignedProperties>
                </xades:QualifyingProperties>
              </ds:Object>
            </ds:Signature>
            """);
        return doc.DocumentElement!;
    }
}

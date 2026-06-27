using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證數位簽章非同步管線的 CancellationToken 協作取消行為。
/// </summary>
public class OdfSignerAsyncCancellationTests
{
    /// <summary>
    /// 預先取消的語彙應使 OdfSigner.SignAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task SignAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var package = CreateMinimalPackage();
        using var cert = CreateSelfSignedCertificate();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await OdfSigner.SignAsync(package, cert, cancellationToken: cts.Token);
        });
    }

    /// <summary>
    /// 預先取消的語彙應使 OdfSigner.VerifySignaturesAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task VerifySignaturesAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var package = CreateMinimalPackage();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await OdfSigner.VerifySignaturesAsync(package, cancellationToken: cts.Token);
        });
    }

    /// <summary>
    /// 預先取消的語彙應使 OdfDocument.SignAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task DocumentSignAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var doc = TextDocument.Create();
        doc.Body.Paragraphs.Add("簽章取消測試");
        using var cert = CreateSelfSignedCertificate();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await doc.SignAsync(cert, cts.Token);
        });
    }

    /// <summary>
    /// 文件層一鍵式 SignDocumentAsync 應寫入簽章專案並可由文件層驗證通過。
    /// </summary>
    [Fact]
    public async Task DocumentSignDocumentAsync_DefaultToken_WritesAndVerifiesSignature()
    {
        using var doc = TextDocument.Create();
        doc.Body.Paragraphs.Add("一鍵式文件簽章測試");
        using var cert = CreateSelfSignedCertificate();

        await doc.SignDocumentAsync(cert, TestContext.Current.CancellationToken);

        OdfDocumentSignatureSummary summary = doc.GetSignatureSummary();
        Assert.True(summary.IsSigned);
        Assert.Equal(1, summary.SignatureCount);

        OdfSignatureValidationResult result = await doc.VerifySignaturesAsync(
            new OdfSigningOptions { AllowUntrustedRoot = true },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Single(result.Signatures);
    }

    /// <summary>
    /// 未取消時 SignAsync 與 VerifySignaturesAsync 應成功完成基本簽章往返。
    /// </summary>
    [Fact]
    public async Task SignAndVerifyAsync_DefaultToken_CompletesSuccessfully()
    {
        using var package = CreateMinimalPackage();
        using var cert = CreateSelfSignedCertificate();

        await OdfSigner.SignAsync(package, cert, cancellationToken: TestContext.Current.CancellationToken);

        OdfSignatureValidationResult result = await OdfSigner.VerifySignaturesAsync(
            package,
            new OdfSigningOptions { AllowUntrustedRoot = true },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Signatures);
    }

    private static OdfPackage CreateMinimalPackage()
    {
        var package = OdfPackage.Create(new MemoryStream());
        package.SetMimeType("application/vnd.oasis.opendocument.text");
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
        package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
        package.WriteEntry("meta.xml", Encoding.UTF8.GetBytes("<meta/>"), "text/xml");
        package.WriteEntry("settings.xml", Encoding.UTF8.GetBytes("<settings/>"), "text/xml");
        return package;
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=AsyncCancellationTestSigner",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

        using X509Certificate2 cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(5));

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
#else
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
#endif
    }
}

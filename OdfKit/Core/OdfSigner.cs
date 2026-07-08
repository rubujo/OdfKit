using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// Provides digital signing and signature validation for ODF packages.
/// 提供 ODF 封裝的數位簽署與簽章驗證功能。
/// </summary>
public static partial class OdfSigner
{
    /// <summary>
    /// Executes the Sign operation.
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署，支援同僚聯署。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    public static void Sign(OdfPackage package, X509Certificate2 certificate)
    {
        Sign(package, certificate, new OdfSigningOptions { Level = XadesLevel.None });
    }

    /// <summary>
    /// Executes the Sign operation.
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署，支援自訂選項。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    /// <param name="options">簽署選項</param>
    /// <remarks>在 ASP.NET Core 等伺服器環境中，請優先使用 <see cref="SignAsync(OdfPackage, X509Certificate2, OdfSigningOptions, CancellationToken)"/> 以避免阻塞執行緒</remarks>
    public static void Sign(OdfPackage package, X509Certificate2 certificate, OdfSigningOptions options)
    {
        SignAsync(package, certificate, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously signs the ODF package with the default XMLDSig options.
    /// 以預設 XMLDSig 選項非同步簽署 ODF 封裝。
    /// </summary>
    /// <param name="package">The ODF package to sign. / 要簽署的 ODF 封裝。</param>
    /// <param name="certificate">The X.509 certificate used to sign the package. / 用於簽署封裝的 X.509 憑證。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous signing operation. / 代表非同步簽署作業的工作。</returns>
    public static Task SignAsync(
        OdfPackage package,
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        return SignAsync(package, certificate, new OdfSigningOptions { Level = XadesLevel.None }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously signs the ODF package with the specified signing options.
    /// 使用指定簽署選項非同步簽署 ODF 封裝。
    /// </summary>
    /// <param name="package">The ODF package to sign. / 要簽署的 ODF 封裝。</param>
    /// <param name="certificate">The X.509 certificate used to sign the package. / 用於簽署封裝的 X.509 憑證。</param>
    /// <param name="options">The signing options. / 簽署選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous signing operation. / 代表非同步簽署作業的工作。</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 寫入與 HTTP（TSA／CRL）期間協作檢查取消語彙。
    /// </remarks>
    public static Task SignAsync(
        OdfPackage package,
        X509Certificate2 certificate,
        OdfSigningOptions options,
        CancellationToken cancellationToken = default)
    {
        return OdfSignatureSigner.SignAsync(package, certificate, options, cancellationToken);
    }

    /// <summary>
    /// Executes the VerifySignatures operation.
    /// 驗證 ODF 封裝中的所有數位簽章。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="certificates">輸出參數，包含所有已驗證的憑證集合</param>
    /// <returns>若所有簽章皆有效，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public static bool VerifySignatures(OdfPackage package, out X509Certificate2Collection certificates)
    {
        certificates = new X509Certificate2Collection();
        var result = VerifySignatures(package, new OdfSigningOptions { Level = XadesLevel.None, AllowUntrustedRoot = true });
        foreach (var sig in result.Signatures)
        {
            if (sig.Certificate != null)
                certificates.Add(sig.Certificate);
        }
        return result.IsValid;
    }

    /// <summary>
    /// Executes the VerifySignatures operation.
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <returns>詳細的數位簽章驗證結果</returns>
    /// <remarks>
    /// Prefer <see cref="VerifySignaturesAsync(OdfPackage, OdfSigningOptions?, CancellationToken)"/> in server environments to avoid blocking request threads.
    /// 在 ASP.NET Core 等伺服器環境中，請優先使用 <see cref="VerifySignaturesAsync(OdfPackage, OdfSigningOptions?, CancellationToken)"/> 以避免阻塞要求執行緒。
    /// </remarks>
    public static OdfSignatureValidationResult VerifySignatures(OdfPackage package, OdfSigningOptions? options = null)
    {
        return VerifySignaturesAsync(package, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes the VerifySignaturesAsync operation.
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果（非同步）。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步驗證作業的工作，其結果包含詳細的數位簽章驗證結果</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在簽章解析與 HTTP（CRL）期間協作檢查取消語彙。
    /// </remarks>
    public static Task<OdfSignatureValidationResult> VerifySignaturesAsync(
        OdfPackage package,
        OdfSigningOptions? options = null,
        CancellationToken cancellationToken = default)
        => OdfSignatureVerifier.VerifySignaturesAsync(package, options, cancellationToken);
}

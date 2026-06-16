using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝數位簽章驗證管線（內部協作者，委派至 <see cref="OdfSigner"/> 實作）。
/// </summary>
internal static class OdfSignatureVerifier
{
    /// <summary>
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果（非同步）。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <returns>代表非同步作業的工作，其結果包含詳細的數位簽章驗證結果</returns>
    internal static Task<OdfSignatureValidationResult> VerifySignaturesAsync(
        OdfPackage package,
        OdfSigningOptions? options = null)
        => OdfSigner.VerifySignaturesAsync(package, options);
}

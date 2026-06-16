using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝數位簽章簽署管線（內部協作者，委派至 <see cref="OdfSigner"/> 實作）。
/// </summary>
internal static class OdfSignatureSigner
{
    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署（非同步）。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    /// <param name="options">簽署選項</param>
    /// <returns>代表非同步作業的工作</returns>
    internal static Task SignAsync(
        OdfPackage package,
        X509Certificate2 certificate,
        OdfSigningOptions options)
        => OdfSigner.SignCoreAsync(package, certificate, options);
}

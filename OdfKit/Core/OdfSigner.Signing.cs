using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;
/// <summary>
/// Implements XMLDSig and XAdES signing for ODF packages.
/// 實作 ODF 封裝的 XMLDSig 與 XAdES 簽署流程。
/// </summary>

public static partial class OdfSigner
{
    #region Signing

    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署（非同步）；委派至 <see cref="OdfSignatureSigner"/>。
    /// </summary>
    internal static Task SignCoreAsync(
        OdfPackage package,
        X509Certificate2 certificate,
        OdfSigningOptions options,
        CancellationToken cancellationToken = default) =>
        OdfSignatureSigner.SignAsync(package, certificate, options, cancellationToken);

    #endregion
}

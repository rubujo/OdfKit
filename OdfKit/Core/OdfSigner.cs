using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 提供 ODF 封裝的數位簽署與驗證功能。
/// </summary>
public static partial class OdfSigner
{
    private static readonly HttpClient s_httpClient = new();

    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署，支援同僚聯署。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    public static void Sign(OdfPackage package, X509Certificate2 certificate)
    {
        Sign(package, certificate, new OdfSigningOptions { Level = XadesLevel.None });
    }

    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署，支援自訂選項。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    /// <param name="options">簽署選項</param>
    public static void Sign(OdfPackage package, X509Certificate2 certificate, OdfSigningOptions options)
    {
        SignAsync(package, certificate, options).GetAwaiter().GetResult();
    }
    /// <summary>
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
            {
                certificates.Add(sig.Certificate);
            }
        }
        return result.IsValid;
    }

    /// <summary>
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <returns>詳細的數位簽章驗證結果</returns>
    public static OdfSignatureValidationResult VerifySignatures(OdfPackage package, OdfSigningOptions? options = null)
    {
        return OdfSignatureVerifier.VerifySignaturesAsync(package, options).GetAwaiter().GetResult();
    }
}

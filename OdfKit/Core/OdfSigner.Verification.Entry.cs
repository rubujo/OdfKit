using System;
using System.Threading.Tasks;
using System.Xml;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region Verification

    /// <summary>
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果（非同步）。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <returns>代表非同步作業的工作，其結果包含詳細的數位簽章驗證結果</returns>
    public static async Task<OdfSignatureValidationResult> VerifySignaturesAsync(OdfPackage package, OdfSigningOptions? options = null)
    {
        options ??= new OdfSigningOptions();
        var result = new OdfSignatureValidationResult { IsValid = true };

        if (package == null)
            throw new ArgumentNullException(nameof(package));

        if (!package.HasEntry(SignaturePath))
        {
            result.IsValid = false;
            return result;
        }

        try
        {
            var doc = new XmlDocument();
            using (var stream = package.GetEntryStream(SignaturePath))
            {
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using var reader = XmlReader.Create(stream, readerSettings);
                doc.Load(reader);
            }

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("ds", OdfNamespaces.Ds);
            nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var signatureNodes = doc.SelectNodes("//ds:Signature", nsManager);
            if (signatureNodes == null || signatureNodes.Count == 0)
            {
                result.IsValid = false;
                return result;
            }

            bool overallValid = true;

            foreach (XmlNode signatureNode in signatureNodes)
            {
                var singleResult = new OdfSingleSignatureValidationResult
                {
                    SignatureId = (signatureNode as XmlElement)?.GetAttribute("Id"),
                    IsSignatureValid = false,
                    IsCertificateValid = false,
                    IsChainValid = false,
                    IsTimestampValid = true,
                    IsRevocationValid = true
                };
                result.Signatures.Add(singleResult);
                singleResult.ValidationSteps.Add($"Starting verification for signature ID: {singleResult.SignatureId}");

                if (!await VerifySingleSignatureAsync(signatureNode, doc, package, options, nsManager, singleResult))
                    overallValid = false;
            }

            result.IsValid = overallValid;
            return result;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Error("Error during digital signature verification", ex);
            result.IsValid = false;
            return result;
        }
    }

    #endregion
}

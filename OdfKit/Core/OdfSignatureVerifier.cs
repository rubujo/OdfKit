using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝數位簽章驗證管線（內部協作者）。
/// </summary>
internal static partial class OdfSignatureVerifier
{
    internal const long MaxEmbeddedCertificateBytes = 1024 * 1024;
    internal const long MaxEmbeddedTimestampBytes = 1024 * 1024;
    internal const long MaxEmbeddedCrlBytes = 10 * 1024 * 1024;

    /// <summary>
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果（非同步）。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步驗證作業的工作，其結果包含詳細的數位簽章驗證結果</returns>
    internal static async Task<OdfSignatureValidationResult> VerifySignaturesAsync(
        OdfPackage package,
        OdfSigningOptions? options = null,
        CancellationToken cancellationToken = default)
        => await VerifySignaturesAsync(
            package,
            options,
            OdfSignatureProfile.Document,
            cancellationToken).ConfigureAwait(false);

    internal static async Task<OdfSignatureValidationResult> VerifySignaturesAsync(
        OdfPackage package,
        OdfSigningOptions? options,
        OdfSignatureProfile profile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        options ??= new OdfSigningOptions();
        var result = new OdfSignatureValidationResult { IsValid = true };

        if (package == null)
            throw new ArgumentNullException(nameof(package));

        if (!package.HasEntry(profile.SignaturePath))
        {
            result.IsValid = false;
            return result;
        }

        try
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var stream = package.GetEntryStream(profile.SignaturePath))
            {
                var readerSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = package.LoadOptions.MaxXmlCharactersInDocument > 0
                        ? package.LoadOptions.MaxXmlCharactersInDocument
                        : 0
                };
                using var reader = XmlReader.Create(stream, readerSettings);
                doc.Load(reader);
            }

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("ds", OdfNamespaces.Ds);
            nsManager.AddNamespace("xades", OdfNamespaces.Xades);

            var signatureNodes = doc.SelectNodes("//ds:Signature", nsManager);
            if (signatureNodes == null || signatureNodes.Count == 0)
            {
                result.IsValid = false;
                return result;
            }

            bool overallValid = true;

            foreach (XmlNode signatureNode in signatureNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

                if (!await VerifySingleSignatureAsync(signatureNode, doc, package, options, profile, nsManager, singleResult, cancellationToken)
                    .ConfigureAwait(false))
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

    internal static byte[] DecodeBase64WithLimit(string value, long maxDecodedBytes, string errorMessageKey)
    {
        string trimmed = value.Trim();
        long maxBase64Length = ((maxDecodedBytes + 2) / 3) * 4;
        if (maxDecodedBytes > 0 && trimmed.Length > maxBase64Length)
        {
            throw new System.Security.SecurityException(
                OdfLocalizer.GetMessage(errorMessageKey, trimmed.Length, maxBase64Length));
        }

        byte[] decoded = Convert.FromBase64String(trimmed);
        if (maxDecodedBytes > 0 && decoded.LongLength > maxDecodedBytes)
        {
            throw new System.Security.SecurityException(
                OdfLocalizer.GetMessage(errorMessageKey, decoded.LongLength, maxDecodedBytes));
        }

        return decoded;
    }

    /// <summary>
    /// 記錄簽章驗證失敗結果並回傳 <see langword="false"/>：設定錯誤碼與在地化錯誤訊息，
    /// 並透過 <paramref name="markInvalid"/> 更新對應的驗證階段旗標（如 <c>IsSignatureValid</c>）。
    /// </summary>
    private static bool Fail(
        Action markInvalid,
        OdfSingleSignatureValidationResult result,
        string errorCode,
        string messageKey,
        params object?[] args)
    {
        markInvalid();
        result.ErrorCode = errorCode;
        result.ErrorMessage = OdfLocalizer.GetMessage(messageKey, args);
        return false;
    }

    /// <summary>
    /// 與 <see cref="Fail"/> 相同，另外將 <paramref name="warningDetail"/>（通常是原始例外訊息）加入 Warnings。
    /// </summary>
    private static bool FailWithWarning(
        Action markInvalid,
        OdfSingleSignatureValidationResult result,
        string errorCode,
        string messageKey,
        string warningDetail,
        params object?[] args)
    {
        markInvalid();
        result.ErrorCode = errorCode;
        result.ErrorMessage = OdfLocalizer.GetMessage(messageKey, args);
        result.Warnings.Add(warningDetail);
        return false;
    }
}


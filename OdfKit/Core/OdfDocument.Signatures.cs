using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;
/// <summary>
/// Adds high-level digital signature APIs for ODF documents.
/// 提供 ODF 文件的高階數位簽章 API。
/// </summary>

public abstract partial class OdfDocument
{
    #region High-Level Digital Signatures


    /// <summary>
    /// Signs the document with the specified X.509 certificate.
    /// 使用指定的 X.509 憑證簽署文件。
    /// </summary>
    /// <param name="certificate">The certificate used to sign the document. / 用於簽署文件的憑證。</param>
    /// <remarks>
    /// Prefer <see cref="SignAsync(X509Certificate2, CancellationToken)"/> in server environments to avoid blocking request threads.
    /// 在 ASP.NET Core 等伺服器環境中，請優先使用 <see cref="SignAsync(X509Certificate2, CancellationToken)"/> 以避免阻塞要求執行緒。
    /// </remarks>
    public void Sign(X509Certificate2 certificate)
    {
        SignAsync(certificate).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously signs the document with the specified X.509 certificate.
    /// 非同步使用指定的 X.509 憑證簽署文件。
    /// </summary>
    /// <returns>A task representing the asynchronous signing operation. / 代表非同步簽署作業的工作。</returns>
    /// <remarks>
    /// 若  已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 DOM 寫入、ZIP 寫入與 HTTP（TSA／CRL）期間協作檢查取消語彙。
    /// </remarks>
    public Task SignAsync(X509Certificate2 certificate) => SignAsync(certificate, default);

    /// <summary>
    /// Full overload of SignAsync that accepts certificate and cancellationToken.
    /// SignAsync 完整多載：接受 certificate 與 cancellationToken。
    /// </summary>
    public async Task SignAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        await SignDocumentAsync(certificate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Signs document async.
    /// 非同步使用指定的 X.509 憑證對文件進行一鍵式數位簽章。
    /// </summary>
    /// <returns>代表非同步簽章作業的工作</returns>
    /// <remarks>
    /// 此方法是計畫文件中 <c>SignDocumentAsync</c> 入口的文件層別名；行為等同於
    /// <see cref="SignAsync(X509Certificate2, CancellationToken)"/>。
    /// </remarks>
    public Task SignDocumentAsync(X509Certificate2 certificate) => SignDocumentAsync(certificate, new OdfSigningOptions { Level = XadesLevel.None }, default);

    /// <summary>
    /// Full overload of SignDocumentAsync that accepts certificate and cancellationToken.
    /// SignDocumentAsync 完整多載：接受 certificate 與 cancellationToken。
    /// </summary>
    public Task SignDocumentAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        return SignDocumentAsync(certificate, new OdfSigningOptions { Level = XadesLevel.None }, cancellationToken);
    }

    /// <summary>
    /// Signs document async.
    /// 非同步使用指定的 X.509 憑證與簽章選項對文件進行一鍵式數位簽章。
    /// </summary>
    /// <returns>代表非同步簽章作業的工作</returns>
    /// <remarks>
    /// 若  已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 DOM 寫入、ZIP 寫入與 HTTP（TSA／CRL）期間協作檢查取消語彙。
    /// </remarks>
    public Task SignDocumentAsync(X509Certificate2 certificate, OdfSigningOptions? options) => SignDocumentAsync(certificate, options, default);

    /// <summary>
    /// Full overload of SignDocumentAsync that accepts certificate, options, and cancellationToken.
    /// SignDocumentAsync 完整多載：接受 certificate、options 與 cancellationToken。
    /// </summary>
    public async Task SignDocumentAsync(
        X509Certificate2 certificate,
        OdfSigningOptions? options,
        CancellationToken cancellationToken)
    {
        StyleEngine.DeduplicateAndSaveStyles();
        OdfDocumentPersistenceEngine.WriteAllDomEntries(PersistenceCollaborators, OdfSaveOptions.Default);

        await OdfSigner.SignAsync(
            Package,
            certificate,
            options ?? new OdfSigningOptions { Level = XadesLevel.None },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets signature summary.
    /// 取得文件封裝內數位簽章專案的摘要狀態。
    /// </summary>
    /// <returns>描述簽章專案存在狀態、可讀性與簽章數量的摘要</returns>
    public OdfDocumentSignatureSummary GetSignatureSummary()
    {
        if (!Package.HasEntry(OdfSignerConstants.SignaturePath))
        {
            return OdfDocumentSignatureSummary.Unsigned(OdfSignerConstants.SignaturePath);
        }

        try
        {
            using Stream stream = Package.GetEntryStream(OdfSignerConstants.SignaturePath);
            int signatureCount = CountSignatureElements(stream, Package.LoadOptions.MaxXmlCharactersInDocument);
            return OdfDocumentSignatureSummary.Readable(OdfSignerConstants.SignaturePath, signatureCount);
        }
        // 簽章摘要為最佳努力查詢：無法讀取時回傳 Unreadable，不向上拋出。
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is XmlException)
        {
            return OdfDocumentSignatureSummary.Unreadable(OdfSignerConstants.SignaturePath, ex.Message);
        }
    }

    /// <summary>
    /// Verifies signatures.
    /// 驗證文件中的所有數位簽章。
    /// </summary>
    /// <param name="certificates">輸出參數，傳回驗證通過的憑證集合</param>
    /// <returns>若所有簽章皆驗證成功則傳回 true；否則傳回 false</returns>
    public bool VerifySignatures(out X509Certificate2Collection certificates)
    {
        return OdfSigner.VerifySignatures(Package, out certificates);
    }

    /// <summary>
    /// Verifies signatures.
    /// 驗證文件中的所有數位簽章，並傳回詳細驗證結果。
    /// </summary>
    /// <returns>詳細的數位簽章驗證結果</returns>
    public OdfSignatureValidationResult VerifySignatures() => VerifySignatures((OdfSigningOptions?)null);

    /// <summary>
    /// Full overload of VerifySignatures that accepts options.
    /// VerifySignatures 完整多載：接受 options。
    /// </summary>
    public OdfSignatureValidationResult VerifySignatures(OdfSigningOptions? options)
    {
        return OdfSigner.VerifySignatures(Package, options);
    }

    /// <summary>
    /// Verifies signatures async.
    /// 非同步驗證文件中的所有數位簽章，並傳回詳細驗證結果。
    /// </summary>
    /// <returns>代表非同步驗證作業的工作，其結果包含詳細的數位簽章驗證結果</returns>
    /// <remarks>
    /// 若  已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在簽章解析與 HTTP（CRL）期間協作檢查取消語彙。
    /// </remarks>
    public Task<OdfSignatureValidationResult> VerifySignaturesAsync() => VerifySignaturesAsync(null, default);

    /// <summary>
    /// Asynchronously verifies all digital signatures with a cancellation token.
    /// 以取消語彙基元非同步驗證所有數位簽章。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the detailed signature validation result. / 代表非同步驗證作業的工作，其結果包含詳細的數位簽章驗證結果。</returns>
    public Task<OdfSignatureValidationResult> VerifySignaturesAsync(CancellationToken cancellationToken) =>
        VerifySignaturesAsync(null, cancellationToken);

    /// <summary>
    /// Short overload of VerifySignaturesAsync that accepts options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 options；其餘可選參數使用預設值並轉呼叫最長 VerifySignaturesAsync 多載。
    /// </summary>
    public Task<OdfSignatureValidationResult> VerifySignaturesAsync(OdfSigningOptions? options) => VerifySignaturesAsync(options, default);

    /// <summary>
    /// Full overload of VerifySignaturesAsync that accepts options and cancellationToken.
    /// VerifySignaturesAsync 完整多載：接受 options 與 cancellationToken。
    /// </summary>
    public Task<OdfSignatureValidationResult> VerifySignaturesAsync(
        OdfSigningOptions? options,
        CancellationToken cancellationToken)
    {
        return OdfSigner.VerifySignaturesAsync(Package, options, cancellationToken);
    }

    private static int CountSignatureElements(Stream stream, long maxCharsInDocument = 0)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharsInDocument > 0 ? maxCharsInDocument : 0
        };

        int count = 0;
        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element &&
                reader.LocalName == "Signature" &&
                reader.NamespaceURI == OdfNamespaces.Ds)
            {
                count++;
            }
        }

        return count;
    }


    #endregion
}
